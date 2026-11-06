using System.Text.Json;
using CRMS.API.Controllers;
using CRMS.API.OpenApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace CRMS.API.Tests;

/// <summary>
/// The API reference must be usable by someone who has never seen the code: every operation named
/// and described, every request and response shown, and the way to authenticate stated. This
/// generates the OpenAPI document exactly as the API serves it (same controllers, same
/// transformers) on an in-memory server with no database or integrations, and checks it.
/// <para>
/// A new endpoint that fails here needs a <c>/// &lt;summary&gt;</c> on its action and a
/// <c>[ProducesResponseType]</c> for what it returns; an action that reads its body by hand needs
/// <c>[RequestBodyDoc]</c>.
/// </para>
/// </summary>
public class OpenApiDocumentTests : IAsyncLifetime
{
    private static readonly string[] Methods = ["get", "post", "put", "patch", "delete"];
    private WebApplication _app = null!;
    private JsonElement _doc;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddControllers().AddApplicationPart(typeof(CustomerCheckController).Assembly);
        builder.Services.AddCrmsOpenApi();
        _app = builder.Build();
        _app.MapOpenApi();
        await _app.StartAsync();

        var json = await _app.GetTestClient().GetStringAsync("/openapi/v1.json");
        if (Environment.GetEnvironmentVariable("OPENAPI_DUMP") is { Length: > 0 } dump)
            await File.WriteAllTextAsync(dump, json);
        _doc = JsonDocument.Parse(json).RootElement;
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();

    private IEnumerable<(string Path, string Method, JsonElement Op)> Operations()
    {
        foreach (var path in _doc.GetProperty("paths").EnumerateObject())
            foreach (var op in path.Value.EnumerateObject().Where(o => Methods.Contains(o.Name)))
                yield return (path.Name, op.Name, op.Value);
    }

    private static string Id((string Path, string Method, JsonElement Op) o) => $"{o.Method.ToUpperInvariant()} {o.Path}";

    private static bool HasSchema(JsonElement holder) =>
        holder.TryGetProperty("content", out var content)
        && content.EnumerateObject().Any(c => c.Value.TryGetProperty("schema", out var s) && s.EnumerateObject().Any());

    [Fact]
    public void The_document_opens_with_a_guide_and_names_both_ways_of_signing_in()
    {
        var info = _doc.GetProperty("info");
        Assert.Equal("CRMS API", info.GetProperty("title").GetString());
        var guide = info.GetProperty("description").GetString()!;
        Assert.Contains("Partner systems: start here", guide);
        Assert.Contains("Bank staff and internal tools: start here", guide);
        Assert.Contains("POST /api/v1/auth/login", guide);
        Assert.Contains("POST /api/v1/customer-credit-check", guide);

        var schemes = _doc.GetProperty("components").GetProperty("securitySchemes");
        Assert.Equal("bearer", schemes.GetProperty("Bearer").GetProperty("scheme").GetString());
        Assert.Equal("X-Api-Key", schemes.GetProperty("ApiKey").GetProperty("name").GetString());
        Assert.Equal("header", schemes.GetProperty("ApiKey").GetProperty("in").GetString());
    }

    [Fact]
    public void Every_operation_has_a_summary_a_description_and_a_known_tag()
    {
        var tags = _doc.GetProperty("tags").EnumerateArray().ToDictionary(t => t.GetProperty("name").GetString()!, t => t);
        var problems = new List<string>();
        foreach (var o in Operations())
        {
            if (!o.Op.TryGetProperty("summary", out var s) || string.IsNullOrWhiteSpace(s.GetString())) problems.Add($"{Id(o)}: no summary");
            if (!o.Op.TryGetProperty("description", out var d) || !d.GetString()!.Contains("**Authentication:**")) problems.Add($"{Id(o)}: no description");
            var tag = o.Op.GetProperty("tags")[0].GetString()!;
            if (!tags.TryGetValue(tag, out var t) || !t.TryGetProperty("description", out _)) problems.Add($"{Id(o)}: tag '{tag}' has no description");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));

