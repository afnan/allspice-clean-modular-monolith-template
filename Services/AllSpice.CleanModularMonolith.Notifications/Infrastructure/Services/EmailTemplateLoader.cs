using System.Reflection;

namespace AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services;

/// <summary>
/// Loads HTML email templates from embedded resources and merges them with the layout.
/// Templates are stored in Infrastructure/Templates/ as .html files compiled as embedded resources.
/// </summary>
public static class EmailTemplateLoader
{
    private const string ResourcePrefix = "AllSpice.CleanModularMonolith.Notifications.Infrastructure.Templates.";
    private static readonly Assembly TemplateAssembly = typeof(EmailTemplateLoader).Assembly;

    /// <summary>
    /// Loads a named template and wraps it in the _Layout.html layout.
    /// </summary>
    /// <param name="templateName">Template name without extension (e.g. "invitation-created").</param>
    /// <returns>The fully rendered HTML with the template content merged into the layout.</returns>
    public static string LoadTemplate(string templateName)
    {
        var layout = ReadResource("_Layout.html");
        var content = ReadResource($"{templateName}.html");

        return layout.Replace("{{content}}", content);
    }

    /// <summary>
    /// Loads a template without the layout wrapper.
    /// </summary>
    /// <param name="templateName">Template name without extension.</param>
    /// <returns>The raw template HTML.</returns>
    public static string LoadRawTemplate(string templateName)
    {
        return ReadResource($"{templateName}.html");
    }

    private static string ReadResource(string fileName)
    {
        // Embedded resources use dots for path separators and hyphens become hyphens
        var resourceName = $"{ResourcePrefix}{fileName}";

        using var stream = TemplateAssembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            throw new InvalidOperationException(
                $"Email template resource '{resourceName}' not found. " +
                $"Available resources: {string.Join(", ", TemplateAssembly.GetManifestResourceNames())}");
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
