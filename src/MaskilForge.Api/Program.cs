using System.Text.Json.Serialization;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IProjectRepository>(_ =>
    new JsonFileProjectRepository(Path.Combine(builder.Environment.ContentRootPath, "App_Data", "projects")));
builder.Services.AddSingleton<ProjectWorkspace>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ProjectPersistenceException exception)
    {
        context.Response.StatusCode = exception switch
        {
            StaleProjectSessionException => StatusCodes.Status409Conflict,
            ProjectSaveException => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status422UnprocessableEntity
        };
        await context.Response.WriteAsJsonAsync(
            new ApiError(exception.Message, exception.Code, exception.RecoveryCopyFileName),
            context.RequestAborted);
    }
});
app.UseCors();

app.MapGet("/api/projects", async (IProjectRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.ListAsync(cancellationToken)));

app.MapGet("/api/recovery", async (IProjectRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.ListRecoverySnapshotsAsync(cancellationToken)));

app.MapGet("/api/recovery/{id}", async (string id, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    var snapshot = await workspace.LoadRecoveryAsync(projectId, cancellationToken);
    return snapshot is null
        ? Results.NotFound(new ApiError("Recovery snapshot not found."))
        : Results.Ok(new RecoveryProjectResponse(
            snapshot.Project,
            snapshot.CapturedAtUtc,
            snapshot.BaseProjectLastModifiedUtc));
});

app.MapPut("/api/projects/{id}/recovery", async (
    string id,
    RecoverySnapshotRequest request,
    IProjectRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || projectId != request.Project.Id)
        return Results.BadRequest(new ApiError("Route and project IDs must match."));
    if (string.IsNullOrWhiteSpace(request.SessionId))
        return Results.BadRequest(new ApiError("A recovery session ID is required."));
    await repository.SaveRecoverySnapshotAsync(new ProjectRecoverySnapshot(
        request.Project,
        DateTimeOffset.UtcNow,
        request.BaseProjectLastModifiedUtc,
        request.SessionId), cancellationToken);
    return Results.NoContent();
});

