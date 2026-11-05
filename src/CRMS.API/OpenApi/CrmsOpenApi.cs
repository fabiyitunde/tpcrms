using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;

namespace CRMS.API.OpenApi;

/// <summary>
/// The API reference (Scalar at <c>/scalar/v1</c>, document at <c>/openapi/v1.json</c>): a "start
/// here" guide, the two ways of authenticating, endpoints grouped by audience, a summary and
/// description on every operation taken from the code comments, and request and response
/// schemas for all of them.
/// </summary>
public static class CrmsOpenApi
{
    public const string BearerScheme = "Bearer";
    public const string ApiKeyScheme = "ApiKey";

    public static IServiceCollection AddCrmsOpenApi(this IServiceCollection services)
    {
        services.TryAddSingleton<XmlDocs>();
        services.TryAddEnumerable(ServiceDescriptor.Transient<IApiDescriptionProvider, RequestBodyDocApiDescriptionProvider>());
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<CrmsDocumentTransformer>();
            options.AddOperationTransformer<CrmsOperationTransformer>();
            options.AddSchemaTransformer<CrmsSchemaTransformer>();
        });
        return services;
    }

    /// <summary>Controller name to the tag shown in the reference, its description, and its group.</summary>
    public static readonly IReadOnlyDictionary<string, (string Tag, string Group, string Description)> Tags =
        new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase)
        {
            ["CustomerCheck"] = ("Customer check", Groups.Partners,
                "For third-party systems doing credit checks on the bank's behalf. One BVN in; identity, credit bureau, fraud check, bank accounts and loans, CRMS facilities and an AI advisory out. **Asynchronous:** submit a check (202), then receive the result on your callback URL or fetch it by job id. Authenticate with your consumer key in `X-Api-Key`."),
            ["PartyExposure"] = ("Party exposure", Groups.Partners,
                "For the Farmers Portal's Farmer 360: every CRMS facility a person or company is tied to, in any role, across the three loan tracks. Requests carry the portal's API key and an HMAC signature."),
            ["RhshfSubmission"] = ("RH-SHF credit profiles", Groups.Partners,
                "For the Farmers Portal: submit a Farmers' Aggregation Centre's consolidated EOPs for credit profiling, refresh its profiling link, and read a case's status."),
            ["NampWebhook"] = ("NAMP webhook", Groups.Partners,
                "For the NAMP portal: hand an equipment-loan application to the CRMS and read its status."),

            ["Auth"] = ("Sign in", Groups.Access, "Staff sign-in. Log in to get the access token every staff endpoint needs, refresh it, sign out, change a password."),
            ["Users"] = ("Users", Groups.Access, "Staff accounts."),

            ["LoanApplications"] = ("Loan applications", Groups.Corporate, "Corporate loan applications: start one, submit it, branch review, documents, and the queues."),
            ["LoanProducts"] = ("Loan products", Groups.Corporate, "The product catalogue and its pricing tiers."),
            ["Collateral"] = ("Collateral", Groups.Corporate, "Collateral pledged on a loan application: capture, valuation, approval, perfection and loan-to-value."),
            ["Guarantors"] = ("Guarantors", Groups.Corporate, "Guarantors on a loan application and their credit checks."),
            ["FinancialStatements"] = ("Financial statements", Groups.Corporate, "A borrower's annual accounts: balance sheet, income statement, cash flow, and the ratios derived from them."),
            ["StatementAnalysis"] = ("Bank statement analysis", Groups.Corporate, "Bank statements uploaded for a loan application and the cashflow analysis run on them."),
            ["CreditBureau"] = ("Credit bureau", Groups.Corporate, "Credit bureau reports requested for the parties to a loan application."),
            ["Advisory"] = ("AI advisory", Groups.Corporate, "The credit advisory generated for a loan application: scores, recommendation and narrative."),
            ["Committee"] = ("Committee", Groups.Corporate, "Committee reviews: membership, voting, decision and closing."),
            ["LoanPack"] = ("Loan pack", Groups.Corporate, "The loan pack PDF assembled for a committee."),
            ["Workflow"] = ("Workflow", Groups.Corporate, "Where an application is in its workflow, who holds it, what it can move to, and the queues."),
            ["CoreBanking"] = ("Core banking", Groups.Corporate, "Read-only lookups against core banking: a corporate customer's record, an account, a statement."),

            ["NampApplications"] = ("NAMP applications", Groups.Namp, "Equipment-loan (NAMP) applications from the staging queue through appraisal, committee, ratification, offer, pre-deployment and deployment."),
            ["NampAdmin"] = ("NAMP routing", Groups.Namp, "Which committee tier a NAMP application is routed to, by amount."),

            ["Reporting"] = ("Reporting", Groups.Operations, "Dashboards and reports: funnel, portfolio, performance, SLA, committee."),
            ["Audit"] = ("Audit trail", Groups.Operations, "Who did what, and who read sensitive data."),
            ["Notification"] = ("Notifications", Groups.Operations, "Notifications sent to staff, and their templates."),
            ["ScoringConfiguration"] = ("Scoring configuration", Groups.Operations, "The advisory's scoring parameters, changed under maker-checker."),
            ["Health"] = ("Health", Groups.Operations, "Connectivity probe for the API's dependencies."),
            ["Seed"] = ("Development seeding", Groups.Operations, "Development environment only. Every action here refuses outside Development."),
        };

    public static class Groups
    {
        public const string Partners = "Partner integrations (API key)";
        public const string Access = "Sign in";
        public const string Corporate = "Corporate loans (staff)";
        public const string Namp = "NAMP (staff)";
        public const string Operations = "Reporting and administration (staff)";

        public static readonly IReadOnlyList<string> Order = [Partners, Access, Corporate, Namp, Operations];
    }

    public const string Guide = """
        The Credit Risk Management System's API. It serves two audiences, and each signs in differently. Find yours below, authenticate once in the **Authentication** panel of this page, and every request you send from here will carry it.

        ## Partner systems: start here

        You are a third-party system the bank has given a key to, such as a credit-check consumer or the Farmers Portal.

        1. **Get your key.** A bank administrator creates your consumer in the CRMS intranet under *Administration > API Consumers* and gives you an API key and a callback signing secret. Both are shown once.
        2. **Authenticate.** In the Authentication panel choose **ApiKey** and paste the key. It is sent as the `X-Api-Key` header.
        3. **Run a customer check.** `POST /api/v1/customer-check` with a `requestId` of your own and the customer's `bvn`. The answer is `202 Accepted` with a `jobId`. The check then runs in the background and takes up to a minute or two.
        4. **Get the result.** Either receive it on your callback URL, signed with your secret, or call `GET /api/v1/customer-check/{jobId}` until `status` is `COMPLETED`. Stored results can be fetched again at any time, free of charge.
        5. **Look back.** `POST /api/v1/customer-check/history` lists your past checks.

        Sending the same `requestId` again within 24 hours returns the same job instead of running, and billing, a second check.

        ## Bank staff and internal tools: start here

        1. **Sign in.** `POST /api/v1/auth/login` with your email and password. Copy `accessToken` from the answer.
        2. **Authenticate.** In the Authentication panel choose **Bearer** and paste the token.
        3. **Call anything under the staff groups.** Some operations need a particular role; where one does, its description says which.

        When the token expires, `POST /api/v1/auth/refresh` exchanges your refresh token for a new one.

        ## Conventions

        - **Bodies** are JSON. Property names are camelCase.
        - **Dates** are ISO 8601 in UTC. **Money** is a decimal number in Naira unless a `currency` field says otherwise.
        - **Identifiers** (BVN, account numbers) travel only in request bodies, never in a URL, and come back masked to their last four digits.
        - **Errors.** Partner endpoints answer with `{ "error": "<code>", "message": "..." }`, and `messages` listing each problem when validation fails. Staff endpoints answer `400`, `404` and so on with a plain-text reason.
        - **Rate limits.** `429` with a `Retry-After` header means slow down and try again after that many seconds.
        """;
}

