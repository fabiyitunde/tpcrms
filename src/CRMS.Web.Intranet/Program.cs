using Blazored.LocalStorage;
using CRMS.Infrastructure;
using CRMS.Infrastructure.Persistence;
using CRMS.Web.Intranet.Components;
using CRMS.Web.Intranet.Services;
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

builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();

// Application service (direct calls to handlers - no HTTP)
builder.Services.AddScoped<ApplicationService>();

// Auth service for Blazor auth state
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider>(
    sp => sp.GetRequiredService<AuthService>());

var app = builder.Build();

// Apply migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CRMSDbContext>();
    await db.Database.MigrateAsync();
    
    // Seed data
    var seedLogger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SeedData");
    await SeedData.SeedAsync(db, seedLogger);
    
    // Seed comprehensive test data (users, applications, etc.)
    var passwordHasher = scope.ServiceProvider.GetRequiredService<CRMS.Application.Identity.Interfaces.IPasswordHasher>();
    await ComprehensiveDataSeeder.SeedComprehensiveDataAsync(db, seedLogger, passwordHasher);
}

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
