using CRMS.Application.CustomerCheck.Commands;
using CRMS.Application.CustomerCheck.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CRMS.Web.Intranet.Pages.Partner;

/// <summary>
/// Where a partner collects its API credentials from the one-time link the bank emailed it
/// (Admin > API Consumers > Send setup link). Anonymous by design: the token in the address is the
/// only credential, and it is good for one opening.
/// <para>
/// Opening the address (GET) changes nothing and only says what the link is for. The credentials
/// are issued by the button (POST). Mail gateways and link previewers follow links with a GET, so
/// they cannot use the link up or see anything worth having.
/// </para>
/// Plain Razor Page like the RH-SHF pages: no Blazor circuit, no staff sign-in involved.
/// </summary>
public class SetupModel : PageModel
{
    private readonly PeekApiConsumerSetupLinkHandler _peek;
    private readonly RedeemApiConsumerSetupLinkHandler _redeem;

    public SetupModel(PeekApiConsumerSetupLinkHandler peek, RedeemApiConsumerSetupLinkHandler redeem)
    {
        _peek = peek;
        _redeem = redeem;
    }

    /// <summary>What the link is for and whether it can still be opened.</summary>
    public ApiConsumerSetupLinkView Link { get; private set; } = ApiConsumerSetupLinkView.Invalid;

    /// <summary>Set only on the response that issues the credentials.</summary>
    public ApiConsumerSetupRedeemedDto? Issued { get; private set; }

    public string? Error { get; private set; }

    public string WhatItIssues => ApiConsumerSetupLinks.Describe(Link.IssuesKey, Link.IssuesSecret);

    public async Task<IActionResult> OnGetAsync(string token, CancellationToken ct)
    {
        Harden();
        await LoadAsync(token, ct);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string token, CancellationToken ct)
    {
        Harden();
        var result = await _redeem.Handle(
            new RedeemApiConsumerSetupLinkCommand(token, HttpContext.Connection.RemoteIpAddress?.ToString()), ct);
        if (result.IsSuccess && result.Data is not null)
        {
            Issued = result.Data;
            return Page();
        }

        // Say why, from the link's own state: used, expired, cancelled or unknown.
        await LoadAsync(token, ct);
        if (Link.State == Domain.Aggregates.CustomerCheck.ApiConsumerSetupLink.Pending)
            Error = result.Error ?? "Something went wrong. Please try again.";
        return Page();
    }

    private async Task LoadAsync(string token, CancellationToken ct)
    {
        var view = await _peek.Handle(new PeekApiConsumerSetupLinkQuery(token), ct);
        Link = view.IsSuccess && view.Data is not null ? view.Data : ApiConsumerSetupLinkView.Invalid;
    }

    /// <summary>The page shows secrets: nothing may keep a copy, pass the address on, or frame it.</summary>
    private void Harden()
    {
        Response.Headers.CacheControl = "no-store, max-age=0";
        Response.Headers.Pragma = "no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        Response.Headers["X-Frame-Options"] = "DENY";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
    }
}