/// <summary>Adds a JSON request body to the description of an action marked with
/// <see cref="RequestBodyDocAttribute"/>, so the generator documents it like any bound body.</summary>
internal sealed class RequestBodyDocApiDescriptionProvider : IApiDescriptionProvider
{
    private readonly IModelMetadataProvider _metadata;

    public RequestBodyDocApiDescriptionProvider(IModelMetadataProvider metadata) => _metadata = metadata;

    // After the framework's own provider (-1000), which builds the descriptions this one amends.
    public int Order => -900;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context)
    {
        foreach (var description in context.Results)
        {
            if (description.ActionDescriptor is not ControllerActionDescriptor action) continue;
            var doc = action.MethodInfo.GetCustomAttribute<RequestBodyDocAttribute>();
            if (doc is null || description.ParameterDescriptions.Any(p => p.Source == BindingSource.Body)) continue;

            description.ParameterDescriptions.Add(new ApiParameterDescription
            {
                Name = "body",
                Source = BindingSource.Body,
                Type = doc.Type,
                ModelMetadata = _metadata.GetMetadataForType(doc.Type),
                IsRequired = true,
            });
            if (description.SupportedRequestFormats.Count == 0)
                description.SupportedRequestFormats.Add(new ApiRequestFormat { MediaType = "application/json" });
        }
    }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context) { }
}

