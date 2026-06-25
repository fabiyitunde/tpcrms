using CRMS.Domain.Common;
using CRMS.Domain.Enums;

namespace CRMS.Domain.Aggregates.Namp;

public class NampDocumentTemplate : AggregateRoot
{
    public NampDocumentType DocumentType { get; private set; }
    public string Title { get; private set; } = string.Empty;

    // For OfferLetter: intro paragraphs before the generated tables.
    // For LeaseAgreement / GpsConsentForm: full body content.
    public string BodyContent { get; private set; } = string.Empty;

    // For OfferLetter only: general conditions + acceptance text rendered after the tables.
    public string? ConditionsContent { get; private set; }

    public int Version { get; private set; }
    public bool IsActive { get; private set; }
    public Guid LastModifiedByUserId { get; private set; }
    public DateTime? LastModifiedAt { get; private set; }

    private NampDocumentTemplate() { }

    public static NampDocumentTemplate Create(
        NampDocumentType documentType,
        string title,
        string bodyContent,
        Guid createdByUserId,
        string? conditionsContent = null)
    {
        return new NampDocumentTemplate
        {
            DocumentType = documentType,
            Title = title.Trim(),
            BodyContent = bodyContent,
            ConditionsContent = conditionsContent,
            Version = 1,
            IsActive = true,
            LastModifiedByUserId = createdByUserId,
            LastModifiedAt = DateTime.UtcNow,
        };
    }

    public Result Update(
        string title,
        string bodyContent,
        Guid modifiedByUserId,
        string? conditionsContent = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Result.Failure("Title is required.");
        if (string.IsNullOrWhiteSpace(bodyContent))
            return Result.Failure("Body content is required.");

        Title = title.Trim();
        BodyContent = bodyContent;
        ConditionsContent = conditionsContent;
        LastModifiedByUserId = modifiedByUserId;
        LastModifiedAt = DateTime.UtcNow;
        Version++;
        return Result.Success();
    }

    public string RenderBody(Dictionary<string, string> variables) =>
        ReplaceTokens(BodyContent, variables);

    public string? RenderConditions(Dictionary<string, string> variables) =>
        ConditionsContent != null ? ReplaceTokens(ConditionsContent, variables) : null;

    private static string ReplaceTokens(string template, Dictionary<string, string> variables)
    {
        var result = template;
        foreach (var kvp in variables)
            result = result.Replace($"{{{{{kvp.Key}}}}}", kvp.Value);
        return result;
    }
}
