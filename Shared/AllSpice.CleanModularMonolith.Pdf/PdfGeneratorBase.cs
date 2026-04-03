using System.Runtime.InteropServices;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace AllSpice.CleanModularMonolith.Pdf;

/// <summary>
/// Abstract base class for PDF generators using PuppeteerSharp (headless Chromium).
/// Provides shared browser management, PDF generation, and utility methods.
/// </summary>
public abstract class PdfGeneratorBase
{
    private static string? _cachedExecutablePath;
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    protected const string BrowserPathEnv = "PUPPETEER_EXECUTABLE_PATH";
    protected const string PuppeteerPathEnv = "PUPPETEER_CACHE_DIR";
    protected const string DisableDownloadEnv = "PUPPETEER_DISABLE_DOWNLOAD";

    /// <summary>
    /// Generates a PDF from HTML content with standard A4 page settings.
    /// </summary>
    protected async Task<byte[]> GeneratePdfAsync(
        string html,
        bool waitForCharts = false)
    {
        var executablePath = await EnsureBrowserAsync();
        await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true,
            ExecutablePath = executablePath,
            Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
        });

#if DEBUG
        var previewPath = Environment.GetEnvironmentVariable("PDF_PREVIEW");
        if (!string.IsNullOrEmpty(previewPath))
        {
            var tempRoot = Path.GetTempPath();
            var fullPath = Path.GetFullPath(previewPath);
            if (fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllTextAsync(fullPath, html);
            }
        }
#endif

        await using var page = await browser.NewPageAsync();
        await page.SetViewportAsync(new ViewPortOptions { Width = 794, Height = 1123 });
        await page.SetContentAsync(html, new NavigationOptions
        {
            WaitUntil = [WaitUntilNavigation.Networkidle0]
        });

        if (waitForCharts)
        {
            try
            {
                await page.WaitForSelectorAsync(
                    ".echart-container canvas",
                    new WaitForSelectorOptions { Timeout = 10000 });
            }
            catch { /* No charts present or timeout */ }

            try
            {
                await page.WaitForFunctionAsync(
                    "() => !document.querySelector('pre.mermaid')",
                    new WaitForFunctionOptions { Timeout = 10000 });
            }
            catch { /* Mermaid CDN unavailable */ }
        }

        return await page.PdfDataAsync(new PdfOptions
        {
            Format = PaperFormat.A4,
            PrintBackground = true,
            MarginOptions = new MarginOptions
            {
                Top = "0",
                Bottom = "0",
                Left = "0",
                Right = "0"
            }
        });
    }

    /// <summary>HTML-encode a string for safe embedding.</summary>
    protected static string Encode(string? value) =>
        System.Net.WebUtility.HtmlEncode(value ?? "");

    /// <summary>
    /// Ensures a Chromium browser is available for PDF rendering.
    /// Checks env vars first, then known system paths, then downloads.
    /// </summary>
    protected static async Task<string> EnsureBrowserAsync()
    {
        if (_cachedExecutablePath is not null)
            return _cachedExecutablePath;

        await _downloadLock.WaitAsync();
        try
        {
            if (_cachedExecutablePath is not null)
                return _cachedExecutablePath;

            var envPath = Environment.GetEnvironmentVariable(BrowserPathEnv)
                          ?? Environment.GetEnvironmentVariable(PuppeteerPathEnv);
            if (!string.IsNullOrWhiteSpace(envPath) && File.Exists(envPath))
            {
                _cachedExecutablePath = envPath;
                return envPath;
            }

            foreach (var knownPath in GetKnownBrowserPaths())
            {
                if (File.Exists(knownPath))
                {
                    _cachedExecutablePath = knownPath;
                    return knownPath;
                }
            }

            var disableDownload = Environment.GetEnvironmentVariable(DisableDownloadEnv);
            if (string.Equals(disableDownload, "true", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Browser download disabled via {DisableDownloadEnv}. " +
                    $"Set {BrowserPathEnv} or {PuppeteerPathEnv} to a valid Chromium executable path.");
            }

            var fetcher = new BrowserFetcher(new BrowserFetcherOptions
            {
                Platform = RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? PuppeteerSharp.Platform.Linux
                    : PuppeteerSharp.Platform.Win64
            });

            var revisionInfo = await fetcher.DownloadAsync();
            _cachedExecutablePath = revisionInfo.GetExecutablePath();
            return _cachedExecutablePath;
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    private static IEnumerable<string> GetKnownBrowserPaths()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            yield return Path.Combine(local, "Google", "Chrome", "Application", "chrome.exe");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Edge", "Application", "msedge.exe");
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            yield return "/usr/bin/google-chrome";
            yield return "/usr/bin/chromium";
        }
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            yield return "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome";
    }
}
