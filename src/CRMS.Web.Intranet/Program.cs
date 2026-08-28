using Blazored.LocalStorage;
using CRMS.Infrastructure;
using CRMS.Infrastructure.Persistence;
using CRMS.Web.Intranet.Components;
using CRMS.Web.Intranet.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Database connection string
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

// Add Infrastructure (Database, Repositories, Services)
builder.Services.AddInfrastructure(connectionString, builder.Configuration);

// Add HTTP Context accessor for audit context
builder.Services.AddHttpContextAccessor();

// Add memory cache (required by ReportingService)
builder.Services.AddMemoryCache();

// Add services
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// RH-SHF's public profiling form (Pages/Rhshf/*) — plain Razor Pages, deliberately NOT an
// interactive Blazor component: anonymous FAC users must not hold open a live SignalR circuit on
// the same app that serves bank staff. See docs/rhshf resources/ for the full design.
builder.Services.AddRazorPages();

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();

// Required so that [Authorize] pages can redirect to /login on hard refresh
// instead of crashing with "IAuthenticationService not found".
// The Blazor circuit manages actual auth state via AuthService.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options => { options.LoginPath = "/login"; })
    // RH-SHF's own cookie scheme — completely independent of staff login. Established only after
    // the §4.2 token is verified+consumed (Phase 2); carries a single claim binding the browser
    // session to one case reference. Never touches AuthService/AuthenticationStateProvider.
    .AddCookie("RhshfProfiling", options =>
    {
        options.Cookie.Name = "RhshfProfilingAuth";
        options.ExpireTimeSpan = TimeSpan.FromHours(2);
        options.SlidingExpiration = true;
        options.LoginPath = "/rhshf/session-expired";
        options.AccessDeniedPath = "/rhshf/session-expired";
    });

// Application service (direct calls to handlers - no HTTP)
builder.Services.Configure<CRMS.Web.Intranet.Services.BankSettings>(builder.Configuration.GetSection(CRMS.Web.Intranet.Services.BankSettings.SectionName));
builder.Services.Configure<CRMS.Web.Intranet.Services.CollateralHaircutSettings>(builder.Configuration.GetSection(CRMS.Web.Intranet.Services.CollateralHaircutSettings.SectionName));
builder.Services.AddScoped<ApplicationService>();
builder.Services.AddSingleton<CRMS.Web.Intranet.Services.StatementFileParserService>();

// Auth service for Blazor auth state
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
    sp => sp.GetRequiredService<AuthService>());

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<CRMSDbContext>();

    try
    {
        // AutoMigrate is a toggle (Database:AutoMigrate, also settable via the
        // Database__AutoMigrate env var). When false, pending migrations are NOT
        // applied automatically — apply them through a controlled, backed-up step.
        var autoMigrate = builder.Configuration.GetValue("Database:AutoMigrate", true);
        if (autoMigrate)
        {
            startupLogger.LogInformation("Applying database migrations...");
            await db.Database.MigrateAsync();
            startupLogger.LogInformation("Migrations applied successfully.");
        }
        else
        {
            var pending = (await db.Database.GetPendingMigrationsAsync()).ToList();
            startupLogger.LogWarning(
                "Database:AutoMigrate is disabled — skipping {Count} pending migration(s): {Migrations}. Apply them manually.",
                pending.Count, pending.Count == 0 ? "(none)" : string.Join(", ", pending));
        }

        var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
        var passwordHasher = scope.ServiceProvider.GetRequiredService<CRMS.Application.Identity.Interfaces.IPasswordHasher>();

        startupLogger.LogInformation("Seeding data (isDevelopment={IsDev})...", app.Environment.IsDevelopment());
        await SeedData.SeedAsync(db, seedLogger, passwordHasher, app.Environment.IsDevelopment());
        startupLogger.LogInformation("Seeding complete.");

        if (app.Environment.IsDevelopment())
            await ComprehensiveDataSeeder.SeedComprehensiveDataAsync(db, seedLogger, passwordHasher);
    }
    catch (Exception ex)
    {
        startupLogger.LogError(ex, "Startup failed during migration or seeding.");
        throw;
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Must come before UseHttpsRedirection so that the scheme/host seen by the app
// reflects the public-facing URL forwarded by the AWS ALB (X-Forwarded-Proto/Host).
// Without this, Navigation.BaseUri resolves to the internal http:// address and
// the password-reset link in emails points to the wrong host.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost
});

app.UseStatusCodePagesWithReExecute("/not-found");
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapRazorPages();

// Document file serving endpoints
app.MapGet("/api/documents/{id:guid}/view", async (Guid id, CRMSDbContext db, CRMS.Domain.Interfaces.IFileStorageService fileStorage, HttpContext httpContext) =>
{
    var document = await db.Set<CRMS.Domain.Aggregates.LoanApplication.LoanApplicationDocument>()
        .FirstOrDefaultAsync(d => d.Id == id);
    
    if (document == null)
        return Results.NotFound("Document not found");
    
    try
    {
        var fileBytes = await fileStorage.DownloadAsync(document.FilePath);
        // Set Content-Disposition to inline for viewing in browser
        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return Results.File(fileBytes, document.ContentType);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving file: {ex.Message}");
    }
}).DisableAntiforgery();

