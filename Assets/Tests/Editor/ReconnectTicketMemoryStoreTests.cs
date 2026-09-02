using EchoZone.Online.Reconnect;
using NUnit.Framework;

public sealed class ReconnectTicketMemoryStoreTests
{
    [SetUp]
    public void SetUp() => ReconnectTicketMemoryStore.Reset();

    [TearDown]
    public void TearDown() => ReconnectTicketMemoryStore.Reset();

    [Test]
    public void SameSession_NormalizesCodeAndKeepsTicket()
    {
        ReconnectTicketMemoryStore.PrepareForSession("room-a");
        ReconnectTicketMemoryStore.Replace("valid-ticket");
        ReconnectTicketMemoryStore.PrepareForSession(" ROOM-A ");
        Assert.That(ReconnectTicketMemoryStore.GetTicket(0), Is.EqualTo("valid-ticket"));
    }

    [Test]
    public void DifferentSession_DoesNotReusePreviousTicket()
    {
        ReconnectTicketMemoryStore.PrepareForSession("room-a");
        ReconnectTicketMemoryStore.Replace("valid-ticket");
        ReconnectTicketMemoryStore.PrepareForSession("room-b");
        Assert.That(ReconnectTicketMemoryStore.GetTicket(0), Is.Empty);
    }

    [Test]
    public void Retry_DoesNotExtendDisconnectedTicketLifetime()
    {
        ReconnectTicketMemoryStore.Replace("valid-ticket");
        ReconnectTicketMemoryStore.MarkDisconnected(10, 30);
        ReconnectTicketMemoryStore.MarkDisconnected(25, 30);
        Assert.That(ReconnectTicketMemoryStore.GetTicket(39), Is.EqualTo("valid-ticket"));
        Assert.That(ReconnectTicketMemoryStore.GetTicket(40), Is.Empty);
    }

    [Test]
    public void ServerIssuedReplacement_StartsNewTicketLifetime()
    {
        ReconnectTicketMemoryStore.Replace("old-ticket");
        ReconnectTicketMemoryStore.MarkDisconnected(0, 30);
        ReconnectTicketMemoryStore.Replace("new-ticket");
        Assert.That(ReconnectTicketMemoryStore.GetTicket(40), Is.EqualTo("new-ticket"));
    }

    [Test]
    public void Reset_ClearsTicketAcrossPlaySessions()
    {
        ReconnectTicketMemoryStore.Replace("valid-ticket");
        ReconnectTicketMemoryStore.Reset();
        Assert.That(ReconnectTicketMemoryStore.GetTicket(0), Is.Empty);
    }
}
