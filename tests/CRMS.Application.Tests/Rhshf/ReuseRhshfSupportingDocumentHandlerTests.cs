using CRMS.Application.Rhshf.Commands;
using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>
/// Carry-over of a document the FAC already supplied on an earlier case — added for the Credit
/// Appraisal Memorandum, which a FAC prepares once and should not have to re-upload every season.
///
/// The cases that matter are the refusals: the posted document id is attacker-controlled, so the
/// handler must prove the source belongs to this FAC's own cases before copying anything, and the
/// copy must be an independent blob rather than a second row pointing at the source's file.
/// </summary>
public class ReuseRhshfSupportingDocumentHandlerTests
{
    private const RhshfDocumentCategory Cam = RhshfDocumentCategory.CreditAppraisalMemorandum;

    [Fact]
    public async Task CarriesTheDocumentOver_WithProvenanceRecorded()
    {
        var facId = Guid.NewGuid();
        var target = AtDocumentsStage(facId);
        var source = SourceDocument(facId, "CAM-2025.pdf");

        var result = await Handler(target, source).Handle(
            new ReuseRhshfSupportingDocumentCommand(target.Reference, source.Document.DocumentId));

        Assert.True(result.IsSuccess);
        var attached = Assert.Single(target.SupportingDocuments);
        Assert.Equal("CAM-2025.pdf", attached.FileName);
        Assert.Equal(Cam, attached.Category);
        Assert.True(attached.IsReused);
        Assert.Equal(source.Document.SourceReference, attached.ReusedFromReference);
        Assert.Equal(source.Document.UploadedAt, attached.OriginalUploadedAt);
    }

    [Fact]
    public async Task CopiesTheBytesToThisCasesOwnPath_RatherThanSharingTheSourceBlob()
    {
        // Two cases pointing at one blob would mean removing the document from either case breaks
        // the other, and the source case is an audited record that must not be reachable for
        // deletion through this one.
        var facId = Guid.NewGuid();
        var target = AtDocumentsStage(facId);
        var source = SourceDocument(facId, "CAM-2025.pdf");

        await Handler(target, source).Handle(
            new ReuseRhshfSupportingDocumentCommand(target.Reference, source.Document.DocumentId));

        var attached = Assert.Single(target.SupportingDocuments);
        Assert.NotEqual(source.Document.StoragePath, attached.StoragePath);
        Assert.Contains(target.Reference, attached.StoragePath);
    }

    [Fact]
    public async Task Refuses_WhenTheDocumentBelongsToAnotherApplicant()
    {
        // The id is posted from the browser. The candidate list is scoped to this FAC's own cases,
        // and re-deriving it is what stops a crafted request pulling another aggregator's CAM —
        // along with whatever commercially sensitive content it holds — into this case.
        var target = AtDocumentsStage(Guid.NewGuid());
        var someoneElse = SourceDocument(Guid.NewGuid(), "their-CAM.pdf");

        var result = await Handler(target, someoneElse, candidateIsVisible: false).Handle(
            new ReuseRhshfSupportingDocumentCommand(target.Reference, someoneElse.Document.DocumentId));

        Assert.False(result.IsSuccess);
        Assert.Contains("does not belong", result.Error!);
        Assert.Empty(target.SupportingDocuments);
    }

    [Fact]
    public async Task Refuses_WhenTheCategoryAlreadyHasADocumentOnThisCase()
    {
        var facId = Guid.NewGuid();
        var target = AtDocumentsStage(facId);
        target.AddSupportingDocument(Cam, "CAM-2026.pdf", "application/pdf", "p/cam-2026.pdf", 2048);
        var source = SourceDocument(facId, "CAM-2025.pdf");

        var result = await Handler(target, source).Handle(
            new ReuseRhshfSupportingDocumentCommand(target.Reference, source.Document.DocumentId));

        Assert.False(result.IsSuccess);
        var kept = Assert.Single(target.SupportingDocuments);
        Assert.Equal("CAM-2026.pdf", kept.FileName);
    }

    [Fact]
    public async Task Refuses_WhenTheCaseHasMovedPastTheDocumentsStage()
    {
        var facId = Guid.NewGuid();
        var target = AtDocumentsStage(facId);
        target.AdvanceStage(RhshfProfilingStage.SupportingDocuments);
        var source = SourceDocument(facId, "CAM-2025.pdf");

        var result = await Handler(target, source).Handle(
            new ReuseRhshfSupportingDocumentCommand(target.Reference, source.Document.DocumentId));

        Assert.False(result.IsSuccess);
        Assert.Empty(target.SupportingDocuments);
    }

