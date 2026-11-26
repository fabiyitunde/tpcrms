using System.Text.RegularExpressions;
using CRMS.Application.Common;
using CRMS.Application.CreditAppraisal;
using CRMS.Domain.Enums;
using CRMS.Domain.Services;
using CRMS.Web.Intranet.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CRMS.Web.Intranet.Services;

/// <summary>A generated file ready to hand to the browser.</summary>
public record DownloadFile(byte[] Content, string FileName);

/// <summary>
/// The credit appraisal as a branded PDF, one per loan track. Each call re-reads the case, so the
/// document carries the appraisal as saved now, not as it was when the page was opened. Every
/// download is written to the data-access log.
/// </summary>
public partial class ApplicationService
{
    public async Task<ApiResponse<DownloadFile>> DownloadCorporateCreditAppraisalAsync(Guid applicationId)
    {
        try
        {
            var app = await GetApplicationDetailAsync(applicationId);
            if (app is null) return ApiResponse<DownloadFile>.Fail("Application not found.");
            var a = app.CreditAppraisal;
            if (a?.SavedAt is null) return ApiResponse<DownloadFile>.Fail("No credit appraisal has been saved for this application yet.");

            var names = _sp.GetRequiredService<IUserNameResolver>();
            var approved = app.Loan.ApprovedAmount is not null;
            var document = new CorporateAppraisalDocument(
                new AppraisalCaseInfo("Corporate Loan", app.ApplicationNumber, app.Customer.CompanyName,
                    NullIfBlank(app.Loan.ProductName),
                    approved ? "Approved amount" : "Amount requested",
                    app.Loan.ApprovedAmount ?? app.Loan.RequestedAmount, "NGN",
                    app.Loan.ApprovedTenorMonths ?? app.Loan.TenorMonths, Spaced(app.Status)),
                a.Dscr, a.Leverage, a.CurrentRatio, a.Ltv, a.CapacityRating, a.Recommendation, a.Notes,
                a.MemoFileName, await names.ResolveAsync(a.SavedByUserId), a.SavedAt);

            var printedBy = await PrintedByAsync(names);
            var bytes = _sp.GetRequiredService<ICreditAppraisalPdfGenerator>().Corporate(document, printedBy.Name, DateTime.UtcNow);
            await AuditAppraisalDownloadAsync(printedBy, "LoanApplication", app.Id, app.ApplicationNumber, app.Id);
            return ApiResponse<DownloadFile>.Ok(new DownloadFile(bytes, FileName(app.ApplicationNumber)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating the credit appraisal PDF for application {Id}", applicationId);
            return ApiResponse<DownloadFile>.Fail("The credit appraisal PDF could not be generated. Try again.");
        }
    }

    public async Task<ApiResponse<DownloadFile>> DownloadNampCreditAppraisalAsync(Guid nampApplicationId)
    {
        try
        {
            var app = await GetNampApplicationByIdAsync(nampApplicationId);
            if (app is null) return ApiResponse<DownloadFile>.Fail("Application not found.");
            if (app.FinancialAppraisalReport is null) return ApiResponse<DownloadFile>.Fail("No financial appraisal has been saved for this application yet.");

            var names = _sp.GetRequiredService<IUserNameResolver>();
            var document = new NampAppraisalDocument(
                new AppraisalCaseInfo("NAMP Equipment Loan", app.ApplicationNumber,
                    string.IsNullOrWhiteSpace(app.CompanyName) ? app.ApplicantName : app.CompanyName,
                    NullIfBlank(app.EquipmentDescription), "Loan amount", app.LoanAmount, "NGN",
                    app.RequestedTenorMonths, Spaced(app.Status)),
                app.FinancialAppraisalReport, app.FinancialAppraisalNote,
                await names.ResolveAsync(app.FinancialAppraisalByUserId), app.FinancialAppraisalAt);

            var printedBy = await PrintedByAsync(names);
            var bytes = _sp.GetRequiredService<ICreditAppraisalPdfGenerator>().Namp(document, printedBy.Name, DateTime.UtcNow);
            await AuditAppraisalDownloadAsync(printedBy, "NampApplication", app.Id, app.ApplicationNumber, null);
            return ApiResponse<DownloadFile>.Ok(new DownloadFile(bytes, FileName(app.ApplicationNumber)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating the credit appraisal PDF for NAMP application {Id}", nampApplicationId);
            return ApiResponse<DownloadFile>.Fail("The credit appraisal PDF could not be generated. Try again.");
        }
    }

    public async Task<ApiResponse<DownloadFile>> DownloadRhshfCreditAppraisalAsync(string reference)
    {
        try
        {
            var workspace = await GetRhshfCaseWorkspaceAsync(reference);
            if (workspace is null) return ApiResponse<DownloadFile>.Fail("Case not found.");
            var (appraisal, error) = await GetRhshfFinancialAppraisalAsync(reference);
            if (appraisal is null) return ApiResponse<DownloadFile>.Fail(error ?? "The financial appraisal could not be loaded.");
            if (appraisal.Report is null) return ApiResponse<DownloadFile>.Fail("No financial appraisal has been saved for this cycle yet.");

            var names = _sp.GetRequiredService<IUserNameResolver>();
            var stage = workspace.InternalStage is { } s ? Spaced(s.ToString()) : Spaced(workspace.Status.ToString());
            var document = new RhshfAppraisalDocument(
                new AppraisalCaseInfo("RH-SHF Dry-Season Inputs", workspace.Reference, workspace.CompanyName,
                    $"Cycle {appraisal.Report.CycleNumber}", "EOP value", workspace.TotalEopValue,
                    string.IsNullOrWhiteSpace(workspace.Currency) ? "NGN" : workspace.Currency,
                    appraisal.Report.CycleMonths, stage),
                appraisal);

            var printedBy = await PrintedByAsync(names);
            var bytes = _sp.GetRequiredService<ICreditAppraisalPdfGenerator>().Rhshf(document, printedBy.Name, DateTime.UtcNow);
            await AuditAppraisalDownloadAsync(printedBy, "RhshfCreditProfile", workspace.SubmissionId, workspace.Reference, null);
            return ApiResponse<DownloadFile>.Ok(new DownloadFile(bytes, FileName(workspace.Reference)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating the credit appraisal PDF for RH-SHF case {Reference}", reference);
            return ApiResponse<DownloadFile>.Fail("The credit appraisal PDF could not be generated. Try again.");
        }
    }

    private async Task<(Guid? Id, string Name)> PrintedByAsync(IUserNameResolver names)
    {
        var id = AuthUserId();
        return (id, id is { } u ? await names.ResolveAsync(u) : "CRMS user");
    }

    private async Task AuditAppraisalDownloadAsync((Guid? Id, string Name) user, string entityType, Guid entityId, string reference, Guid? loanApplicationId)
    {
        if (user.Id is not { } userId) return;
        try
        {
            await _sp.GetRequiredService<AuditService>().LogDataAccessAsync(
                userId, user.Name, string.Empty, SensitiveDataType.FinancialStatement, entityType, entityId,
                DataAccessType.Download, entityReference: reference, loanApplicationId: loanApplicationId,
                loanApplicationNumber: reference, accessReason: "Downloaded the credit appraisal PDF");
        }
        catch (Exception ex)
        {
            // The document is already built; a failed log line should not stop the download.
            _logger.LogWarning(ex, "Could not log the credit appraisal download for {Reference}", reference);
        }
    }

    private static string FileName(string caseNumber) =>
        $"CreditAppraisal_{Regex.Replace(caseNumber, "[^A-Za-z0-9-]", "_")}_{DateTime.UtcNow.AddHours(1):yyyyMMdd_HHmm}.pdf";

    private static string Spaced(string value) => Regex.Replace(value, "(?<=[a-z])(?=[A-Z])", " ");

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
