using CRMS.Application.Rhshf.Interfaces;

namespace CRMS.Infrastructure.Tests.Rhshf;

internal class FakeRhshfPublicUrlProvider : IRhshfPublicUrlProvider
{
    public string? ProfilingUrl(string reference) => $"https://crms.test/rhshf/profiling/{reference}";

    public string? OfferUrl(string reference) => $"https://crms.test/rhshf/offer/{reference}";
}
