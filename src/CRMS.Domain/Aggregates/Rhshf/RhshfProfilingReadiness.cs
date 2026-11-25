using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Rhshf;

public enum RhshfReadinessStatus
{
    /// <summary>Nothing supplied.</summary>
    Missing,
    /// <summary>A data item is filled in (data items have no review step).</summary>
    Provided,
    /// <summary>Supplied but not usable — an expired ID.</summary>
    Invalid,
    /// <summary>A document awaiting a reviewer.</summary>
    Pending,
    Verified,
    Rejected,
}

/// <summary>Which part of the profiling form an item is filled in on, so a blocked FAC can be told
/// where to go back to.</summary>
public enum RhshfReadinessSection
{
    DirectorsAndManagement,
    Documents,
}

/// <summary>One line of the Credit Risk Readiness list: a requirement as it applies to this case, or
/// to one person on it.</summary>
public record RhshfReadinessItem(
    string Title,
    string? PersonName,
    bool IsMandatory,
    RhshfReadinessSection Section,
    RhshfReadinessStatus Status,
    string? Note = null,
    RhshfDocumentCategory? Category = null,
    Guid? DirectorId = null,
    Guid? ManagementStaffId = null,
    Guid? DocumentId = null)
{
    public bool IsDocument => Category.HasValue;

    /// <summary>Blocks the FAC's submission: a mandatory item missing, unusable, or a document the
    /// reviewer rejected and the FAC has not yet replaced.</summary>
    public bool BlocksSubmission => IsMandatory && Status is RhshfReadinessStatus.Missing
        or RhshfReadinessStatus.Invalid or RhshfReadinessStatus.Rejected;

    public string Label => PersonName is null ? Title : $"{Title} – {PersonName}";
}

/// <summary>
/// Works out, for one case, every profiling requirement BOA asks for and where it stands. The single
/// source for both the FAC's submit gate (<see cref="RhshfCreditProfile.AdvanceStage"/>) and the
/// staff Credit Risk Readiness panel, so the two cannot disagree about what is outstanding.
///
/// Requirements come from admin-configured rows (<see cref="RhshfDocumentRequirement"/>,
/// <see cref="RhshfProfilingFieldRequirement"/>); inactive document rows are skipped, and an optional
/// item is still listed so the reviewer sees it, but never blocks.
/// </summary>
public static class RhshfProfilingReadiness
{
    public static IReadOnlyList<RhshfReadinessItem> Evaluate(
        RhshfCreditProfile profile,
        IEnumerable<RhshfDocumentRequirement>? documentRequirements,
        IEnumerable<RhshfProfilingFieldRequirement>? fieldRequirements,
        DateTime asOf)
    {
        var items = new List<RhshfReadinessItem>();
        if (fieldRequirements is not null)
            items.AddRange(EvaluateFields(profile, fieldRequirements, asOf));
        if (documentRequirements is not null)
            items.AddRange(EvaluateDocuments(profile, documentRequirements));
        return items;
    }

    public static IEnumerable<RhshfReadinessItem> EvaluateFields(
        RhshfCreditProfile profile, IEnumerable<RhshfProfilingFieldRequirement> requirements, DateTime asOf)
    {
        const RhshfReadinessSection section = RhshfReadinessSection.DirectorsAndManagement;

        foreach (var r in requirements.OrderBy(r => r.SortOrder))
        {
            switch (r.Key)
            {
                case RhshfProfilingFieldKey.DirectorNin:
                    foreach (var d in profile.Directors)
                        yield return new(r.Title, d.FullName, r.IsMandatory, section,
                            Has(d.Nin), DirectorId: d.Id);
                    break;

                case RhshfProfilingFieldKey.DirectorPhone:
                    foreach (var d in profile.Directors)
                        yield return new(r.Title, d.FullName, r.IsMandatory, section,
                            Has(d.PhoneNumber), DirectorId: d.Id);
                    break;

                case RhshfProfilingFieldKey.DirectorMeansOfId:
                    foreach (var d in profile.Directors)
                    {
                        if (!d.HasMeansOfId)
                            yield return new(r.Title, d.FullName, r.IsMandatory, section,
                                RhshfReadinessStatus.Missing, DirectorId: d.Id);
                        else if (d.IsMeansOfIdExpired(asOf))
                            yield return new(r.Title, d.FullName, r.IsMandatory, section,
                                RhshfReadinessStatus.Invalid,
                                $"Expired on {d.MeansOfIdExpiry:dd/MM/yyyy}", DirectorId: d.Id);
                        else
                            yield return new(r.Title, d.FullName, r.IsMandatory, section,
                                RhshfReadinessStatus.Provided, DirectorId: d.Id);
                    }
                    break;

                case RhshfProfilingFieldKey.ManagementStaffRequired:
                    yield return new(r.Title, null, r.IsMandatory, section,
                        profile.ManagementStaff.Count > 0 ? RhshfReadinessStatus.Provided : RhshfReadinessStatus.Missing);
                    break;

                case RhshfProfilingFieldKey.ManagementPhone:
                    foreach (var m in profile.ManagementStaff)
                        yield return new(r.Title, m.FullName, r.IsMandatory, section,
                            Has(m.PhoneNumber), ManagementStaffId: m.Id);
                    break;

                case RhshfProfilingFieldKey.ChiefPromoterDesignated:
                    var promoter = profile.ChiefPromoterName;
                    yield return new(r.Title, null, r.IsMandatory, section,
                        promoter is null ? RhshfReadinessStatus.Missing : RhshfReadinessStatus.Provided,
                        promoter);
                    break;
            }
        }
    }

