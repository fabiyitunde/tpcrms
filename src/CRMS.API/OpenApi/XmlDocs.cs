using System.Collections.Concurrent;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CRMS.API.OpenApi;

/// <summary>
/// The XML documentation comments of the API, Application and Domain assemblies, read once from the
/// .xml files the build writes next to the assemblies. The API reference takes its summaries,
/// parameter notes and field descriptions from here, so a comment in the code is the single source
/// for what the reference says.
/// </summary>
public sealed class XmlDocs
{
    private readonly Dictionary<string, XElement> _members = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string?> _methodKeys = new();

    public XmlDocs()
    {
        var dir = AppContext.BaseDirectory;
        foreach (var name in new[] { "CRMS.API.xml", "CRMS.Application.xml", "CRMS.Domain.xml" })
        {
            var path = Path.Combine(dir, name);
            if (!File.Exists(path)) continue;
            try
            {
                foreach (var m in XDocument.Load(path).Descendants("member"))
                    if (m.Attribute("name")?.Value is { } key)
                        _members[key] = m;
            }
            catch (Exception)
            {
                // A malformed comment file must never stop the API from starting.
            }
        }
    }

    public bool IsEmpty => _members.Count == 0;

    public string? MethodSummary(MethodInfo method) => Text(Method(method)?.Element("summary"));

    public string? MethodRemarks(MethodInfo method) => Text(Method(method)?.Element("remarks"));

    public string? MethodReturns(MethodInfo method) => Text(Method(method)?.Element("returns"));

    public string? MethodParam(MethodInfo method, string name) =>
        Text(Method(method)?.Elements("param").FirstOrDefault(p => p.Attribute("name")?.Value == name));

    public string? TypeSummary(Type type) =>
        _members.TryGetValue("T:" + Name(type), out var m) ? Text(m.Element("summary")) : null;

    /// <summary>A property's summary, or for a positional record the matching <c>param</c> note on
    /// the record itself.</summary>
    public string? MemberSummary(Type declaringType, string memberName)
    {
        if (_members.TryGetValue($"P:{Name(declaringType)}.{memberName}", out var p))
            return Text(p.Element("summary"));
        if (_members.TryGetValue("T:" + Name(declaringType), out var t))
            return Text(t.Elements("param").FirstOrDefault(x => x.Attribute("name")?.Value == memberName));
        return null;
    }

    private XElement? Method(MethodInfo method)
    {
        var prefix = $"M:{Name(method.DeclaringType!)}.{method.Name}";
        var key = _methodKeys.GetOrAdd(prefix, p =>
            _members.ContainsKey(p) ? p : _members.Keys.FirstOrDefault(k => k.StartsWith(p + "(", StringComparison.Ordinal)));
        return key is not null && _members.TryGetValue(key, out var m) ? m : null;
    }

    private static string Name(Type type) => (type.FullName ?? type.Name).Replace('+', '.');

    private static readonly Regex Whitespace = new(@"[ \t]*\r?\n[ \t]*", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"[ \t]{2,}", RegexOptions.Compiled);

    /// <summary>Comment XML as Markdown: code in backticks, references by their short name,
    /// paragraphs kept, everything else flattened to one line.</summary>
    private static string? Text(XElement? element)
    {
        if (element is null) return null;
        var sb = new System.Text.StringBuilder();
        Append(element, sb);
        var text = Whitespace.Replace(sb.ToString().Replace("\r", ""), "\n");
        text = string.Join("\n\n", text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Spaces.Replace(p.Replace('\n', ' ').Trim(), " ")).Where(p => p.Length > 0));
        return text.Length == 0 ? null : text;
    }

    private static void Append(XElement element, System.Text.StringBuilder sb)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText t:
                    sb.Append(t.Value);
                    break;
                case XElement e when e.Name.LocalName is "see" or "seealso":
                    var cref = e.Attribute("cref")?.Value ?? e.Attribute("href")?.Value ?? e.Value;
                    sb.Append('`').Append(ShortName(cref)).Append('`');
                    break;
                case XElement e when e.Name.LocalName is "paramref" or "typeparamref":
                    sb.Append('`').Append(e.Attribute("name")?.Value).Append('`');
                    break;
                case XElement e when e.Name.LocalName is "c" or "code":
                    sb.Append('`').Append(e.Value.Trim()).Append('`');
                    break;
                case XElement e when e.Name.LocalName is "b" or "strong":
                    sb.Append("**").Append(e.Value.Trim()).Append("**");
                    break;
                case XElement e when e.Name.LocalName == "para":
                    sb.Append("\n\n");
                    Append(e, sb);
                    sb.Append("\n\n");
                    break;
                case XElement e when e.Name.LocalName == "br":
                    sb.Append("\n\n");
                    break;
                case XElement e:
                    Append(e, sb);
                    break;
            }
        }
    }

    private static string ShortName(string cref)
    {
        var name = cref.Contains(':') ? cref[(cref.IndexOf(':') + 1)..] : cref;
        var paren = name.IndexOf('(');
        if (paren >= 0) name = name[..paren];
        return name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
    }
}