app.MapDelete("/api/recovery/{id}", async (string id, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    return await repository.DeleteRecoverySnapshotAsync(projectId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound(new ApiError("Recovery snapshot not found."));
});

app.MapDelete("/api/projects/{id}", async (string id, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    return await repository.MoveToTrashAsync(projectId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound(new ApiError("Project not found."));
});

app.MapGet("/api/trash", async (IProjectRepository repository, CancellationToken cancellationToken) =>
    Results.Ok(await repository.ListTrashAsync(cancellationToken)));

app.MapPost("/api/trash/{id}/restore", async (string id, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    try
    {
        return await repository.RestoreFromTrashAsync(projectId, cancellationToken)
            ? Results.NoContent()
            : Results.NotFound(new ApiError("Trashed project not found."));
    }
    catch (InvalidOperationException exception) { return Results.Conflict(new ApiError(exception.Message)); }
});

app.MapDelete("/api/trash/{id}", async (string id, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    return await repository.PermanentlyDeleteAsync(projectId, cancellationToken)
        ? Results.NoContent()
        : Results.NotFound(new ApiError("Trashed project not found."));
});

app.MapPost("/api/projects", async (CreateProjectRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    try
    {
        var editor = await workspace.CreateAsync(request.Title, cancellationToken);
        return Results.Created($"/api/projects/{editor.Project.Id}", ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Validation(exception);
    }
});

app.MapGet("/api/projects/{id}", async (string id, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    var editor = await workspace.LoadFromStorageAsync(projectId, cancellationToken);
    return editor is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(ProjectResponse.From(editor));
});

app.MapPut("/api/projects/{id}", async (string id, UpdateProjectRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || projectId != request.Project.Id)
        return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var editor = await workspace.UpdateAsync(request.Project, request.BaseProjectLastModifiedUtc, cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/commands", async (string id, ProjectCommandRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project is not null && request.Project.Id != projectId)
        return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var editor = request.Project is null
        ? await workspace.GetAsync(projectId, cancellationToken)
        : await workspace.SyncAsync(request.Project, cancellationToken);
    if (editor is null) return Results.NotFound(new ApiError("Project not found."));
    try
    {
        ApplyRequest(editor, request);
        return Results.Ok(ProjectResponse.From(editor));
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/prosody-score", async (string id, ProsodyScoreRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var editor = await workspace.SyncAsync(request.Project, cancellationToken);
    if (editor is null) return Results.NotFound(new ApiError("Project not found."));
    try
    {
        var score = request.RhythmCandidateId is null
            ? ProsodyScorer.ScoreActivePhrase(
                editor.Project,
                request.SectionId,
                request.LineId,
                request.PhraseId)
            : ProsodyScorer.ScoreRhythmCandidate(
                editor.Project,
                request.SectionId,
                request.LineId,
                request.RhythmCandidateId.Value);
        return Results.Ok(score);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/lyric-timeline", async (string id, LyricTimelineRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var editor = await workspace.SyncAsync(request.Project, cancellationToken);
    if (editor is null) return Results.NotFound(new ApiError("Project not found."));
    try
    {
        return Results.Ok(LyricTimelineProjector.Project(editor.Project, request.RhythmCandidateId));
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/undo", async (string id, EditorStateRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var editor = await workspace.SyncAsync(request.Project, cancellationToken);
    if (editor is null) return Results.NotFound(new ApiError("Project not found."));
    if (!editor.Undo()) return Results.Conflict(new ApiError("Nothing to undo."));
    return Results.Ok(ProjectResponse.From(editor));
});

app.MapPost("/api/projects/{id}/redo", async (string id, EditorStateRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var editor = await workspace.SyncAsync(request.Project, cancellationToken);
    if (editor is null) return Results.NotFound(new ApiError("Project not found."));
    if (!editor.Redo()) return Results.Conflict(new ApiError("Nothing to redo."));
    return Results.Ok(ProjectResponse.From(editor));
});

app.Run();

static void ApplyRequest(ProjectEditor editor, ProjectCommandRequest request)
{
    var project = editor.Project;
    switch (request.Type.Trim().ToLowerInvariant())
    {
        case "rename-project": project.Rename(Required(request.Title, "title")); break;
        case "set-tempo": project.SetTempo(request.Tempo ?? throw new ArgumentException("Tempo is required.")); break;
        case "set-time-signature": project.SetTimeSignature(
            request.Numerator ?? throw new ArgumentException("Numerator is required."),
            request.Denominator ?? throw new ArgumentException("Denominator is required.")); break;
        case "add-section": editor.Execute(new AddSectionCommand(
            request.Kind ?? throw new ArgumentException("Section kind is required."), request.Title)); break;
        case "rename-section": editor.Execute(new RenameSectionCommand(RequiredSectionId(request), Required(request.Title, "title"))); break;
        case "move-section": editor.Execute(new MoveSectionCommand(RequiredSectionId(request), request.TargetIndex ?? throw new ArgumentException("Target index is required."))); break;
        case "set-section-duration": editor.Execute(new SetSectionDurationCommand(
            RequiredSectionId(request),
            request.DurationBars ?? throw new ArgumentException("Section duration is required."))); break;
        case "remove-section": editor.Execute(new RemoveSectionCommand(RequiredSectionId(request))); break;
        case "set-lyrics":
            var section = project.FindSection(RequiredSectionId(request));
            if (section.LyricLines.Any(line => project.IsLyricLineLocked(line.Id)))
                throw new InvalidOperationException("Unlock locked lyric lines in this section before replacing all lyrics.");
            section.SetLyricLines((request.Lyrics ?? []).Select(LyricLine.Create));
            project.ReconcileLocks();
            project.Touch();
            break;
        case "edit-lyric-line":
            var editLineId = request.LineId ?? throw new ArgumentException("Lyric line ID is required.");
            project.EnsureLyricLineUnlocked(editLineId);
            project.FindSection(RequiredSectionId(request)).EditLyricLine(
                editLineId,
                request.Text ?? throw new ArgumentException("Lyric text is required."));
            project.ReconcileLocks();
            project.Touch();
            break;
        case "set-word-syllables":
            var syllableLineId = request.LineId ?? throw new ArgumentException("Lyric line ID is required.");
            project.EnsureLyricLineUnlocked(syllableLineId);
            project.FindSection(RequiredSectionId(request))
                .FindLyricLine(syllableLineId)
                .SetSyllables(
                    request.WordId ?? throw new ArgumentException("Lyric word ID is required."),
                    request.Syllables ?? throw new ArgumentException("Syllables are required."));
            project.ReconcileLocks();
            project.Touch();
            break;
        case "set-syllable-stress":
            editor.Execute(new SetSyllableStressCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.WordId ?? throw new ArgumentException("Lyric word ID is required."),
                request.SyllableId ?? throw new ArgumentException("Syllable ID is required."),
                request.StressLevel));
            break;
        case "set-prosodic-weight":
            editor.Execute(new SetProsodicWeightCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.PhraseId ?? throw new ArgumentException("Lyric phrase ID is required."),
                request.SyllableId ?? throw new ArgumentException("Syllable ID is required."),
                request.ProsodicWeight));
            break;
        case "set-syllable-placement":
            editor.Execute(new SetSyllablePlacementCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.SyllableId ?? throw new ArgumentException("Syllable ID is required."),
                request.BeatPosition));
            break;
        case "capture-rhythm-candidate":
            editor.Execute(new CaptureRhythmCandidateCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.PhraseId ?? throw new ArgumentException("Lyric phrase ID is required."),
                Required(request.CandidateLabel, "Rhythm option label")));
            break;
        case "rename-rhythm-candidate":
            editor.Execute(new RenameRhythmCandidateCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.RhythmCandidateId ?? throw new ArgumentException("Rhythm candidate ID is required."),
                Required(request.CandidateLabel, "Rhythm option label")));
            break;
        case "remove-rhythm-candidate":
            editor.Execute(new RemoveRhythmCandidateCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.RhythmCandidateId ?? throw new ArgumentException("Rhythm candidate ID is required.")));
            break;
        case "apply-rhythm-candidate":
            editor.Execute(new ApplyRhythmCandidateCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.RhythmCandidateId ?? throw new ArgumentException("Rhythm candidate ID is required.")));
            break;
        case "set-breath-point":
            editor.Execute(new SetBreathPointCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.SyllableId ?? throw new ArgumentException("Syllable ID is required."),
                request.BreathPresent ?? throw new ArgumentException("Breath present is required.")));
            break;
        case "lock-lyric-line":
            editor.Execute(new LockLyricLineCommand(
                request.LineId ?? throw new ArgumentException("Lyric line ID is required.")));
            break;
        case "lock-phrase-rhythm":
            editor.Execute(new LockPhraseRhythmCommand(
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.PhraseId ?? throw new ArgumentException("Lyric phrase ID is required.")));
            break;
        case "unlock-creative-lock":
            editor.Execute(new UnlockCreativeLockCommand(
                request.CreativeLockId ?? throw new ArgumentException("Creative lock ID is required.")));
            break;
        case "split-lyric-phrase":
            editor.Execute(new SplitLyricPhraseCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.WordId ?? throw new ArgumentException("Lyric word ID is required.")));
            break;
        case "join-lyric-phrase":
            editor.Execute(new JoinLyricPhraseCommand(
                RequiredSectionId(request),
                request.LineId ?? throw new ArgumentException("Lyric line ID is required."),
                request.PhraseId ?? throw new ArgumentException("Lyric phrase ID is required.")));
            break;
        default: throw new ArgumentException($"Unknown command type '{request.Type}'.");
    }
}

