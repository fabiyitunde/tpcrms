using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace CRMS.Infrastructure.Documents;

/// <summary>
/// Shared Bank of Agriculture brand assets (colour palette + logo) for generated
/// PDF documents. The palette mirrors the branded email templates seeded in
/// <c>SeedData</c> so PDFs and emails stay visually consistent.
/// </summary>
internal static class BoaBrand
{
    // ── Greens ──────────────────────────────────────────────────────────────
    /// <summary>Deep forest green — page rules, section headers.</summary>
    public const string Primary = "#14532d";
    /// <summary>Mid green — table headers, emphasis, call-outs.</summary>
    public const string Accent = "#1f7a3d";
    /// <summary>Soft green — subtitle / muted accent text on dark backgrounds.</summary>
    public const string Subtle = "#a7d3b5";

    // ── Neutrals / panels ───────────────────────────────────────────────────
    /// <summary>Soft green-tinted panel fill (info cells, summary boxes).</summary>
    public const string PanelGreen = "#f0f4f1";
    public const string LightGray = "#f7fafc";
    public const string MediumGray = "#e2e8f0";

    private const string LogoUrl =
        "https://tpclientassets.s3.eu-central-1.amazonaws.com/bankofagriculture/logo.png";

    // Fetched once per process; null if the remote asset cannot be loaded.
    private static readonly Lazy<byte[]?> _logo = new(() =>
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            return http.GetByteArrayAsync(LogoUrl).GetAwaiter().GetResult();
        }
        catch
        {
            return null;
        }
    });

    /// <summary>Cached logo bytes, or <c>null</c> if the asset is unavailable.</summary>
    public static byte[]? Logo => _logo.Value;

    /// <summary>
    /// Renders the Bank of Agriculture logo within a square box of the given
    /// size, falling back to a green "BOA" monogram if the remote logo cannot
    /// be loaded (e.g. no network at generation time).
    /// </summary>
    public static void RenderLogo(IContainer container, float size)
    {
        var logo = Logo;
        if (logo is { Length: > 0 })
        {
            container.Width(size).Height(size).AlignMiddle().Image(logo).FitArea();
        }
        else
        {
            container.Width(size).Height(size)
                .Background(Color.FromHex(Primary))
                .AlignCenter().AlignMiddle()
                .Text("BOA").FontSize(size / 4f).Bold().FontColor(Colors.White);
        }
    }
}