internal sealed class CrmsDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "CRMS API",
            Version = "v1",
            Description = CrmsOpenApi.Guide,
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes[CrmsOpenApi.BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Staff access token. Get one from `POST /api/v1/auth/login` and paste the `accessToken` here.",
        };
        document.Components.SecuritySchemes[CrmsOpenApi.ApiKeyScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-Api-Key",
            Description = "A partner system's key. Customer-check consumers get theirs from a bank administrator (Administration > API Consumers); the Farmers Portal and the NAMP portal use the key agreed for their integration.",
        };

        // Only the tags in use, in audience order, each with its description; grouped for the sidebar.
        var used = document.Paths.Values.SelectMany(p => p.Operations.Values).SelectMany(o => o.Tags).Select(t => t.Name).ToHashSet();
        var known = CrmsOpenApi.Tags.Values.Where(t => used.Contains(t.Tag)).ToList();
        document.Tags = known.Select(t => new OpenApiTag { Name = t.Tag, Description = t.Description })
            .Concat(used.Except(known.Select(t => t.Tag)).Order().Select(t => new OpenApiTag { Name = t }))
            .ToList();

        var groups = new OpenApiArray();
        foreach (var group in CrmsOpenApi.Groups.Order)
        {
            var tags = new OpenApiArray();
            tags.AddRange(known.Where(t => t.Group == group).Select(t => new OpenApiString(t.Tag)));
            if (tags.Count > 0)
                groups.Add(new OpenApiObject { ["name"] = new OpenApiString(group), ["tags"] = tags });
        }
        document.Extensions["x-tagGroups"] = groups;
        return Task.CompletedTask;
    }
}

internal sealed class CrmsOperationTransformer : IOpenApiOperationTransformer
{
    private static readonly Regex Camel = new("(?<=[a-z0-9])(?=[A-Z])", RegexOptions.Compiled);
    private readonly XmlDocs _docs;