    public static IEnumerable<RhshfReadinessItem> EvaluateDocuments(
        RhshfCreditProfile profile, IEnumerable<RhshfDocumentRequirement> requirements)
    {
        const RhshfReadinessSection section = RhshfReadinessSection.Documents;

        foreach (var r in requirements.Where(r => r.IsActive && r.Category != RhshfDocumentCategory.Other)
                     .OrderBy(r => r.SortOrder))
        {
            switch (r.AppliesTo)
            {
                case RhshfRequirementAppliesTo.Case:
                    yield return ForDocument(r.Title, null, r.IsMandatory, r.Category,
                        Find(profile, r.Category, null, null), null, null);
                    break;

                case RhshfRequirementAppliesTo.EachDirector:
                    foreach (var d in profile.Directors)
                        yield return ForDocument(r.Title, d.FullName, r.IsMandatory, r.Category,
                            Find(profile, r.Category, d.Id, null), d.Id, null);
                    break;

                case RhshfRequirementAppliesTo.EachManagementStaff:
                    foreach (var m in profile.ManagementStaff)
                        yield return ForDocument(r.Title, m.FullName, r.IsMandatory, r.Category,
                            Find(profile, r.Category, null, m.Id), null, m.Id);
                    break;

                case RhshfRequirementAppliesTo.ChiefPromoter:
                    // Satisfied by the promoter's own CV, uploaded on their row. The item keeps this
                    // requirement's category, so it can be matched back to it; the file is a DirectorCv
                    // or ManagementCv.
                    var director = profile.Directors.FirstOrDefault(d => d.IsChiefPromoter);
                    var manager = profile.ManagementStaff.FirstOrDefault(m => m.IsChiefPromoter);
                    if (director is not null)
                        yield return ForDocument(r.Title, director.FullName, r.IsMandatory, r.Category,
                            Find(profile, RhshfDocumentCategory.DirectorCv, director.Id, null), director.Id, null);
                    else if (manager is not null)
                        yield return ForDocument(r.Title, manager.FullName, r.IsMandatory, r.Category,
                            Find(profile, RhshfDocumentCategory.ManagementCv, null, manager.Id), null, manager.Id);
                    else
                        yield return new(r.Title, null, r.IsMandatory, section, RhshfReadinessStatus.Missing,
                            "Name a Chief Promoter among the directors or management staff first.",
                            Category: r.Category);
                    break;
            }
        }
    }

    /// <summary>
    /// Why a Credit Officer may not yet proceed: a mandatory document still awaiting review, or any
    /// document the officer rejected. A mandatory slot with nothing in it does not block here, on
    /// purpose — cases submitted before a requirement existed must not be stranded at appraisal;
    /// the FAC's submit gate is what makes a new case supply it.
    /// </summary>
    public static IReadOnlyList<string> AppraisalBlockers(
        RhshfCreditProfile profile, IEnumerable<RhshfDocumentRequirement>? documentRequirements)
    {
        var blockers = new List<string>();

        if (documentRequirements is not null)
        {
            blockers.AddRange(EvaluateDocuments(profile, documentRequirements)
                .Where(i => i.IsMandatory && i.Status == RhshfReadinessStatus.Pending)
                .Select(i => $"{i.Label} is awaiting verification"));
        }

        blockers.AddRange(profile.SupportingDocuments
            .Where(d => d.VerificationStatus == RhshfDocumentVerificationStatus.Rejected)
            .Select(d => $"{d.FileName} was rejected"));

        return blockers.Distinct().ToList();
    }

    private static RhshfReadinessStatus Has(string? value) =>
        string.IsNullOrWhiteSpace(value) ? RhshfReadinessStatus.Missing : RhshfReadinessStatus.Provided;

    private static RhshfSupportingDocument? Find(
        RhshfCreditProfile profile, RhshfDocumentCategory category, Guid? directorId, Guid? managementStaffId) =>
        profile.SupportingDocuments
            .Where(d => d.FillsSlot(category, directorId, managementStaffId))
            .OrderByDescending(d => d.UploadedAt)
            .FirstOrDefault();

    private static RhshfReadinessItem ForDocument(
        string title, string? person, bool mandatory, RhshfDocumentCategory category,
        RhshfSupportingDocument? doc, Guid? directorId, Guid? managementStaffId)
    {
        var status = doc?.VerificationStatus switch
        {
            null => RhshfReadinessStatus.Missing,
            RhshfDocumentVerificationStatus.Verified => RhshfReadinessStatus.Verified,
            RhshfDocumentVerificationStatus.Rejected => RhshfReadinessStatus.Rejected,
            _ => RhshfReadinessStatus.Pending,
        };
        return new(title, person, mandatory, RhshfReadinessSection.Documents, status,
            doc?.RejectionReason, category, directorId, managementStaffId, doc?.Id);
    }
}
