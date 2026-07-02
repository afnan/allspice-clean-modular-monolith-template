using System.Runtime.InteropServices;
using PuppeteerSharp;
using PuppeteerSharp.Media;

namespace AllSpice.CleanModularMonolith.Pdf;

/// <summary>
/// Abstract base class for PDF generators using PuppeteerSharp (headless Chromium).
/// Provides shared browser management, PDF generation, and utility methods.
/// </summary>
public abstract class PdfGeneratorBase : IAsyncDisposable
{
    private static string? _cachedExecutablePath;
    private static readonly SemaphoreSlim _downloadLock = new(1, 1);

    // A single long-lived Chromium instance is launched lazily and REUSED across PDF calls (one NewPageAsync
    // per PDF, the page disposed each call) — launching a full browser per PDF is expensive. The browser is
    // per-generator-instance and released via IAsyncDisposable on shutdown, so register concrete generators as
    // singletons to actually reuse it across requests. _browserLock guards launch/relaunch only; page creation
    // is concurrent (Chromium supports many isolated pages on one browser).
    private readonly SemaphoreSlim _browserLock = new(1, 1);
    private IBrowser? _browser;
    private bool _disposed;

    protected const string BrowserPathEnv = "PUPPETEER_EXECUTABLE_PATH";
    protected const string PuppeteerPathEnv = "PUPPETEER_CACHE_DIR";
    protected const string DisableDownloadEnv = "PUPPETEER_DISABLE_DOWNLOAD";

    // Awaits web fonts and any still-loading images before the PDF snapshot. Each image resolves on
    // load/error or a 5s cap so a broken/hanging resource can't stall rendering; runs in the page context.
    private const string ResourceSettleScript = @"
        async () => {
            if (document.fonts && document.fonts.ready) {
                try { await document.fonts.ready; } catch (e) { }
            }
            const pending = Array.from(document.images).filter(img => !img.complete);
            await Promise.all(pending.map(img => new Promise(resolve => {
                img.addEventListener('load', () => resolve(), { once: true });
                img.addEventListener('error', () => resolve(), { once: true });
                setTimeout(() => resolve(), 5000);
            })));
        }";

    /// <summary>
    /// Generates a PDF from HTML content with standard A4 page settings.
    /// </summary>
    protected async Task<byte[]> GeneratePdfAsync(
        string html,
        bool waitForCharts = false)
    {
        var browser = await GetBrowserAsync();

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
        // PuppeteerSharp 25 deprecated SetContentAsync(string, NavigationOptions): the networkidle wait
        // conditions never worked reliably with SetContent. Wait for the load event instead; the explicit
        // chart/mermaid waits below handle settling of dynamically-rendered content.
        await page.SetContentAsync(html, new SetContentOptions
        {
            WaitUntil = [WaitUntilNavigation.Load]
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

        // Settle late-loading resources the `load` event doesn't cover: web fonts (so @font-face text isn't
        // captured in a fallback face) and images still in flight after load (e.g. logo/chart images fetched
        // by script). Each image is individually time-boxed so a broken or hanging resource can't stall
        // rendering, and the whole step is best-effort. A stronger guarantee than SetContent's
        // (never-reliable) networkidle wait ever provided.
        try
        {
            await page.EvaluateFunctionAsync(ResourceSettleScript);
        }
        catch { /* best-effort: render with whatever has loaded */ }

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

    /// <summary>
    /// Lazily launches and returns the shared Chromium instance, relaunching it if a previous instance died
    /// (crashed / was disconnected). Thread-safe: concurrent first-callers are serialized on <c>_browserLock</c>,
    /// after which only one launch happens and subsequent calls reuse the connected browser.
    /// </summary>
    private async Task<IBrowser> GetBrowserAsync()
    {
        var existing = _browser;
        if (existing is { IsConnected: true })
        {
            return existing;
        }

        await _browserLock.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            if (_browser is { IsConnected: true })
            {
                return _browser;
            }

            // Discard a dead/disconnected browser before relaunching.
            if (_browser is not null)
            {
                try { await _browser.DisposeAsync(); }
                catch { /* best-effort cleanup of an already-dead browser */ }
                _browser = null;
            }

            var executablePath = await EnsureBrowserAsync();
            _browser = await Puppeteer.LaunchAsync(new LaunchOptions
            {
                Headless = true,
                ExecutablePath = executablePath,
                Args = ["--no-sandbox", "--disable-setuid-sandbox", "--disable-dev-shm-usage"]
            });
            return _browser;
        }
        finally
        {
            _browserLock.Release();
        }
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

            // PUPPETEER_EXECUTABLE_PATH points DIRECTLY at a Chromium/Chrome binary — validate it as a file.
            var explicitPath = Environment.GetEnvironmentVariable(BrowserPathEnv);
            if (!string.IsNullOrWhiteSpace(explicitPath) && File.Exists(explicitPath))
            {
                _cachedExecutablePath = explicitPath;
                return explicitPath;
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
                    $"Set {BrowserPathEnv} to a Chromium executable, or {PuppeteerPathEnv} to a cache " +
                    "directory that contains (or may receive) a downloaded browser.");
            }

            // PUPPETEER_CACHE_DIR is a DIRECTORY, not an executable (the previous File.Exists check on it was a
            // dead branch that never matched). Point the fetcher at it so the browser is located there — and
            // downloaded there only if absent, since DownloadAsync is a no-op when the platform build is already
            // cached. This gives a correct "locate-or-download" against the cache directory.
            var cacheDir = Environment.GetEnvironmentVariable(PuppeteerPathEnv);
            var fetcherOptions = new BrowserFetcherOptions { Platform = GetDownloadPlatform() };
            if (!string.IsNullOrWhiteSpace(cacheDir))
            {
                Directory.CreateDirectory(cacheDir);
                fetcherOptions.Path = cacheDir;
            }

            var fetcher = new BrowserFetcher(fetcherOptions);
            var installedBrowser = await fetcher.DownloadAsync();
            _cachedExecutablePath = installedBrowser.GetExecutablePath();
            return _cachedExecutablePath;
        }
        finally
        {
            _downloadLock.Release();
        }
    }

    /// <summary>
    /// Selects the browser build to download for the current OS/architecture. The previous logic yielded
    /// <c>Win64</c> for anything that wasn't Linux, which produced an unusable Windows build on macOS.
    /// </summary>
    private static PuppeteerSharp.Platform GetDownloadPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return PuppeteerSharp.Platform.Linux;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? PuppeteerSharp.Platform.MacOSArm64
                : PuppeteerSharp.Platform.MacOS;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.Is64BitOperatingSystem
                ? PuppeteerSharp.Platform.Win64
                : PuppeteerSharp.Platform.Win32;
        }

        // Any other/unknown OS: Linux is the most portable managed build target.
        return PuppeteerSharp.Platform.Linux;
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

    /// <summary>
    /// Releases the shared Chromium instance (and the launch lock) on shutdown. Best-effort: shutdown must not
    /// throw over a browser that may already have exited.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;

        await _browserLock.WaitAsync();
        try
        {
            if (_browser is not null)
            {
                try { await _browser.DisposeAsync(); }
                catch { /* best-effort: the browser process may already be gone */ }
                _browser = null;
            }
        }
        finally
        {
            _browserLock.Release();
        }

        _browserLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
