namespace AllSpice.CleanModularMonolith.Pdf;

/// <summary>
/// Builds HTML header/footer elements and the page-frame table wrapper.
/// The fixed elements render visually; the table thead/tfoot reserve space on every page.
/// </summary>
public static class PdfFooterBuilder
{
    /// <summary>
    /// Builds header + footer + opening page-frame for versioned documents.
    /// Call this right after the cover page. Close with <see cref="ClosePageFrame"/>.
    /// </summary>
    public static string Build(string organizationName, string documentType, int? version)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(organizationName);
        var encodedType = System.Net.WebUtility.HtmlEncode(documentType);
        var versionSuffix = version.HasValue ? $" | v{version.Value}" : "";
        return $$"""
<div class='page-header'>{{encoded}} — {{encodedType}}</div>
<div class='page-footer'>
  <span>{{encodedType}} | {{encoded}}{{versionSuffix}}</span>
  <span class='footer-confidential'>CONFIDENTIAL</span>
</div>
<table class='page-frame'>
  <thead><tr><td class='header-space'></td></tr></thead>
  <tfoot><tr><td class='footer-space'></td></tr></tfoot>
  <tbody><tr><td class='page-content'>
""";
    }

    /// <summary>
    /// Builds header + footer + opening page-frame for report-style documents.
    /// </summary>
    public static string BuildReport(string documentType, DateTimeOffset generatedAt, string? assemblyVersion)
    {
        var encodedType = System.Net.WebUtility.HtmlEncode(documentType);
        var version = assemblyVersion ?? "unknown";
        var ts = generatedAt.ToString("yyyy-MM-dd HH:mm 'UTC'");
        return $$"""
<div class='page-header'>{{encodedType}}</div>
<div class='page-footer'>
  <span>{{encodedType}} | {{ts}} (v{{version}})</span>
  <span style='color:#d1d5db;'>System-generated record</span>
</div>
<table class='page-frame'>
  <thead><tr><td class='header-space'></td></tr></thead>
  <tfoot><tr><td class='footer-space'></td></tr></tfoot>
  <tbody><tr><td class='page-content'>
""";
    }

    /// <summary>
    /// Builds header + footer + opening page-frame with simple text.
    /// </summary>
    public static string BuildSimple(string text)
    {
        var encoded = System.Net.WebUtility.HtmlEncode(text);
        return $$"""
<div class='page-header'>{{encoded}}</div>
<div class='page-footer'>
  <span>{{encoded}}</span>
  <span></span>
</div>
<table class='page-frame'>
  <thead><tr><td class='header-space'></td></tr></thead>
  <tfoot><tr><td class='footer-space'></td></tr></tfoot>
  <tbody><tr><td class='page-content'>
""";
    }

    /// <summary>
    /// Closes the page-frame table. Place before &lt;/body&gt;.
    /// </summary>
    public static string ClosePageFrame() => """
  </td></tr></tbody>
</table>
""";
}
