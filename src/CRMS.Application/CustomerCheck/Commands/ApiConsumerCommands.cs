using CRMS.Application.Common;
using CRMS.Application.CustomerCheck.DTOs;
using CRMS.Domain.Aggregates.CustomerCheck;
using CRMS.Domain.Interfaces;

namespace CRMS.Application.CustomerCheck.Commands;

public static class ApiConsumerMapping
{
    public static ApiConsumerDto ToDto(ApiConsumer c, ApiConsumerSetupLinkDto? latestSetupLink = null) => new(
        c.Id, c.Name, c.Notes, c.KeyPrefix, c.IsActive, c.AllowedSectionList, c.PerMinuteLimit,
        c.ReuseBureauReports, c.BureauReuseMaxAgeDays, c.LastUsedAt, c.KeyRotatedAt, c.CreatedAt,
        c.CallbackUrl, c.AllowedCallbackHostList, c.MaxQueuedJobs, c.CallbackSecretPrefix, c.CallbackSecretRotatedAt,
        c.ContactEmail, latestSetupLink);

    /// <summary>Section names as given, canonicalised; unknown names are an error.</summary>
    public static (List<string> Sections, string? Error) Sections(IEnumerable<string>? given)
    {
        var list = new List<string>();
        foreach (var g in given ?? [])
        {
            var canonical = CustomerCheckSections.Canonical(g);
            if (canonical is null)
                return ([], $"'{g}' is not a known section.");
            if (!list.Contains(canonical)) list.Add(canonical);
        }
        return list.Count == 0 ? ([], "At least one section must be allowed.") : (list, null);
    }
}

/// <summary>Callback registration shared by create and update.</summary>
public record ApiConsumerCallbackSettings(string? CallbackUrl, List<string> AllowedCallbackHosts, int MaxQueuedJobs)
{
    public static readonly ApiConsumerCallbackSettings None = new(null, [], 100);
}

public record CreateApiConsumerCommand(
    string Name,
    string? Notes,
    List<string> AllowedSections,
    int PerMinuteLimit,
    bool ReuseBureauReports,
    int BureauReuseMaxAgeDays,
    Guid CreatedByUserId,
    ApiConsumerCallbackSettings? Callback = null,
    string? ContactEmail = null) : IRequest<ApplicationResult<ApiConsumerKeyIssuedDto>>;

public class CreateApiConsumerHandler : IRequestHandler<CreateApiConsumerCommand, ApplicationResult<ApiConsumerKeyIssuedDto>>
{
    private readonly IApiConsumerRepository _repository;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IUnitOfWork _unitOfWork;

