using System.Text.Json;

namespace MaskilForge.Api;

public sealed class DevelopmentActivityLogStore
{
    public const int DefaultMaximumSessions = 16;
    public const int DefaultMaximumEntriesPerSession = 1_000;

    private static readonly HashSet<string> AllowedLevels =
        ["info", "success", "warning", "error"];
    private static readonly HashSet<string> AllowedDeviceKinds =
        ["phone", "tablet", "desktop"];

    private readonly object _gate = new();
    private readonly Dictionary<Guid, SessionState> _sessions = [];
    private readonly int _maximumSessions;
    private readonly int _maximumEntriesPerSession;

    public DevelopmentActivityLogStore(
        int maximumSessions = DefaultMaximumSessions,
        int maximumEntriesPerSession = DefaultMaximumEntriesPerSession)
    {
        if (maximumSessions <= 0) throw new ArgumentOutOfRangeException(nameof(maximumSessions));
        if (maximumEntriesPerSession <= 0) throw new ArgumentOutOfRangeException(nameof(maximumEntriesPerSession));
        _maximumSessions = maximumSessions;
        _maximumEntriesPerSession = maximumEntriesPerSession;
    }

    public void Append(DevelopmentActivityLogSubmission submission, DateTimeOffset receivedUtc)
    {
        ArgumentNullException.ThrowIfNull(submission);
        if (submission.SessionId == Guid.Empty) throw new ArgumentException("A development log session ID is required.");
        if (!AllowedDeviceKinds.Contains(submission.DeviceKind)) throw new ArgumentException("Development log device kind is invalid.");
        if (submission.ViewportWidth is < 1 or > 20_000 || submission.ViewportHeight is < 1 or > 20_000)
            throw new ArgumentOutOfRangeException(nameof(submission), "Development log viewport dimensions are invalid.");
        if (submission.Entries.Count is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(submission), "A development log batch must contain between 1 and 100 entries.");

        var entries = submission.Entries.Select(ValidateAndClone).ToList();
        lock (_gate)
        {
            if (!_sessions.TryGetValue(submission.SessionId, out var session))
            {
                MakeRoomForSession();
                session = new SessionState(
                    submission.SessionId,
                    submission.DeviceKind,
                    submission.ViewportWidth,
                    submission.ViewportHeight,
                    submission.Standalone,
                    receivedUtc);
                _sessions.Add(submission.SessionId, session);
            }

            session.DeviceKind = submission.DeviceKind;
            session.ViewportWidth = submission.ViewportWidth;
            session.ViewportHeight = submission.ViewportHeight;
            session.Standalone = submission.Standalone;
            session.LastSeenUtc = receivedUtc;
            foreach (var entry in entries)
            {
                session.LastSequence += 1;
                session.Entries.Add(new DevelopmentActivityLogEntry(
                    session.LastSequence,
                    entry.Id,
                    entry.Timestamp,
                    entry.Level,
                    entry.Action,
                    entry.Message,
                    entry.Details));
            }

            if (session.Entries.Count > _maximumEntriesPerSession)
                session.Entries.RemoveRange(0, session.Entries.Count - _maximumEntriesPerSession);
        }
    }

    public IReadOnlyList<DevelopmentActivityLogSessionSummary> ListSessions()
    {
        lock (_gate)
        {
            return _sessions.Values
                .OrderByDescending(session => session.LastSeenUtc)
                .Select(ToSummary)
                .ToList();
        }
    }

    public DevelopmentActivityLogSession? ReadSession(Guid sessionId)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out var session)) return null;
            return new DevelopmentActivityLogSession(
                ToSummary(session),
                session.Entries.ToList());
        }
    }

    public bool RemoveSession(Guid sessionId)
    {
        lock (_gate)
        {
            return _sessions.Remove(sessionId);
        }
    }

    private DevelopmentActivityLogClientEntry ValidateAndClone(DevelopmentActivityLogClientEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (entry.Id == Guid.Empty) throw new ArgumentException("A development log entry ID is required.");
        if (!AllowedLevels.Contains(entry.Level)) throw new ArgumentException("Development log level is invalid.");
        if (string.IsNullOrWhiteSpace(entry.Action) || entry.Action.Length > 160)
            throw new ArgumentException("Development log action is required and cannot exceed 160 characters.");
        if (string.IsNullOrWhiteSpace(entry.Message) || entry.Message.Length > 4_000)
            throw new ArgumentException("Development log message is required and cannot exceed 4,000 characters.");
        if (entry.Timestamp == default) throw new ArgumentException("A development log timestamp is required.");
        if (entry.Details is { } details && details.GetRawText().Length > 16_000)
            throw new ArgumentException("Development log details cannot exceed 16,000 characters.");

        return entry with { Details = entry.Details?.Clone() };
    }

    private void MakeRoomForSession()
    {
        if (_sessions.Count < _maximumSessions) return;
        var oldest = _sessions.Values.MinBy(session => session.LastSeenUtc)!;
        _sessions.Remove(oldest.SessionId);
    }

    private static DevelopmentActivityLogSessionSummary ToSummary(SessionState session) => new(
        session.SessionId,
        session.DeviceKind,
        session.ViewportWidth,
        session.ViewportHeight,
        session.Standalone,
        session.StartedUtc,
        session.LastSeenUtc,
        session.Entries.Count);

    private sealed class SessionState(
        Guid sessionId,
        string deviceKind,
        int viewportWidth,
        int viewportHeight,
        bool standalone,
        DateTimeOffset startedUtc)
    {
        public Guid SessionId { get; } = sessionId;
        public string DeviceKind { get; set; } = deviceKind;
        public int ViewportWidth { get; set; } = viewportWidth;
        public int ViewportHeight { get; set; } = viewportHeight;
        public bool Standalone { get; set; } = standalone;
        public DateTimeOffset StartedUtc { get; } = startedUtc;
        public DateTimeOffset LastSeenUtc { get; set; } = startedUtc;
        public long LastSequence { get; set; }
        public List<DevelopmentActivityLogEntry> Entries { get; } = [];
    }
}

public sealed record DevelopmentActivityLogSubmission(
    Guid SessionId,
    string DeviceKind,
    int ViewportWidth,
    int ViewportHeight,
    bool Standalone,
    IReadOnlyList<DevelopmentActivityLogClientEntry> Entries);

public sealed record DevelopmentActivityLogClientEntry(
    Guid Id,
    DateTimeOffset Timestamp,
    string Level,
    string Action,
    string Message,
    JsonElement? Details = null);

public sealed record DevelopmentActivityLogEntry(
    long Sequence,
    Guid Id,
    DateTimeOffset Timestamp,
    string Level,
    string Action,
    string Message,
    JsonElement? Details = null);

public sealed record DevelopmentActivityLogSessionSummary(
    Guid SessionId,
    string DeviceKind,
    int ViewportWidth,
    int ViewportHeight,
    bool Standalone,
    DateTimeOffset StartedUtc,
    DateTimeOffset LastSeenUtc,
    int EntryCount);

public sealed record DevelopmentActivityLogSession(
    DevelopmentActivityLogSessionSummary Session,
    IReadOnlyList<DevelopmentActivityLogEntry> Entries);