static SectionId RequiredSectionId(ProjectCommandRequest request) =>
    request.SectionId ?? throw new ArgumentException("Section ID is required.");
static string Required(string? value, string name) =>
    string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.") : value;
static IResult Validation(Exception exception) =>
    Results.BadRequest(new ApiError(exception.Message));

public sealed record CreateProjectRequest(string Title);
public sealed record UpdateProjectRequest(SongProject Project, DateTimeOffset BaseProjectLastModifiedUtc);
public sealed record RecoverySnapshotRequest(SongProject Project, DateTimeOffset BaseProjectLastModifiedUtc, string SessionId);
public sealed record EditorStateRequest(SongProject Project);
public sealed record ProsodyScoreRequest(
    SongProject Project,
    SectionId SectionId,
    LyricLineId LineId,
    LyricPhraseId PhraseId,
    RhythmCandidateId? RhythmCandidateId = null);
public sealed record LyricTimelineRequest(
    SongProject Project,
    RhythmCandidateId? RhythmCandidateId = null);
public sealed record ProjectCommandRequest(
    string Type,
    SongProject? Project = null,
    SectionId? SectionId = null,
    SectionKind? Kind = null,
    string? Title = null,
    decimal? Tempo = null,
    int? Numerator = null,
    int? Denominator = null,
    int? TargetIndex = null,
    int? DurationBars = null,
    IReadOnlyList<string>? Lyrics = null,
    LyricLineId? LineId = null,
    LyricWordId? WordId = null,
    SyllableId? SyllableId = null,
    LyricPhraseId? PhraseId = null,
    StressLevel? StressLevel = null,
    ProsodicWeight? ProsodicWeight = null,
    BeatPosition? BeatPosition = null,
    RhythmCandidateId? RhythmCandidateId = null,
    string? CandidateLabel = null,
    bool? BreathPresent = null,
    CreativeLockId? CreativeLockId = null,
    string? Text = null,
    IReadOnlyList<string>? Syllables = null);
public sealed record ApiError(string Error, string? Code = null, string? RecoveryCopyFileName = null);
public sealed record ProjectResponse(SongProject Project, bool CanUndo, bool CanRedo)
{
    public static ProjectResponse From(ProjectEditor editor) => new(editor.Project, editor.CanUndo, editor.CanRedo);
}
public sealed record RecoveryProjectResponse(
    SongProject Project,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset BaseProjectLastModifiedUtc);

public partial class Program;
