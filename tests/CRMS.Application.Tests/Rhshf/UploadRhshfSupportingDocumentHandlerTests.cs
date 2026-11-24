using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>BOA allows the farmers register 20 MB, because it lists every smallholder with
/// biometric evidence. Every other profiling document stays at 10 MB.</summary>
public class UploadRhshfSupportingDocumentHandlerTests
{
    private const int MB = 1024 * 1024;

    // Only the profile lookup is reached in these tests; it finds nothing, so a file that gets past
    // the size check stops at "Case not found." and never reaches storage.
    private static UploadRhshfSupportingDocumentHandler Handler() =>
        new(new NoProfiles(), null!, null!, null!, null!);

    private static UploadRhshfSupportingDocumentCommand Upload(RhshfDocumentCategory category, int bytes) =>
        new("RHSHF-TEST", category, "file.pdf", "application/pdf", new byte[bytes]);

    [Fact]
    public async Task A_15_MB_farmers_register_passes_the_size_check()
    {
        var result = await Handler().Handle(Upload(RhshfDocumentCategory.FarmerRegister, 15 * MB));

        Assert.Equal("Case not found.", result.Error);
    }

    [Fact]
    public async Task A_farmers_register_over_20_MB_is_refused()
    {
        var result = await Handler().Handle(Upload(RhshfDocumentCategory.FarmerRegister, 20 * MB + 1));

        Assert.Equal("File exceeds the 20 MB size limit.", result.Error);
    }

    [Fact]
    public async Task Any_other_document_over_10_MB_is_refused()
    {
        var result = await Handler().Handle(Upload(RhshfDocumentCategory.BankStatement, 15 * MB));

        Assert.Equal("File exceeds the 10 MB size limit.", result.Error);
    }

    [Theory]
    [InlineData(RhshfDocumentCategory.FarmerRegister, "20 MB")]
    [InlineData(RhshfDocumentCategory.OffTakeAgreement, "10 MB")]
    public void The_form_shows_the_slots_limit(RhshfDocumentCategory category, string expected) =>
        Assert.Equal(expected, UploadRhshfSupportingDocumentHandler.MaxSizeText(category));

    private class NoProfiles : IRhshfCreditProfileRepository
    {
        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default) => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task AddAsync(RhshfCreditProfile p, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
