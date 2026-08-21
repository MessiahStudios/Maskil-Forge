using System.Text.Json;
using MaskilForge.Api;

namespace MaskilForge.Engine.Tests;

public sealed class DevelopmentActivityLogStoreTests
{
    [Fact]
    public void Append_ExposesBoundedTransientDeviceSessions()
    {
        var store = new DevelopmentActivityLogStore(maximumSessions: 2, maximumEntriesPerSession: 2);
        var phoneId = Guid.NewGuid();
        var started = new DateTimeOffset(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);

        store.Append(Submission(phoneId, "phone", Entry("first"), Entry("second"), Entry("third")), started);

        var phone = store.ReadSession(phoneId);
        Assert.NotNull(phone);
        Assert.Equal("phone", phone.Session.DeviceKind);
        Assert.Equal(390, phone.Session.ViewportWidth);
        Assert.Equal(2, phone.Session.EntryCount);
        Assert.Equal(["second", "third"], phone.Entries.Select(entry => entry.Message));
        Assert.Equal([2L, 3L], phone.Entries.Select(entry => entry.Sequence));

        var tabletId = Guid.NewGuid();
        var desktopId = Guid.NewGuid();
        store.Append(Submission(tabletId, "tablet", Entry("tablet")), started.AddSeconds(1));
        store.Append(Submission(desktopId, "desktop", Entry("desktop")), started.AddSeconds(2));

        Assert.Null(store.ReadSession(phoneId));
        Assert.Equal([desktopId, tabletId], store.ListSessions().Select(session => session.SessionId));
    }

    [Fact]
    public void Append_RejectsUnboundedOrUnrecognizedTelemetry()
    {
        var store = new DevelopmentActivityLogStore();
        var sessionId = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => store.Append(
            Submission(sessionId, "headset", Entry("event")), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => store.Append(
            Submission(sessionId, "phone", Entry("event") with { Level = "trace" }), DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => store.Append(
            Submission(sessionId, "phone", Entry(new string('x', 4_001))), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void RemoveSession_DropsTelemetryAndAllowsTheDeviceToReconnect()
    {
        var store = new DevelopmentActivityLogStore();
        var sessionId = Guid.NewGuid();
        var received = new DateTimeOffset(2026, 8, 20, 20, 0, 0, TimeSpan.Zero);
        store.Append(Submission(sessionId, "phone", Entry("before clear")), received);

        Assert.True(store.RemoveSession(sessionId));
        Assert.Null(store.ReadSession(sessionId));
        Assert.DoesNotContain(store.ListSessions(), session => session.SessionId == sessionId);

        store.Append(Submission(sessionId, "phone", Entry("after clear")), received.AddSeconds(1));
        Assert.Equal(["after clear"], store.ReadSession(sessionId)!.Entries.Select(entry => entry.Message));
        Assert.False(store.RemoveSession(Guid.NewGuid()));
    }

    private static DevelopmentActivityLogSubmission Submission(
        Guid sessionId,
        string deviceKind,
        params DevelopmentActivityLogClientEntry[] entries) => new(
            sessionId,
            deviceKind,
            390,
            844,
            false,
            entries);

    private static DevelopmentActivityLogClientEntry Entry(string message)
    {
        using var details = JsonDocument.Parse("""{"trackCount":1}""");
        return new DevelopmentActivityLogClientEntry(
            Guid.NewGuid(),
            new DateTimeOffset(2026, 8, 20, 20, 0, 0, TimeSpan.Zero),
            "info",
            "vocal.preflight",
            message,
            details.RootElement.Clone());
    }
}
