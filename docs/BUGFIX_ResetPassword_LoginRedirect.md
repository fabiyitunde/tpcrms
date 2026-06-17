# Bug Post-Mortem: Password-Reset / Forgot-Password pages redirect to `/login`

| | |
|---|---|
| **Date identified / fixed** | 2026-06-16 |
| **Severity** | High — password self-service unusable for the people who need it most (locked-out, logged-out users) |
| **Affected app** | `CRMS.Web.Intranet` (Blazor Server) |
| **Affected routes** | `/reset-password`, `/forgot-password` (and any anonymous page using `EmptyLayout`) |
| **Status** | Fixed (working tree on `namp-crms`); pending commit + production publish |

---

## 1. Summary (TL;DR)

Unauthenticated users who opened the password-reset link (or navigated to `/forgot-password`) were silently redirected to `/login` and never saw the reset form. The cause was **not** the email, the reset token, AWS SES click-tracking, the deployment, or the load balancer — it was a **client-side authentication redirect in the Blazor render pipeline**:

- `Routes.razor` uses `AuthorizeRouteView` with `DefaultLayout="MainLayout"`.
- Authentication state is resolved **asynchronously** (it is read from the browser's `localStorage`).
- During that async "authorizing" window, **`MainLayout` is rendered transiently for every route — including the anonymous pages that declare `@layout EmptyLayout`.**
- `MainLayout.OnInitializedAsync` contains: *"if the user is not authenticated, `NavigateTo("/login")`."*
- Therefore **every logged-out page load was bounced to `/login`.** `/login` itself appeared to work only because redirecting `/login` → `/login` is a no-op. `/forgot-password` and `/reset-password` visibly bounced.

This also explains the original, confusing **"works for some users, not for others"** report (see §3).

A second, unrelated change had also been introduced to `ResetPassword.razor` during debugging — a per-page `@rendermode @(new InteractiveServerRenderMode(prerender: true))` override — which was reverted as part of the fix.

---

## 2. Symptoms

- Clicking the reset link in the email opened the site and immediately showed the **login page** instead of the "Set a new password" form.
- The same happened when the **raw link copied straight from the `Notifications` table** was pasted into the browser.
- The behaviour appeared **erratic**: some users reached the reset form, others were sent to login. Switching browsers did not help for an affected user.
- `/forgot-password` exhibited the same redirect (often unnoticed because users normally reach it via an in-app link while the SPA is already loaded).

---

## 3. Why it looked "erratic" / "only some users"

Authentication state is stored in the browser's `localStorage` (`authToken` / `authUser`) and read by `AuthService.GetAuthenticationStateAsync()`.

- A user who was **already logged in** in that browser had a valid token in `localStorage`. When `MainLayout` ran its check, the user resolved as **authenticated**, so **no redirect** happened and the reset page rendered. → "It works."
- A user who was **logged out** (the normal state for someone using a password-reset link, and the state of any fresh browser / private window) resolved as **unauthenticated**, so `MainLayout` redirected them to `/login`. → "It goes to login."

So the deciding variable was simply *"does this browser currently hold a valid session token?"* — which looks random across users but is fully deterministic per browser/session.

---

## 4. Why it was hard to diagnose (the dead ends)

The application is configured with `prerender: false`:

```razor
<!-- App.razor -->
<Routes @rendermode="@(new InteractiveServerRenderMode(prerender: false))" />
```

With prerendering disabled, the **initial HTTP GET returns only a blank host-page shell**; the page's real content (and crucially, the auth/redirect logic) executes later, over the Blazor **WebSocket circuit**.

This invalidated every HTTP-level test:

- `curl -i https://.../reset-password?token=test` returned **`200 OK`** with the shell — and *no* `Location: /login`. This made the page look healthy at the HTTP layer even though the live render redirected.
- Running the app locally in **Development** *and* in **Production mode** both returned `200` to `curl` — reinforcing the false conclusion that "the code is fine."

Because of that false signal, several **incorrect** root-cause theories were pursued and discarded:

1. **Stale deployment** — disproven: redeploys did not help, and a local run of the current code reproduced the issue once the *circuit* was exercised.
2. **Mixed Elastic Beanstalk instance fleet** behind the ALB — disproven.
3. **AWS SES click-tracking** (`awstrack.me` link wrapping) — real but irrelevant: the wrapped URL decodes cleanly to the correct `/reset-password?token=<base64url>` (the token is URL-safe and survives), so it was not the cause.
4. **Recipient-side link rewriting** (Microsoft Defender Safe Links / Proofpoint / Mimecast) — disproven by the raw-DB-link test.

**The breakthrough** was to stop testing the HTTP shell and **drive the live Blazor circuit with a real browser** (Playwright using the installed Chrome). That reproduced the redirect locally and, by capturing the DOM every 50 ms, revealed the transient `MainLayout` (sidebar) rendering on `/reset-password` immediately before the bounce.

---

## 5. Root cause (detailed)

### Relevant components

`Routes.razor`:

```razor
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
            <NotAuthorized>
                <RedirectToLogin />
            </NotAuthorized>
        </AuthorizeRouteView>
        ...
```

`MainLayout.razor` (before the fix):

```csharp
protected override async Task OnInitializedAsync()
{
    var state = await AuthService.GetAuthenticationStateAsync();   // async — reads localStorage
    if (!(state.User.Identity?.IsAuthenticated ?? false))
        Navigation.NavigateTo("/login", replace: true);
}
```

`AuthService : AuthenticationStateProvider` reads the token from `localStorage` (via JS interop) — i.e. authentication resolution is **asynchronous** and only possible once the interactive circuit is live.

The anonymous pages all declare an explicit empty layout, e.g.:

```razor
@page "/reset-password"
@layout EmptyLayout
```

### The chain of events (unauthenticated user → `/reset-password`)

1. HTTP `GET /reset-password?token=…` → server returns the blank shell (`prerender:false`). **200 OK.**
2. Browser connects the Blazor WebSocket circuit and begins interactive rendering.
3. `AuthorizeRouteView` must resolve the `AuthenticationState` before it can render the page as *Authorized*. Because the state provider is **async**, there is a brief **authorizing phase**, during which content is rendered under the **`DefaultLayout` = `MainLayout`** (not the page's `EmptyLayout`).
4. `MainLayout.OnInitializedAsync` runs, awaits `GetAuthenticationStateAsync()` (empty `localStorage` → **unauthenticated**), and calls `NavigateTo("/login", replace: true)`.
5. The navigation fires **before** the page ever resolves to its `EmptyLayout`/Authorized render, so the reset form is never shown.

### Why `/login` "worked" but `/forgot-password` and `/reset-password` did not

The redirect target is `/login`. For `/login` the redirect is a **self-navigation no-op**, so it stayed and rendered. For the other anonymous pages it was a real navigation, so they bounced. All three pages are structurally identical (`@layout EmptyLayout`, no `[Authorize]`) — the *only* difference in observed behaviour was the destination of the redirect.

### Secondary issue

`ResetPassword.razor` had been given a per-page render-mode override during debugging:

```razor
@rendermode @(new InteractiveServerRenderMode(prerender: true))
```

This was **not** in the committed baseline and is its own hazard (prerendering renders server-side where `localStorage` is unavailable, so the auth check fails differently). It was reverted.

---

## 6. Reproduction

Prerequisite: the bug only reproduces when the **interactive circuit** runs — a plain HTTP request will not show it.

1. Run `CRMS.Web.Intranet` locally.
2. Using a fresh browser context / private window (**no** session token in `localStorage`), navigate to `http://localhost:5292/reset-password?token=test`.
3. Observe the URL change to `/login`; the reset form never appears.

Automated reproduction used Playwright (system Chrome via `channel: 'chrome'`), navigating to the route, waiting for the circuit, and asserting the final URL / DOM. Polling the DOM every 50 ms captured the transient `MainLayout` (sidebar) on `/reset-password` just before the redirect — the definitive evidence.

---

## 7. The fix

Two changes in `CRMS.Web.Intranet`:

### 7.1 `MainLayout.razor` — do not redirect on anonymous routes

```csharp
// Pages that anonymous users must be able to reach. MainLayout briefly renders for every
// route during AuthorizeRouteView's async "authorizing" phase, so without this guard the
// unauthenticated redirect below bounces these public pages straight to /login.
private static readonly string[] AnonymousRoutes = { "login", "forgot-password", "reset-password" };

protected override async Task OnInitializedAsync()
{
    var path = Navigation.ToBaseRelativePath(Navigation.Uri).Split('?')[0].Trim('/').ToLowerInvariant();
    if (Array.IndexOf(AnonymousRoutes, path) >= 0)
        return;

    var state = await AuthService.GetAuthenticationStateAsync();
    if (!(state.User.Identity?.IsAuthenticated ?? false))
    {
        Navigation.NavigateTo("/login", replace: true);
    }
}
```

### 7.2 `ResetPassword.razor` — revert the per-page render-mode override

```diff
 @page "/reset-password"
-@rendermode @(new InteractiveServerRenderMode(prerender: true))
 @layout EmptyLayout
```

---

## 8. Verification

Re-ran the Playwright browser test (the one that reproduced the bug) against the rebuilt app:

| Route | Before fix | After fix |
|---|---|---|
| `/login` | stays | stays |
| `/forgot-password` | → `/login` | **stays** |
| `/reset-password` | → `/login` | **stays** |
| `/reset-password?token=test` | → `/login` | **stays**, reset form renders (`HAS_RESET_FORM=true`) |

No console errors. Authenticated users and protected pages are unaffected (see §9).

---

## 9. Safety / scope of the fix

- The guard only **skips** the redirect for the three known anonymous routes. Every other route still redirects unauthenticated users to `/login` exactly as before, so **protected pages remain protected**.
- Authenticated users are unaffected on all routes (their auth check passes and no redirect occurs).
- No database, API, or infrastructure changes are involved.

---

## 10. Lessons learned / prevention

1. **A `200 OK` from `curl` proves nothing about a Blazor Server page with `prerender:false`.** It only confirms the *route resolves*; all auth/redirect/render logic runs later in the WebSocket circuit. **Diagnose interactive pages with a real browser** (e.g. Playwright using the installed Chrome), not HTTP probes.
2. **Don't let a layout perform authentication redirects.** Using `MainLayout.OnInitializedAsync` to bounce unauthenticated users is fragile: the layout renders transiently for *every* route during `AuthorizeRouteView`'s async authorizing phase, so it leaks onto pages that explicitly opted into a different layout. The more robust pattern is attribute-based authorization (`[Authorize]` on protected pages, or a fallback authorization policy with `[AllowAnonymous]` on the public pages) handled by `AuthorizeRouteView`'s `<NotAuthorized>` / `RedirectToLogin`. *(Future hardening — the current guard fixes the immediate bug with minimal risk.)*
3. **Avoid per-page `@rendermode` overrides** that conflict with the global render mode unless there is a specific, tested reason.
4. **Trust reproducible symptoms over plausible infrastructure theories.** "Works for some users, not others" pointed at per-session client state (auth in `localStorage`) all along; the infrastructure theories (stale deploy, instance fleet, SES tracking) were comfortable but wrong.

---

## 11. Affected files

| File | Change |
|---|---|
| `src/CRMS.Web.Intranet/Components/Layout/MainLayout.razor` | Added anonymous-route guard before the unauthenticated redirect |
| `src/CRMS.Web.Intranet/Components/Pages/Auth/ResetPassword.razor` | Reverted the per-page `@rendermode prerender:true` override |

Related (separate, also fixed this session): `Routes.razor` / `NotFound.razor` were changed so genuine 404s render a friendly "request a new link" page on `EmptyLayout` instead of falling through to `MainLayout` (which would also have bounced unauthenticated users to `/login`).
