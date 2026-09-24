using CRMS.Application.Rhshf.Interfaces;

namespace CRMS.Application.Tests.Rhshf;

/// <summary>Fixed host so tests can assert the exact actionUrl. Returning null from both methods
/// (the unconfigured-host case) is exercised explicitly where it matters.</summary>
internal class FakeRhshfPublicUrlProvider : IRhshfPublicUrlProvider
{
    private readonly string? _host;

    public FakeRhshfPublicUrlProvider(string? host = "https://crms.test") => _host = host;

    public string? ProfilingUrl(string reference) => _host is null ? null : $"{_host}/rhshf/profiling/{reference}";

    public string? OfferUrl(string reference) => _host is null ? null : $"{_host}/rhshf/offer/{reference}";
}
