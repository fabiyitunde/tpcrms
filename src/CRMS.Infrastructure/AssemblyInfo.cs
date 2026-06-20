using System.Runtime.CompilerServices;

// Expose internal members (e.g. SmartComplyProvider.MapCacAdvancedToResult) to the test project
// so the CAC mapping/dedup logic can be unit-tested without live API calls.
[assembly: InternalsVisibleTo("CRMS.Infrastructure.Tests")]
