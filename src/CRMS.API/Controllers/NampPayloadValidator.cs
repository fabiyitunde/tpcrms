using CRMS.Domain.Enums;

namespace CRMS.API.Controllers;

/// <summary>
/// Validates an inbound NAMP webhook payload against the fields required
/// throughout the full CRMS processing flow (recall → bureau → appraisal → offer → deployment).
/// Returns a flat list of human-readable errors. Empty list = valid.
/// </summary>
internal static class NampPayloadValidator
{
    public static List<string> Validate(NampWebhookPayload payload, NampApplicantCategory category)
    {
        var errors = new List<string>();

        ValidateCommon(payload, errors);
        ValidateEquipmentCart(payload, errors);
        ValidateGuarantors(payload, errors);

        if (category == NampApplicantCategory.AgroServiceCompany)
            ValidateCompany(payload, errors);
        else
            ValidateIndividual(payload, errors);

        return errors;
    }

    // ── Group A: Common (all categories) ─────────────────────────────────────

    private static void ValidateCommon(NampWebhookPayload payload, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(payload.FullName))
            errors.Add("FullName is required.");

        if (payload.LoanAmount <= 0)
            errors.Add("LoanAmount must be greater than zero.");

        if (payload.CartTotal <= 0)
            errors.Add("CartTotal must be greater than zero.");

        if (payload.EquityPercent < 0)
            errors.Add("EquityPercent must be zero or greater.");

        if (payload.EquityAmount < 0)
            errors.Add("EquityAmount must be zero or greater.");

        if (string.IsNullOrWhiteSpace(payload.LoanPurpose))
            errors.Add("LoanPurpose is required (used during financial appraisal).");
    }

    // ── Group B: Equipment cart ───────────────────────────────────────────────

    private static void ValidateEquipmentCart(NampWebhookPayload payload, List<string> errors)
    {
        if (payload.EquipmentCart is null || payload.EquipmentCart.Count == 0)
        {
            errors.Add("EquipmentCart must contain at least one item.");
            return;
        }

        for (int i = 0; i < payload.EquipmentCart.Count; i++)
        {
            var item = payload.EquipmentCart[i];
            var label = $"EquipmentCart[{i}]";

            if (string.IsNullOrWhiteSpace(item.EquipmentName))
                errors.Add($"{label}.EquipmentName is required.");

            if (item.LineTotal <= 0)
                errors.Add($"{label}.LineTotal must be greater than zero.");

            if (item.Quantity <= 0)
                errors.Add($"{label}.Quantity must be greater than zero.");
        }
    }

    // ── Group C: Guarantors ───────────────────────────────────────────────────

    private static void ValidateGuarantors(NampWebhookPayload payload, List<string> errors)
    {
        if (payload.Guarantors is null || payload.Guarantors.Count == 0)
        {
            errors.Add("At least one guarantor is required.");
            return;
        }

        for (int i = 0; i < payload.Guarantors.Count; i++)
        {
            var g = payload.Guarantors[i];
            var label = $"Guarantors[{i}]";

            if (string.IsNullOrWhiteSpace(g.FullName))
                errors.Add($"{label}.FullName is required.");

            if (string.IsNullOrWhiteSpace(g.GuarantorType))
                errors.Add($"{label}.GuarantorType is required.");

            if (string.IsNullOrWhiteSpace(g.GuaranteeType))
                errors.Add($"{label}.GuaranteeType is required.");

            if (string.IsNullOrWhiteSpace(g.Bvn))
                errors.Add($"{label}.Bvn is required (needed for guarantor credit bureau check).");
        }
    }

    // ── Group D: Individual applicants (Youth + Women) ────────────────────────

    private static void ValidateIndividual(NampWebhookPayload payload, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(payload.BankVerificationNumber))
            errors.Add("BankVerificationNumber (BVN) is required for individual applicants (needed for personal credit bureau check).");

        if (string.IsNullOrWhiteSpace(payload.NationalIdNumber))
            errors.Add("NationalIdNumber (NIN) is required for individual applicants.");

        if (string.IsNullOrWhiteSpace(payload.DateOfBirth))
            errors.Add("DateOfBirth is required for individual applicants.");

        if (string.IsNullOrWhiteSpace(payload.StateOfResidence))
            errors.Add("StateOfResidence is required for individual applicants.");

        if (payload.EstimatedMonthlyIncome is null or <= 0)
            errors.Add("EstimatedMonthlyIncome is required for individual applicants (used in financial appraisal).");
    }

    // ── Group E: Company applicants (AgroServiceCompany) ─────────────────────

    private static void ValidateCompany(NampWebhookPayload payload, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(payload.CompanyName))
            errors.Add("CompanyName is required for AgroServiceCompany applicants.");

        if (string.IsNullOrWhiteSpace(payload.RcNumber))
            errors.Add("RcNumber is required for AgroServiceCompany applicants (needed for business credit bureau check).");

        if (string.IsNullOrWhiteSpace(payload.BankVerificationNumber))
            errors.Add("BankVerificationNumber (BVN) is required for AgroServiceCompany applicants (principal officer BVN for bureau checks).");
    }
}
