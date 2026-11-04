using CRMS.Domain.Aggregates.Rhshf;
using CRMS.Domain.Enums;
using CRMS.Infrastructure.Persistence;
using Xunit;

namespace CRMS.Infrastructure.Tests.Rhshf;

/// <summary>The default reason codes every environment starts with (CRMSQA#1).</summary>
public class RhshfReasonCodeSeedTests
{
    [Fact]
    public void The_defaults_are_valid_unique_and_include_OTHER()
    {
        var defaults = RhshfWorkflowSeeder.DefaultReasonCodes;

        Assert.All(defaults, d => Assert.True(RhshfReasonCode.Create(d.Code, d.Type, d.Label, 1).IsSuccess, d.Code));
        Assert.Equal(defaults.Length, defaults.Select(d => d.Code).Distinct().Count());
        Assert.Contains(defaults, d => d.Code == RhshfReasonCode.Other);
        Assert.Contains(defaults, d => d.Type == RhshfReasonCodeType.Correction && d.Code != RhshfReasonCode.Other);
        Assert.Contains(defaults, d => d.Type == RhshfReasonCodeType.Decline);
    }
}