    public CrmsOperationTransformer(XmlDocs docs) => _docs = docs;

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action)
            return Task.CompletedTask;
        var method = action.MethodInfo;

        // Tag: the audience-facing name of the controller.
        if (CrmsOpenApi.Tags.TryGetValue(action.ControllerName, out var tag))
            operation.Tags = [new OpenApiTag { Name = tag.Tag }];
        operation.OperationId ??= $"{action.ControllerName}_{action.ActionName}";

        // Summary and description from the action's comment. An action without one still gets a
        // readable name, never a blank line in the sidebar.
        var summary = _docs.MethodSummary(method);
        var remarks = _docs.MethodRemarks(method);
        if (summary is not null)
        {
            var (headline, rest) = SplitSummary(summary);
            operation.Summary = headline;
            remarks = string.Join("\n\n", new[] { rest, remarks }.Where(s => !string.IsNullOrWhiteSpace(s)));
        }
        operation.Summary ??= Humanise(action.ActionName);

        var notes = new List<string>();
        if (!string.IsNullOrWhiteSpace(remarks)) notes.Add(remarks!);

        // Who may call it, and with what.
        var apiKey = method.GetCustomAttribute<ApiKeyAuthAttribute>() ?? action.ControllerTypeInfo.GetCustomAttribute<ApiKeyAuthAttribute>();
        var anonymous = method.GetCustomAttribute<AllowAnonymousAttribute>() is not null;
        var authorize = method.GetCustomAttributes<AuthorizeAttribute>().Concat(action.ControllerTypeInfo.GetCustomAttributes<AuthorizeAttribute>()).ToList();
        if (apiKey is not null)
        {
            Secure(operation, CrmsOpenApi.ApiKeyScheme);
            notes.Add($"**Authentication:** `X-Api-Key` header. {apiKey.WhoIssuesIt}");
        }
        else if (authorize.Count > 0 && !anonymous)
        {
            Secure(operation, CrmsOpenApi.BearerScheme);
            // Method-level roles narrow the controller's; show the ones that actually apply.
            var roles = (method.GetCustomAttributes<AuthorizeAttribute>().FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Roles))
                ?? authorize.FirstOrDefault(a => !string.IsNullOrWhiteSpace(a.Roles)))?.Roles;
            notes.Add(roles is null
                ? "**Authentication:** staff access token (Bearer)."
                : $"**Authentication:** staff access token (Bearer). **Role required:** {string.Join(", ", roles.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))}.");
        }
        else
        {
            notes.Add("**Authentication:** none.");
        }

        if (method.GetCustomAttribute<DevelopmentOnlyAttribute>() is not null || action.ControllerTypeInfo.GetCustomAttribute<DevelopmentOnlyAttribute>() is not null)
            notes.Add("**Development environment only.** Refused with `400` anywhere else.");

        operation.Description = string.Join("\n\n", notes);

        // Parameter notes from the comment's <param> entries.
        foreach (var parameter in operation.Parameters ?? [])
        {
            if (!string.IsNullOrWhiteSpace(parameter.Description)) continue;
            var match = method.GetParameters().FirstOrDefault(p => string.Equals(p.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
            if (match?.Name is { } name && _docs.MethodParam(method, name) is { } text)
                parameter.Description = text;
        }

        // The body's example, where the action supplies one.
        if (method.GetCustomAttribute<RequestBodyDocAttribute>() is { Example: { } example } && operation.RequestBody is { } body)
        {
            body.Required = true;
            foreach (var content in body.Content.Values)
                content.Example = ToAny(JsonDocument.Parse(example).RootElement);
        }

        // What comes back.
        if (_docs.MethodReturns(method) is { } returns)
            foreach (var (code, response) in operation.Responses)
                if (code.StartsWith('2') && IsDefaultDescription(response.Description))
                    response.Description = returns;
        return Task.CompletedTask;
    }

    private static void Secure(OpenApiOperation operation, string scheme)
    {
        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = scheme } }] = [],
        });
        if (!operation.Responses.ContainsKey("401"))
            operation.Responses["401"] = new OpenApiResponse { Description = "Missing, invalid or expired credentials." };
    }

    private static bool IsDefaultDescription(string? d) => string.IsNullOrWhiteSpace(d) || d is "OK" or "Created" or "Accepted" or "Success";

    /// <summary>The first sentence is the headline shown in the sidebar; anything after it leads
    /// the description. A comment that opens with the route ("POST /x — ...") drops that prefix.</summary>
    private static (string Headline, string? Tail) SplitSummary(string summary)
    {
        var text = Regex.Replace(summary, @"^(GET|POST|PUT|PATCH|DELETE)\s+\S+\s*[—:-]\s*", "", RegexOptions.IgnoreCase).Trim();
        var end = Regex.Match(text, @"(?<=[a-z0-9\)])\.(\s|$)");
        if (!end.Success || end.Index > 110)
            return text.Length <= 110 ? (text.TrimEnd('.'), null) : (text[..107].TrimEnd() + "…", text);
        var headline = text[..end.Index].Trim();
        var rest = text[(end.Index + 1)..].Trim();
        return (char.ToUpperInvariant(headline[0]) + headline[1..], rest.Length == 0 ? null : rest);
    }

    private static string Humanise(string actionName)
    {
        var words = Camel.Replace(actionName, " ").ToLowerInvariant();
        return char.ToUpperInvariant(words[0]) + words[1..];
    }

    private static IOpenApiAny ToAny(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => ToObject(e),
        JsonValueKind.Array => ToArray(e),
        JsonValueKind.String => new OpenApiString(e.GetString()),
        JsonValueKind.Number => e.TryGetInt64(out var l) ? new OpenApiLong(l) : new OpenApiDouble(e.GetDouble()),
        JsonValueKind.True => new OpenApiBoolean(true),
        JsonValueKind.False => new OpenApiBoolean(false),
        _ => new OpenApiNull(),
    };

    private static OpenApiObject ToObject(JsonElement e)
    {
        var o = new OpenApiObject();
        foreach (var p in e.EnumerateObject()) o[p.Name] = ToAny(p.Value);
        return o;
    }

    private static OpenApiArray ToArray(JsonElement e)
    {
        var a = new OpenApiArray();
        a.AddRange(e.EnumerateArray().Select(ToAny));
        return a;
    }
}

/// <summary>Field and type descriptions from the code comments.</summary>
internal sealed class CrmsSchemaTransformer : IOpenApiSchemaTransformer
{
    private readonly XmlDocs _docs;

    public CrmsSchemaTransformer(XmlDocs docs) => _docs = docs;

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(schema.Description)) return Task.CompletedTask;

        // A field's own note goes on its inline schema. A field whose type is another object shares
        // that object's schema, so a field-specific note must not leak onto it; such a schema, and
        // any schema reached as a body or a response, is described by the type's own comment.
        if (context.JsonPropertyInfo is { } property && context.JsonTypeInfo.Kind != JsonTypeInfoKind.Object)
        {
            var member = (property.AttributeProvider as MemberInfo)?.Name;
            if (member is not null && _docs.MemberSummary(property.DeclaringType, member) is { } text)
                schema.Description = text;
        }
        else if (context.JsonTypeInfo.Kind == JsonTypeInfoKind.Object && _docs.TypeSummary(context.JsonTypeInfo.Type) is { } summary)
        {
            schema.Description = summary;
        }
        return Task.CompletedTask;
    }
}
