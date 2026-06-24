using AllSpice.CleanModularMonolith.Notifications.Application.Contracts.Services;
using AllSpice.CleanModularMonolith.Notifications.Infrastructure.Services.Email;

namespace AllSpice.CleanModularMonolith.Notifications.Application.UnitTests;

/// <summary>
/// D1: the From resolution and HTML-vs-text body selection shared by the MailKit/Resend/SendGrid senders
/// now live once in <see cref="EmailEnvelope.From"/>.
/// </summary>
public class EmailEnvelopeTests
{
    private static EmailMessage Message(bool isHtml = true, string? from = null)
        => new("to@example.com", "Subject", "BODY", IsHtml: isHtml, From: from);

    [Fact]
    public void Prefers_message_from_over_default()
    {
        var envelope = EmailEnvelope.From(Message(from: "override@example.com"), "default@example.com", "Sender", replyToAddress: null);

        Assert.Equal("override@example.com", envelope.FromAddress);
    }

    [Fact]
    public void Uses_default_from_when_message_has_none()
    {
        var envelope = EmailEnvelope.From(Message(from: null), "default@example.com", "Sender", replyToAddress: null);

        Assert.Equal("default@example.com", envelope.FromAddress);
    }

    [Fact]
    public void Html_message_sets_html_body_only()
    {
        var envelope = EmailEnvelope.From(Message(isHtml: true), "d@example.com", null, null);

        Assert.Equal("BODY", envelope.HtmlBody);
        Assert.Null(envelope.TextBody);
    }

    [Fact]
    public void Text_message_sets_text_body_only()
    {
        var envelope = EmailEnvelope.From(Message(isHtml: false), "d@example.com", null, null);

        Assert.Equal("BODY", envelope.TextBody);
        Assert.Null(envelope.HtmlBody);
    }

    [Fact]
    public void Carries_from_name_and_reply_to()
    {
        var envelope = EmailEnvelope.From(Message(), "d@example.com", "Sender Name", "reply@example.com");

        Assert.Equal("Sender Name", envelope.FromName);
        Assert.Equal("reply@example.com", envelope.ReplyToAddress);
    }
}