app.MapGet("/api/documents/{id:guid}/download", async (Guid id, CRMSDbContext db, CRMS.Domain.Interfaces.IFileStorageService fileStorage) =>
{
    var document = await db.Set<CRMS.Domain.Aggregates.LoanApplication.LoanApplicationDocument>()
        .FirstOrDefaultAsync(d => d.Id == id);
    
    if (document == null)
        return Results.NotFound("Document not found");
    
    try
    {
        var fileBytes = await fileStorage.DownloadAsync(document.FilePath);
        return Results.File(fileBytes, "application/octet-stream", document.FileName);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving file: {ex.Message}");
    }
}).DisableAntiforgery();

// Collateral document file serving endpoints
app.MapGet("/api/collateral-documents/{id:guid}/view", async (Guid id, CRMSDbContext db, CRMS.Domain.Interfaces.IFileStorageService fileStorage, HttpContext httpContext) =>
{
    var document = await db.Set<CRMS.Domain.Aggregates.Collateral.CollateralDocument>()
        .FirstOrDefaultAsync(d => d.Id == id);
    
    if (document == null)
        return Results.NotFound("Document not found");
    
    if (string.IsNullOrEmpty(document.StoragePath))
        return Results.NotFound("Document file path not available");
    
    try
    {
        var fileBytes = await fileStorage.DownloadAsync(document.StoragePath);
        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return Results.File(fileBytes, document.ContentType ?? "application/octet-stream");
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving file: {ex.Message}");
    }
}).DisableAntiforgery();

app.MapGet("/api/collateral-documents/{id:guid}/download", async (Guid id, CRMSDbContext db, CRMS.Domain.Interfaces.IFileStorageService fileStorage) =>
{
    var document = await db.Set<CRMS.Domain.Aggregates.Collateral.CollateralDocument>()
        .FirstOrDefaultAsync(d => d.Id == id);
    
    if (document == null)
        return Results.NotFound("Document not found");
    
    if (string.IsNullOrEmpty(document.StoragePath))
        return Results.NotFound("Document file path not available");
    
    try
    {
        var fileBytes = await fileStorage.DownloadAsync(document.StoragePath);
        return Results.File(fileBytes, "application/octet-stream", document.FileName);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving file: {ex.Message}");
    }
}).DisableAntiforgery();

// NAMP document file serving endpoints
app.MapGet("/api/namp-documents/{id:guid}/view", async (Guid id, CRMSDbContext db, CRMS.Domain.Interfaces.IFileStorageService fileStorage, HttpContext httpContext) =>
{
    var document = await db.Set<CRMS.Domain.Aggregates.Namp.NampDocument>()
        .FirstOrDefaultAsync(d => d.Id == id);

    if (document == null)
        return Results.NotFound("Document not found");

    try
    {
        var fileBytes = await fileStorage.DownloadAsync(document.StoragePath);
        var contentType = string.IsNullOrEmpty(document.ContentType) ? "application/octet-stream" : document.ContentType;
        httpContext.Response.Headers.ContentDisposition = $"inline; filename=\"{document.FileName}\"";
        return Results.File(fileBytes, contentType);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving file: {ex.Message}");
    }
}).DisableAntiforgery();

app.MapGet("/api/namp-documents/{id:guid}/download", async (Guid id, CRMSDbContext db, CRMS.Domain.Interfaces.IFileStorageService fileStorage) =>
{
    var document = await db.Set<CRMS.Domain.Aggregates.Namp.NampDocument>()
        .FirstOrDefaultAsync(d => d.Id == id);

    if (document == null)
        return Results.NotFound("Document not found");

    try
    {
        var fileBytes = await fileStorage.DownloadAsync(document.StoragePath);
        var contentType = string.IsNullOrEmpty(document.ContentType) ? "application/octet-stream" : document.ContentType;
        return Results.File(fileBytes, contentType, document.FileName);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error retrieving file: {ex.Message}");
    }
}).DisableAntiforgery();

// Financial Statement Excel template endpoints
app.MapGet("/api/financial-statements/template", () =>
{
    var excelService = new FinancialStatementExcelService();
    var templateBytes = excelService.GenerateBlankTemplate();
    return Results.File(templateBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FinancialStatementTemplate.xlsx");
}).DisableAntiforgery();

app.MapGet("/api/financial-statements/sample", () =>
{
    var excelService = new FinancialStatementExcelService();
    var sampleBytes = excelService.GenerateSampleTemplate();
    return Results.File(sampleBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "FinancialStatementSample.xlsx");
}).DisableAntiforgery();

// Dev-only endpoint to reset passwords
if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/reset-passwords", async (CRMSDbContext db, CRMS.Application.Identity.Interfaces.IPasswordHasher hasher) =>
    {
        var users = await db.Users.ToListAsync();
        var hash = hasher.HashPassword("Password1$$$");
        foreach (var user in users)
        {
            user.SetPasswordHash(hash);
        }
        await db.SaveChangesAsync();
        return Results.Ok(new { message = $"Reset {users.Count} user passwords to 'Password1$$$'", users = users.Select(u => u.Email).ToList() });
    });
}

app.Run();