    public CreateApiConsumerHandler(IApiConsumerRepository repository, ICustomerCheckCipher cipher, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _cipher = cipher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ApiConsumerKeyIssuedDto>> Handle(CreateApiConsumerCommand request, CancellationToken ct = default)
    {
        var (sections, sectionError) = ApiConsumerMapping.Sections(request.AllowedSections);
        if (sectionError is not null)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure(sectionError);

        if (await _repository.GetByNameAsync(request.Name.Trim(), ct) is not null)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure($"A consumer named '{request.Name.Trim()}' already exists.");

        var (key, hash, prefix) = ApiKeys.Generate();
        var created = ApiConsumer.Create(
            request.Name, hash, prefix, sections, request.PerMinuteLimit,
            request.ReuseBureauReports, request.BureauReuseMaxAgeDays, request.Notes, request.CreatedByUserId);
        if (created.IsFailure)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure(created.Error);

        var consumer = created.Value;
        // Required from the start: it is where the credentials are delivered.
        var contactSet = consumer.SetContactEmail(request.ContactEmail, request.CreatedByUserId);
        if (contactSet.IsFailure)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure(contactSet.Error);

        var callback = request.Callback ?? ApiConsumerCallbackSettings.None;
        var callbackSet = consumer.SetCallback(callback.CallbackUrl, callback.AllowedCallbackHosts, callback.MaxQueuedJobs, request.CreatedByUserId);
        if (callbackSet.IsFailure)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure(callbackSet.Error);

        // Every consumer gets its own signing secret from the start, so callbacks can be verified
        // from the first one, whether or not a URL is registered today.
        var (secret, secretPrefix) = ApiKeys.GenerateSecret();
        consumer.SetCallbackSecret(_cipher.Protect(secret), secretPrefix, request.CreatedByUserId);

        await _repository.AddAsync(consumer, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult<ApiConsumerKeyIssuedDto>.Success(new ApiConsumerKeyIssuedDto(ApiConsumerMapping.ToDto(consumer), key, secret));
    }
}

public record UpdateApiConsumerCommand(
    Guid Id,
    string Name,
    string? Notes,
    List<string> AllowedSections,
    int PerMinuteLimit,
    bool ReuseBureauReports,
    int BureauReuseMaxAgeDays,
    Guid ModifiedByUserId,
    ApiConsumerCallbackSettings? Callback = null,
    string? ContactEmail = null) : IRequest<ApplicationResult>;

public class UpdateApiConsumerHandler : IRequestHandler<UpdateApiConsumerCommand, ApplicationResult>
{
    private readonly IApiConsumerRepository _repository;
    private readonly IApiConsumerSetupLinkRepository _links;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateApiConsumerHandler(IApiConsumerRepository repository, IApiConsumerSetupLinkRepository links, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _links = links;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(UpdateApiConsumerCommand request, CancellationToken ct = default)
    {
        var consumer = await _repository.GetByIdAsync(request.Id, ct);
        if (consumer is null)
            return ApplicationResult.Failure("Consumer not found.");

        var (sections, sectionError) = ApiConsumerMapping.Sections(request.AllowedSections);
        if (sectionError is not null)
            return ApplicationResult.Failure(sectionError);

        var other = await _repository.GetByNameAsync(request.Name.Trim(), ct);
        if (other is not null && other.Id != consumer.Id)
            return ApplicationResult.Failure($"A consumer named '{request.Name.Trim()}' already exists.");

        var updated = consumer.Update(
            request.Name, sections, request.PerMinuteLimit, request.ReuseBureauReports,
            request.BureauReuseMaxAgeDays, request.Notes, request.ModifiedByUserId);
        if (updated.IsFailure)
            return ApplicationResult.Failure(updated.Error);

        // Null leaves the address as it is; anything else must be a usable address.
        var contactChanged = false;
        if (request.ContactEmail is not null)
        {
            contactChanged = !string.Equals(consumer.ContactEmail, request.ContactEmail.Trim(), StringComparison.OrdinalIgnoreCase);
            var contactSet = consumer.SetContactEmail(request.ContactEmail, request.ModifiedByUserId);
            if (contactSet.IsFailure)
                return ApplicationResult.Failure(contactSet.Error);
        }

        if (request.Callback is { } callback)
        {
            var callbackSet = consumer.SetCallback(callback.CallbackUrl, callback.AllowedCallbackHosts, callback.MaxQueuedJobs, request.ModifiedByUserId);
            if (callbackSet.IsFailure)
                return ApplicationResult.Failure(callbackSet.Error);
        }

        _repository.Update(consumer);
        await _unitOfWork.SaveChangesAsync(ct);
        // A link already sitting in the old mailbox must not outlive the change of address.
        if (contactChanged)
            await _links.RevokeOpenAsync(consumer.Id, DateTime.UtcNow, ct);
        return ApplicationResult.Success();
    }
}

public record SetApiConsumerActiveCommand(Guid Id, bool IsActive, Guid ModifiedByUserId) : IRequest<ApplicationResult>;

public class SetApiConsumerActiveHandler : IRequestHandler<SetApiConsumerActiveCommand, ApplicationResult>
{
    private readonly IApiConsumerRepository _repository;
    private readonly IApiConsumerSetupLinkRepository _links;
    private readonly IUnitOfWork _unitOfWork;

    public SetApiConsumerActiveHandler(IApiConsumerRepository repository, IApiConsumerSetupLinkRepository links, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _links = links;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult> Handle(SetApiConsumerActiveCommand request, CancellationToken ct = default)
    {
        var consumer = await _repository.GetByIdAsync(request.Id, ct);
        if (consumer is null)
            return ApplicationResult.Failure("Consumer not found.");

        consumer.SetActive(request.IsActive, request.ModifiedByUserId);
        _repository.Update(consumer);
        await _unitOfWork.SaveChangesAsync(ct);
        // Switching a consumer off also cancels a setup link that has not been opened yet, so
        // switching it back on later does not bring an old email back to life.
        if (!request.IsActive)
            await _links.RevokeOpenAsync(consumer.Id, DateTime.UtcNow, ct);
        return ApplicationResult.Success();
    }
}

public record RotateApiConsumerKeyCommand(Guid Id, Guid ModifiedByUserId) : IRequest<ApplicationResult<ApiConsumerKeyIssuedDto>>;

public class RotateApiConsumerKeyHandler : IRequestHandler<RotateApiConsumerKeyCommand, ApplicationResult<ApiConsumerKeyIssuedDto>>
{
    private readonly IApiConsumerRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public RotateApiConsumerKeyHandler(IApiConsumerRepository repository, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ApiConsumerKeyIssuedDto>> Handle(RotateApiConsumerKeyCommand request, CancellationToken ct = default)
    {
        var consumer = await _repository.GetByIdAsync(request.Id, ct);
        if (consumer is null)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure("Consumer not found.");

        var (key, hash, prefix) = ApiKeys.Generate();
        consumer.RotateKey(hash, prefix, request.ModifiedByUserId);
        _repository.Update(consumer);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult<ApiConsumerKeyIssuedDto>.Success(new ApiConsumerKeyIssuedDto(ApiConsumerMapping.ToDto(consumer), key));
    }
}

/// <summary>Issues a new callback signing secret. Callbacks sent from now on are signed with it;
/// the consumer must switch its verifier over.</summary>
public record RotateApiConsumerCallbackSecretCommand(Guid Id, Guid ModifiedByUserId) : IRequest<ApplicationResult<ApiConsumerKeyIssuedDto>>;

public class RotateApiConsumerCallbackSecretHandler : IRequestHandler<RotateApiConsumerCallbackSecretCommand, ApplicationResult<ApiConsumerKeyIssuedDto>>
{
    private readonly IApiConsumerRepository _repository;
    private readonly ICustomerCheckCipher _cipher;
    private readonly IUnitOfWork _unitOfWork;

    public RotateApiConsumerCallbackSecretHandler(IApiConsumerRepository repository, ICustomerCheckCipher cipher, IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _cipher = cipher;
        _unitOfWork = unitOfWork;
    }

    public async Task<ApplicationResult<ApiConsumerKeyIssuedDto>> Handle(RotateApiConsumerCallbackSecretCommand request, CancellationToken ct = default)
    {
        var consumer = await _repository.GetByIdAsync(request.Id, ct);
        if (consumer is null)
            return ApplicationResult<ApiConsumerKeyIssuedDto>.Failure("Consumer not found.");

        var (secret, prefix) = ApiKeys.GenerateSecret();
        consumer.SetCallbackSecret(_cipher.Protect(secret), prefix, request.ModifiedByUserId);
        _repository.Update(consumer);
        await _unitOfWork.SaveChangesAsync(ct);
        return ApplicationResult<ApiConsumerKeyIssuedDto>.Success(new ApiConsumerKeyIssuedDto(ApiConsumerMapping.ToDto(consumer), null, secret));
    }
}
