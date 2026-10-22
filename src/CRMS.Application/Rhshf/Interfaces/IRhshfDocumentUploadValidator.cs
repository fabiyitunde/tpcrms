using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Common;

namespace CRMS.Application.Rhshf.Interfaces;

/// <summary>
/// Decides whether an uploaded file is the kind of document the slot asked for.
/// <para>
/// Uploads used to be checked for nothing but "not empty" and "under 10 MB", which is how a PDF came
/// to be filed as audited financials, spreadsheets as bank statements, and a plain text file as
/// supporting evidence. None of that surfaces until a credit officer opens the case, by which point
/// the FAC has moved on and the case has to be returned.
/// </para>
/// </summary>
public interface IRhshfDocumentUploadValidator
{
    /// <summary>
    /// Checks a file against its requirement. A null requirement, or one with no configured
    /// formats, restricts nothing beyond the universal size floor — an unconfigured slot must keep
    /// behaving the way it did before these rules existed.
    /// </summary>
    /// <param name="requirement">The slot being filled, where one is configured.</param>
    /// <param name="fileName">Used only to name the file in messages; never trusted as evidence of type.</param>
    /// <param name="content">The actual bytes, which are what the decision is made on.</param>
    Result Validate(RhshfDocumentRequirement? requirement, string fileName, byte[] content);
}
