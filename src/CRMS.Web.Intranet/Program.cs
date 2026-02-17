using Blazored.LocalStorage;
using CRMS.Web.Intranet.Components;
using CRMS.Web.Intranet.Services;

var builder = WebApplication.CreateBuilder(args);

// API base URL
var apiBaseUrl = builder.Configuration.GetValue<string>("ApiBaseUrl") ?? "https://localhost:7001";

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
    sp => sp.GetRequiredService<AuthService>());
builder.Services.AddScoped<ApiClient>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