    [Fact]
    public async Task Refuses_WhenTheStoredFileCannotBeRead()
    {
        // The row outlives its blob (a storage migration, a manual delete). The FAC can still
        // upload a fresh copy, so this is a message rather than an unhandled throw.
        var facId = Guid.NewGuid();
        var target = AtDocumentsStage(facId);
        var source = SourceDocument(facId, "CAM-2025.pdf");

        var result = await Handler(target, source, blobReadable: false).Handle(
            new ReuseRhshfSupportingDocumentCommand(target.Reference, source.Document.DocumentId));

        Assert.False(result.IsSuccess);
        Assert.Contains("Upload it again", result.Error!);
        Assert.Empty(target.SupportingDocuments);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────────────────────────

    private static RhshfCreditProfile AtDocumentsStage(Guid facId)
    {
        var profile = RhshfCreditProfile.Create(
            submissionId: Guid.NewGuid(), programmeCode: "RH-SHF-DRY-2026", programmeName: "Renewed Hope",
            sessionCode: "2026-DRY", sessionName: "Dry Season 2026", facId: facId,
            companyName: "Alliedsoft Limited", rcNumber: "RC123456", tin: "01234567-0001",
            boaAccountNumber: "0123456789", contactEmail: "fac@company.com", contactPhone: "+2348012345678",
            state: "Kano", lga: "Nassarawa", totalEopValue: 51_500_000.00m, currency: "NGN", farmerCount: 1200,
            callbackUrl: "https://portal.example.gov.ng/api/integrations/crms/webhook",
            certifiedByAdmin: "admin@boa.gov.ng", certifiedAt: DateTime.UtcNow, rawSubmissionPayload: "{}",
            eopLines: null, resolvedBranchId: null, resolvedOfficeId: null).Value;

        profile.AdvanceStage(RhshfProfilingStage.CompanyVerification);
        profile.AdvanceStage(RhshfProfilingStage.CreditBureauCheck);
        profile.AdvanceStage(RhshfProfilingStage.EopReview);
        return profile;
    }

    /// <summary>A CAM on one of the FAC's earlier cases, as the candidate query would return it,
    /// paired with the entity the id lookup resolves to.</summary>
    private static (RhshfReusableDocument Document, RhshfSupportingDocument Entity) SourceDocument(
        Guid facId, string fileName)
    {
        var sourceProfileId = Guid.NewGuid();
        var entity = new RhshfSupportingDocument(
            sourceProfileId, Cam, fileName, "application/pdf", $"rhshf-documents/RHSHF-2025-0001/{fileName}", 4096);

        var candidate = new RhshfReusableDocument(
            entity.Id, Cam, fileName, "application/pdf", entity.StoragePath, 4096,
            DateTime.UtcNow.AddMonths(-9), "RHSHF-2025-0001", "Dry Season 2025", RhshfCaseStatus.Approved);

        _ = facId; // the FAC link lives in the fake repository's scoping, not on the document row
        return (candidate, entity);
    }

    private static ReuseRhshfSupportingDocumentHandler Handler(
        RhshfCreditProfile target,
        (RhshfReusableDocument Document, RhshfSupportingDocument Entity) source,
        bool candidateIsVisible = true,
        bool blobReadable = true)
        => new(
            new FakeProfileRepository(target, source, candidateIsVisible),
            new FakeFileStorage(blobReadable),
            new FakeUnitOfWork());

    private class FakeProfileRepository : IRhshfCreditProfileRepository
    {
        private readonly RhshfCreditProfile _target;
        private readonly (RhshfReusableDocument Document, RhshfSupportingDocument Entity) _source;
        private readonly bool _candidateIsVisible;

        public FakeProfileRepository(
            RhshfCreditProfile target,
            (RhshfReusableDocument Document, RhshfSupportingDocument Entity) source,
            bool candidateIsVisible)
        {
            _target = target;
            _source = source;
            _candidateIsVisible = candidateIsVisible;
        }

        public Task<RhshfCreditProfile?> GetByReferenceAsync(string reference, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(_target.Reference == reference ? _target : null);

        public Task<RhshfSupportingDocument?> GetSupportingDocumentByIdAsync(Guid documentId, CancellationToken ct = default)
            => Task.FromResult<RhshfSupportingDocument?>(_source.Entity.Id == documentId ? _source.Entity : null);

        // Stands in for the FacId-scoped query: an invisible candidate is exactly what another
        // applicant's document looks like from here.
        public Task<IReadOnlyList<RhshfReusableDocument>> GetReusableDocumentsForFacAsync(
            Guid facId, Guid excludeProfileId, RhshfDocumentCategory category, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfReusableDocument>>(
                _candidateIsVisible && _source.Document.Category == category ? [_source.Document] : []);

        public Task<RhshfCreditProfile?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task<RhshfCreditProfile?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
            => Task.FromResult<RhshfCreditProfile?>(null);
        public Task AddAsync(RhshfCreditProfile profile, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<RhshfCreditProfile>> GetQueueAsync(RhshfInternalStage stage, Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
        public Task<IReadOnlyList<RhshfCreditProfile>> GetAllForListAsync(Guid? branchId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<RhshfCreditProfile>>([]);
    }

    private class FakeFileStorage : IFileStorageService
    {
        private readonly bool _readable;
        public FakeFileStorage(bool readable) => _readable = readable;

        public Task<byte[]> DownloadAsync(string storagePath, CancellationToken ct = default)
            => _readable
                ? Task.FromResult(new byte[4096])
                : throw new FileNotFoundException($"No blob at {storagePath}");

        public Task<string> UploadAsync(string containerName, string fileName, byte[] content, string contentType, CancellationToken ct = default)
            => Task.FromResult($"{containerName}/{fileName}");

        public Task<string> UploadAsync(string containerName, string fileName, Stream content, string contentType, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Stream> GetStreamAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<bool> ExistsAsync(string storagePath, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<string?> GetPresignedUrlAsync(string storagePath, TimeSpan expiry, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IEnumerable<string>> ListFilesAsync(string containerName, string? prefix = null, CancellationToken ct = default) => throw new NotSupportedException();
    }

    private class FakeUnitOfWork : IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken ct = default) => Task.FromResult(1);
    }
}
