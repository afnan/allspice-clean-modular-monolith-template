using AllSpice.CleanModularMonolith.Identity.Domain.Aggregates.Invitation;
using AllSpice.CleanModularMonolith.Identity.Domain.Enums;
using AllSpice.CleanModularMonolith.Identity.Domain.Events;

namespace AllSpice.CleanModularMonolith.Identity.Domain.UnitTests;

public class InvitationTests
{
    [Fact]
    public void Create_SetsAllPropertiesAndFiresDomainEvent()
    {
        var invitation = Invitation.Create(
            "test@example.com", "John", "Doe", "Admin",
            "kc-123", Guid.NewGuid(), "creator-1", "TempPass123!");

        Assert.Equal("test@example.com", invitation.Email);
        Assert.Equal("John", invitation.FirstName);
        Assert.Equal("Doe", invitation.LastName);
        Assert.Equal("Admin", invitation.Role);
        Assert.Equal("kc-123", invitation.KeycloakUserId);
        Assert.Equal(InvitationStatus.Pending, invitation.Status);
        Assert.NotEqual(Guid.Empty, invitation.Token);
        Assert.True(invitation.ExpiresAtUtc > DateTimeOffset.UtcNow);

        var domainEvent = Assert.Single(invitation.DomainEvents);
        var createdEvent = Assert.IsType<InvitationCreatedDomainEvent>(domainEvent);
        Assert.Equal("test@example.com", createdEvent.Email);
        Assert.Equal("TempPass123!", createdEvent.TempPassword);
    }

    [Fact]
    public void MarkAccepted_ChangesStatus()
    {
        var invitation = CreateTestInvitation();

        invitation.MarkAccepted();

        Assert.Equal(InvitationStatus.Accepted, invitation.Status);
    }

    [Fact]
    public void Resend_ExtendsExpiry_WhenPending()
    {
        var invitation = CreateTestInvitation();
        var originalExpiry = invitation.ExpiresAtUtc;

        invitation.Resend();

        Assert.True(invitation.ExpiresAtUtc >= originalExpiry);
    }

    [Fact]
    public void Resend_Throws_WhenNotPending()
    {
        var invitation = CreateTestInvitation();
        invitation.MarkAccepted();

        Assert.Throws<ArgumentException>(() => invitation.Resend());
    }

    [Fact]
    public void IsExpired_ReturnsFalse_WhenFresh()
    {
        var invitation = CreateTestInvitation();
        Assert.False(invitation.IsExpired());
    }

    [Fact]
    public void Create_Throws_WhenEmailEmpty()
    {
        Assert.Throws<ArgumentException>(() =>
            Invitation.Create("", "John", "Doe", "Admin", "kc-1", Guid.NewGuid(), "c", "pw"));
    }

    [Fact]
    public void Create_NormalizesEmail()
    {
        var invitation = Invitation.Create(
            "  TEST@EXAMPLE.COM  ", "John", "Doe", "Admin",
            "kc-1", Guid.NewGuid(), "c", "pw");

        Assert.Equal("test@example.com", invitation.Email);
    }

    private static Invitation CreateTestInvitation() =>
        Invitation.Create("test@example.com", "John", "Doe", "Admin",
            "kc-123", Guid.NewGuid(), "creator-1", "TempPass123!");
}
