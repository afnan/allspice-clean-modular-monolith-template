namespace AllSpice.CleanModularMonolith.Pdf;

/// <summary>
/// Shared CSS theme and brand constants for PDF documents.
/// Uses generic branding suitable for template customization.
/// </summary>
public static class PdfTheme
{
    public const string BrandPrimary = "#2563eb";
    public const string BrandDark = "#111827";
    public const string BrandGray = "#6b7280";
    public const string BrandWhite = "#ffffff";
    public const string BrandRed = "#EF4444";

    /// <summary>
    /// Professional A4-formatted CSS for PDF documents.
    /// Generic brand colours, system font stack, print-optimised layout.
    /// </summary>
    public const string Css = """
    /* ===== Print & Page Setup ===== */
    @page {
      size: A4 portrait;
      margin: 0;
    }
    *,
    *::before,
    *::after {
      box-sizing: border-box;
    }
    html {
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    body {
      font-family: 'Segoe UI', Arial, sans-serif;
      font-size: 13px;
      line-height: 1.6;
      color: #1a1a1a;
      margin: 0;
      padding: 0;
      background: #ffffff;
      text-align: justify;
    }

    /* ===== Content layout: table trick for repeating header/footer space ===== */
    .page-frame {
      margin: 0 10mm;
      width: calc(100% - 20mm);
      border-collapse: collapse;
    }
    .page-frame td,
    .page-frame th {
      padding: 0;
      border: 0;
    }
    .header-space, .footer-space {
      height: 10mm;
    }
    .page-content {
      padding: 2mm 0;
    }

    /* ===== Running Header & Footer ===== */
    .page-header {
      position: fixed;
      top: 0;
      left: -5mm;
      right: -5mm;
      padding: 7px 15mm;
      background: #111827;
      color: #9CA3AF;
      font-size: 8px;
      text-align: right;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    .page-footer {
      position: fixed;
      bottom: -1px;
      left: -5mm;
      right: -5mm;
      border-top: 2px solid #2563eb;
      padding: 8px 15mm 10px;
      background: #111827;
      color: #f9fafb;
      font-size: 8px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    .page-footer .footer-brand {
      color: #2563eb;
      font-size: 10px;
      font-weight: 700;
    }
    .page-footer .footer-confidential {
      color: #EF4444;
      font-weight: 700;
      letter-spacing: 1px;
    }

    /* ===== Cover Page ===== */
    .cover,
    .cover-page {
      page-break-after: always;
    }
    .cover {
      margin: 0;
      padding: 0;
      border: 0;
      background: #111827;
      color: #ffffff;
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      width: 100%;
      min-height: 100vh;
      position: relative;
      z-index: 10;
    }
    .cover-body {
      flex: 1;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: 60px 40px 24px;
      width: 100%;
    }
    .cover-title {
      font-size: 36px;
      font-weight: 700;
      color: #2563eb;
      margin: 0 0 6px 0;
      letter-spacing: 1px;
    }
    .cover-subtitle {
      font-size: 22px;
      font-weight: 600;
      color: #ffffff;
      margin: 10px 0 4px;
    }
    .cover-tag {
      color: #2563eb;
      text-transform: uppercase;
      letter-spacing: 3px;
      font-size: 14px;
      font-weight: 700;
      margin-bottom: 20px;
    }
    .cover-meta {
      margin-top: 36px;
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: 10px;
      max-width: 520px;
    }
    .meta-chip {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: rgba(255,255,255,0.07);
      border: 1px solid rgba(255,255,255,0.1);
      border-radius: 20px;
      padding: 5px 14px;
      font-size: 11px;
    }
    .meta-chip-label {
      color: rgba(255,255,255,0.5);
      font-weight: 400;
      text-transform: uppercase;
      font-size: 9px;
      letter-spacing: 0.8px;
    }
    .meta-chip-value {
      color: #ffffff;
      font-weight: 600;
    }
    .cover-divider {
      width: 60px;
      height: 2px;
      background: #2563eb;
      margin: 28px auto 0;
      border-radius: 1px;
    }
    .cover-confidential {
      margin-top: 20px;
      font-size: 10px;
      text-transform: uppercase;
      letter-spacing: 4px;
      color: rgba(255,255,255,0.4);
    }

    /* ===== Part Headers ===== */
    .part-header {
      padding: 0;
      margin: 30px 0 12px 0;
      page-break-after: avoid;
      border-bottom: 3px solid #2563eb;
      padding-bottom: 8px;
    }
    .part-header h2 {
      margin: 0;
      font-size: 22px;
      color: #111827;
      font-weight: 700;
      text-align: left;
    }

    /* ===== Sections ===== */
    .section {
      margin: 16px 0;
      padding: 0;
    }
    .section h3 {
      font-size: 15px;
      color: #111827;
      margin: 0 0 8px 0;
      font-weight: 600;
      text-align: left;
    }
    .section h4 {
      font-size: 14px;
      color: #4B5563;
      margin: 12px 0 4px 0;
      text-align: left;
    }
    .section-text {
      margin: 4px 0;
      color: #374151;
      line-height: 1.7;
    }

    /* ===== Data Tables ===== */
    table.data-table,
    .data-table {
      width: 100%;
      border-collapse: collapse;
      margin: 10px 0;
      font-size: 12px;
    }
    .data-table th,
    .data-table td {
      border: 1px solid #e5e7eb;
      padding: 8px 12px;
      text-align: left;
    }
    .data-table th {
      background: #f3f4f6;
      color: #374151;
      font-weight: 600;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.3px;
    }
    .data-table tr:nth-child(even) {
      background: #F9FAFB;
    }

    /* ===== Badges ===== */
    .badge {
      display: inline;
      font-weight: 700;
      font-size: 11px;
      text-transform: uppercase;
    }
    .badge-high { color: #DC2626; }
    .badge-medium { color: #D97706; }
    .badge-low { color: #059669; }

    /* ===== Cards ===== */
    .card {
      margin: 10px 0;
      padding: 0 0 10px 0;
      border-bottom: 1px solid #f3f4f6;
    }
    .card-title {
      font-size: 14px;
      font-weight: 600;
      color: #1F2937;
      margin: 0;
    }
    .card-body {
      padding: 0;
      font-size: 12px;
      color: #4B5563;
      line-height: 1.6;
    }

    /* ===== Callout Boxes ===== */
    .callout {
      border-radius: 6px;
      padding: 12px 16px;
      margin: 12px 0;
      font-size: 12px;
      line-height: 1.5;
      page-break-inside: avoid;
    }
    .callout-warning {
      background: #FEF2F2;
      border: 1px solid #DC2626;
      border-left: 4px solid #DC2626;
      color: #991B1B;
    }
    .callout-info {
      background: #eff6ff;
      border: 1px solid #2563eb;
      border-left: 4px solid #2563eb;
      color: #1e40af;
    }

    /* ===== Metadata Block ===== */
    .metadata-block {
      page-break-before: always;
      padding: 40px 20px;
      background: #F3F4F6;
      border: 1px solid #E5E7EB;
    }
    .metadata-table {
      width: 100%;
      border-collapse: collapse;
      margin: 16px 0;
      font-size: 12px;
    }
    .metadata-table td {
      padding: 6px 12px;
      border-bottom: 1px solid #E5E7EB;
    }
    .metadata-table td:first-child {
      font-weight: 600;
      color: #4B5563;
      width: 35%;
    }

    /* ===== Section Header ===== */
    .section-header {
      background: #111827;
      color: white;
      padding: 10px 20px;
      margin: 30px 0 15px;
      border-radius: 4px;
      -webkit-print-color-adjust: exact;
      print-color-adjust: exact;
    }
    .section-header h2 {
      margin: 0;
      font-size: 18px;
      color: white;
    }
    """;
}
