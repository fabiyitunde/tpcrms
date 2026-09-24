using CRMS.Application.Rhshf.Interfaces;
using Microsoft.Extensions.Options;

namespace CRMS.Infrastructure.ExternalServices.Rhshf;

/// <summary>Thin adapter over RhshfSettings' route builders — see IRhshfPublicUrlProvider.</summary>
public class RhshfPublicUrlProvider : IRhshfPublicUrlProvider
{
    private readonly RhshfSettings _settings;

    public RhshfPublicUrlProvider(IOptions<RhshfSettings> settings) => _settings = settings.Value;

    public string? ProfilingUrl(string reference) => _settings.BuildProfilingUrl(reference);

    public string? OfferUrl(string reference) => _settings.BuildOfferUrl(reference);
}
