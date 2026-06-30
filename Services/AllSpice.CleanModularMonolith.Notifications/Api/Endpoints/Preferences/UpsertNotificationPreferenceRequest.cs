namespace AllSpice.CleanModularMonolith.Notifications.Api.Endpoints.Preferences;

/// <summary>
/// Request payload for upserting a notification preference.
/// </summary>
public sealed class UpsertNotificationPreferenceRequest
{
    /// <summary>Local user UUID (User.Id — not the Keycloak external ID).</summary>
    public Guid UserId { get; set; }

    /// <summary>Notification channel name (e.g. Email, InApp).</summary>
    public string Channel { get; set; } = string.Empty;

    /// <summary>True to opt in; false to opt out.</summary>
    public bool IsEnabled { get; set; }
}