        // The sidebar is grouped by audience, partners first.
        var groups = _doc.GetProperty("x-tagGroups").EnumerateArray().Select(g => g.GetProperty("name").GetString()).ToList();
        Assert.Equal(CrmsOpenApi.Groups.Order, groups);
    }

    [Fact]
    public void Summaries_come_from_the_code_comments_not_from_method_names()
    {
        var login = Operations().Single(o => o.Path == "/api/v1/Auth/login").Op;
        Assert.Equal("Sign in with email and password", login.GetProperty("summary").GetString());
        Assert.Contains("Five wrong passwords", login.GetProperty("description").GetString());
        Assert.Contains("**Authentication:** none.", login.GetProperty("description").GetString());

        var submit = Operations().Single(o => o.Path == "/api/v1/customer-credit-check" && o.Method == "post").Op;
        Assert.Equal("Accept one check and queue it", submit.GetProperty("summary").GetString());
        Assert.Contains("X-Api-Key", submit.GetProperty("description").GetString());
        Assert.Contains("does not run while you wait", submit.GetProperty("description").GetString());
    }

    [Fact]
    public void Every_operation_shows_what_it_returns()
    {
        var problems = new List<string>();
        foreach (var o in Operations())
        {
            var responses = o.Op.GetProperty("responses").EnumerateObject().ToList();
            var success = responses.Where(r => r.Name.StartsWith('2')).ToList();
            if (success.Count == 0) { problems.Add($"{Id(o)}: no success response"); continue; }
            // 204 has no body by definition; anything else must show its payload unless the action
            // is listed below as answering with an empty body.
            if (success.All(r => r.Name == "204")) continue;
            if (!success.Any(r => HasSchema(r.Value)) && !EmptyBodied.Contains(Id(o)))
                problems.Add($"{Id(o)}: success response has no schema");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Every_operation_that_takes_a_body_shows_it()
    {
        var problems = new List<string>();
        foreach (var o in Operations().Where(o => o.Method is "post" or "put" or "patch"))
        {
            var has = o.Op.TryGetProperty("requestBody", out var body) && HasSchema(body);
            if (!has && !Bodiless.Contains(Id(o))) problems.Add($"{Id(o)}: no request body schema");
            if (has && Bodiless.Contains(Id(o))) problems.Add($"{Id(o)}: listed as bodiless but has a body; remove it from the list");
        }
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    [Fact]
    public void Partner_endpoints_use_the_api_key_and_staff_endpoints_the_bearer_token()
    {
        foreach (var o in Operations())
        {
            var tag = o.Op.GetProperty("tags")[0].GetString()!;
            var security = o.Op.TryGetProperty("security", out var s) ? s.EnumerateArray().SelectMany(r => r.EnumerateObject().Select(p => p.Name)).ToList() : [];
            var group = CrmsOpenApi.Tags.Values.First(t => t.Tag == tag).Group;
            if (group == CrmsOpenApi.Groups.Partners)
                Assert.True(security.SequenceEqual(["ApiKey"]), $"{Id(o)} should use the API key");
            else if (security.Count > 0 && tag != "Health")
                Assert.True(security.SequenceEqual(["Bearer"]), $"{Id(o)} should use the bearer token");
            if (security.Count > 0)
                Assert.True(o.Op.GetProperty("responses").TryGetProperty("401", out _), $"{Id(o)} should document 401");
        }

        string[] open = ["POST /api/v1/Auth/login", "POST /api/v1/Auth/refresh"];
        foreach (var o in Operations().Where(o => open.Contains(Id(o))))
            Assert.False(o.Op.TryGetProperty("security", out var s) && s.GetArrayLength() > 0, $"{Id(o)} must be callable without credentials");
    }

    [Fact]
    public void The_customer_check_is_documented_end_to_end()
    {
        var submit = Operations().Single(o => o.Path == "/api/v1/customer-credit-check" && o.Method == "post").Op;
        var body = submit.GetProperty("requestBody").GetProperty("content").GetProperty("application/json");
        // The example a reader meets first carries only what must be sent; the optional fields
        // are a second example, so nobody takes them for requirements.
        Assert.Equal("#/components/schemas/CustomerCreditCheckRequest", body.GetProperty("schema").GetProperty("$ref").GetString());
        var examples = body.GetProperty("examples");
        var first = examples.EnumerateObject().First();
        Assert.Equal("Required fields only", first.Value.GetProperty("summary").GetString());
        Assert.Equal(["bvn", "requestId"], first.Value.GetProperty("value").EnumerateObject().Select(p => p.Name).Order());
        Assert.True(examples.GetProperty("optional").GetProperty("value").TryGetProperty("sections", out _));
        var request = Resolve(body.GetProperty("schema"));
        foreach (var field in new[] { "requestId", "bvn", "nubans", "sections", "options", "callbackUrl" })
            Assert.True(request.GetProperty("properties").TryGetProperty(field, out _), $"request is missing {field}");
        // Only what the endpoint insists on carries the "required" badge.
        Assert.Equal(["bvn", "requestId"], request.GetProperty("required").EnumerateArray().Select(r => r.GetString()).Order());
        Assert.Contains("Optional", request.GetProperty("properties").GetProperty("nubans").GetProperty("description").GetString());

        var accepted = Resolve(submit.GetProperty("responses").GetProperty("202").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        foreach (var field in new[] { "jobId", "requestId", "status", "statusUrl", "callbackRegistered" })
            Assert.True(accepted.GetProperty("properties").TryGetProperty(field, out _), $"202 is missing {field}");
        foreach (var code in new[] { "400", "401", "429", "503" })
            Assert.True(HasSchema(submit.GetProperty("responses").GetProperty(code)), $"{code} should show the error body");

        var get = Operations().Single(o => o.Path == "/api/v1/customer-credit-check/{jobId}").Op;
        var view = Resolve(get.GetProperty("responses").GetProperty("200").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        foreach (var field in new[] { "status", "callback", "result", "error" })
            Assert.True(view.GetProperty("properties").TryGetProperty(field, out _), $"job view is missing {field}");

        var history = Operations().Single(o => o.Path == "/api/v1/customer-credit-check/history").Op;
        Assert.True(HasSchema(history.GetProperty("requestBody")));
        Assert.True(HasSchema(history.GetProperty("responses").GetProperty("200")));
    }

    private JsonElement Resolve(JsonElement schema)
    {
        if (!schema.TryGetProperty("$ref", out var r)) return schema;
        var name = r.GetString()!.Split('/').Last();
        return _doc.GetProperty("components").GetProperty("schemas").GetProperty(name);
    }

    /// <summary>Actions that take no body by design: the route says everything ("submit this",
    /// "activate that"). Add a new one here only if it truly has nothing to send.</summary>
    private static readonly HashSet<string> Bodiless =
    [
        "POST /api/v1/Auth/logout",
        "POST /api/Committee/{id}/start-voting",
        "POST /api/Committee/{id}/close",
        "POST /api/v1/financial-statements/{id}/submit",
        "POST /api/LoanPack/generate/{loanApplicationId}",
        "POST /api/v1/LoanProducts/{id}/activate",
        "POST /api/v1/namp/applications/{id}/submit",
        "POST /api/v1/namp/applications/{id}/ratify",
        "POST /api/v1/namp/applications/{id}/accept-offer",
        "POST /api/v1/namp/applications/{id}/lapse-offer",
        "POST /api/v1/namp/applications/{id}/begin-pdv",
        "POST /api/v1/namp/applications/{id}/run-credit-checks",
        "POST /api/Notification/{id}/mark-read",
        "POST /api/Notification/templates/{id}/deactivate",
        "POST /v1/credit-profiles/{reference}/token",
        "POST /api/ScoringConfiguration/{id}/cancel",
        "POST /api/ScoringConfiguration/seed",
        "POST /api/Seed/comprehensive",
        "POST /api/Seed/reset-passwords",
        "POST /api/Seed/basic",
        "POST /api/Seed/namp-cleanup-migration",
        "POST /api/Seed/namp",
        "POST /api/Seed/namp-repair-checklist",
        "POST /api/v1/statements/{id}/analyze",
        "POST /api/Workflow/{id}/unassign",
        "POST /api/Workflow/seed-corporate-workflow",
    ];

    /// <summary>Actions that answer 200 with an empty body by design.</summary>
    private static readonly HashSet<string> EmptyBodied =
    [
        "POST /api/v1/loan-applications/{id}/submit",
        "POST /api/v1/loan-applications/{id}/branch-approve",
        "POST /api/v1/loan-applications/{id}/branch-return",
        "POST /api/v1/loan-applications/{id}/documents/{documentId}/verify",
        "POST /api/Notification/{id}/mark-read",
        "POST /api/Workflow/seed-corporate-workflow",
    ];
}
