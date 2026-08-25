using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.FileProviders;
using MaskilForge.Api;
using MaskilForge.Domain;
using MaskilForge.Engine;
using MaskilForge.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
if (OperatingSystem.IsWindows())
    builder.Logging.AddFilter<Microsoft.Extensions.Logging.EventLog.EventLogLoggerProvider>(_ => false);
var webClient = WebClientDistribution.Locate(builder.Environment.ContentRootPath);
var projectLibraryPath = ProjectLibraryPath.Resolve(
    builder.Environment.ContentRootPath,
    builder.Environment.IsDevelopment(),
    builder.Configuration["MaskilForge:LibraryPath"]);
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSingleton<IProjectRepository>(_ =>
    new JsonFileProjectRepository(projectLibraryPath));
builder.Services.AddSingleton<ProjectWorkspace>();
builder.Services.AddSingleton<DevelopmentActivityLogStore>();
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:5173", "http://127.0.0.1:5173").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();
if (webClient.IsAvailable)
{
    var webClientFiles = new PhysicalFileProvider(webClient.RootPath);
    app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = webClientFiles });
    app.UseStaticFiles(new StaticFileOptions { FileProvider = webClientFiles });
}
app.Use(async (context, next) =>
{
    try { await next(); }
    catch (ProjectPersistenceException exception)
    {
        app.Logger.LogError(exception, "Project persistence request failed with code {Code}.", exception.Code);
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
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    {
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        await context.Response.WriteAsJsonAsync(new ApiError(exception.Message), context.RequestAborted);
    }
});
app.UseCors();

app.MapGet("/api/health", () => Results.Ok(new WorkspaceHealthResponse(
    "ready",
    "local-host",
    SchemaVersion.Current.Value,
    webClient.IsAvailable,
    app.Environment.IsDevelopment())));

app.MapGet("/api/instrument-profiles", () => Results.Ok(InstrumentProfileCatalogLoader.Current));
app.MapPost("/api/instrument-recommendations", (InstrumentRecommendationRequest request) =>
    Results.Ok(InstrumentRoleRecommender.Recommend(request.Roles, request.Quality)));
app.MapPost("/api/instrument-range-review", (InstrumentRangeReviewRequest request) =>
    Results.Ok(InstrumentRangeReviewer.Review(request.Notes)));
app.MapGet("/api/instrument-articulation-maps", () => Results.Ok(InstrumentArticulationMapper.Map()));
app.MapGet("/api/drum-kit-gm-map", () => Results.Ok(DrumKitGeneralMidiMapper.Map()));
app.MapGet("/api/instrument-midi-channels", () => Results.Ok(InstrumentMidiChannelMapper.Map()));
app.MapGet("/api/instrument-midi-programs", () => Results.Ok(InstrumentMidiProgramMapper.Map()));

if (app.Environment.IsDevelopment())
{
    app.MapPost("/api/dev/activity-logs", (
        DevelopmentActivityLogSubmission submission,
        DevelopmentActivityLogStore logs) =>
    {
        logs.Append(submission, DateTimeOffset.UtcNow);
        return Results.NoContent();
    });

    app.MapGet("/api/dev/activity-logs/sessions", (DevelopmentActivityLogStore logs) =>
        Results.Ok(logs.ListSessions()));

    app.MapGet("/api/dev/activity-logs/sessions/{sessionId:guid}", (
        Guid sessionId,
        DevelopmentActivityLogStore logs) =>
    {
        var session = logs.ReadSession(sessionId);
        return session is null
            ? Results.NotFound(new ApiError("Development activity log session not found."))
            : Results.Ok(session);
    });

    app.MapDelete("/api/dev/activity-logs/sessions/{sessionId:guid}", (
        Guid sessionId,
        DevelopmentActivityLogStore logs) =>
        logs.RemoveSession(sessionId)
            ? Results.NoContent()
            : Results.NotFound(new ApiError("Development activity log session not found.")));
}

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
        var editor = request.IsDeviceLyricCapture
            ? await workspace.CreateFromLyricCaptureAsync(
                request.Title,
                request.Artist ?? string.Empty,
                request.Genre ?? SongGenre.Unspecified,
                request.Description ?? string.Empty,
                request.RawLyricDraft ?? string.Empty,
                cancellationToken)
            : await workspace.CreateAsync(request.Title, cancellationToken);
        return Results.Created($"/api/projects/{editor.Project.Id}", ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/duplicate", async (string id, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    var editor = await workspace.DuplicateAsync(projectId, cancellationToken);
    return editor is null
        ? Results.NotFound(new ApiError("Project not found."))
        : Results.Created($"/api/projects/{editor.Project.Id}", ProjectResponse.From(editor));
});

app.MapPost("/api/projects/import-preview", async (PortableProjectImportRequest request, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    var requestError = ValidatePortableProjectImport(request);
    if (requestError is not null) return requestError;
    var document = PortableProjectImporter.Inspect(request.ProjectJson);
    return Results.Ok(CreatePortablePreview(document.Project, document.SourceSchemaVersion, await repository.ProjectIdentityExistsAsync(document.Project.Id, cancellationToken)));
});

app.MapPost("/api/projects/import", async (PortableProjectImportRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    var requestError = ValidatePortableProjectImport(request);
    if (requestError is not null) return requestError;
    try
    {
        var project = request.ImportAsCopy
            ? PortableProjectImporter.ImportAsCopy(request.ProjectJson)
            : PortableProjectImporter.Import(request.ProjectJson);
        var editor = await workspace.ImportAsync(project, cancellationToken);
        return Results.Created($"/api/projects/{project.Id}", ProjectResponse.From(editor));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new ApiError(exception.Message));
    }
});

app.MapPost("/api/projects/package-preview", async (HttpRequest request, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    var package = await ReadPortablePackageAsync(request, cancellationToken);
    if (package.Error is not null) return package.Error;
    var document = PortableProjectPackage.Inspect(package.Bytes!);
    return Results.Ok(CreatePortablePreview(document.Project, document.SourceSchemaVersion, await repository.ProjectIdentityExistsAsync(document.Project.Id, cancellationToken)));
});

app.MapPost("/api/projects/package-import", async (HttpRequest request, bool importAsCopy, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    var package = await ReadPortablePackageAsync(request, cancellationToken);
    if (package.Error is not null) return package.Error;
    try
    {
        var document = PortableProjectPackage.Inspect(package.Bytes!, importAsCopy);
        var editor = await workspace.ImportWithAssetsAsync(document.Project, document.Assets, cancellationToken);
        return Results.Created($"/api/projects/{document.Project.Id}", ProjectResponse.From(editor));
    }
    catch (InvalidOperationException exception)
    {
        return Results.Conflict(new ApiError(exception.Message));
    }
});

app.MapPost("/api/structure-preview", (StructurePreviewRequest request) =>
    Results.Ok(LyricSheetStructureParser.Parse(request.Text)));

app.MapGet("/api/projects/{id}", async (string id, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    var editor = await workspace.LoadFromStorageAsync(projectId, cancellationToken);
    return editor is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(ProjectResponse.From(editor));
});

app.MapGet("/api/projects/{id}/vocal-takes/{assetId}", async (
    string id,
    string assetId,
    IProjectRepository repository,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(assetId, out var assetGuid))
        return Results.BadRequest(new ApiError("Invalid project or vocal-take ID."));

    var project = await repository.LoadAsync(projectId, cancellationToken);
    var asset = project?.Assets.SingleOrDefault(item => item.Id.Value == assetGuid && item.Kind == ProjectAssetKind.OriginalVocalTake);
    if (asset is null) return Results.NotFound(new ApiError("Rough vocal take not found."));
    var stream = await repository.OpenAssetAsync(projectId, asset.Id, cancellationToken);
    return stream is null
        ? Results.NotFound(new ApiError("Rough vocal take not found."))
        : Results.Stream(stream, asset.MediaType, enableRangeProcessing: true);
});

app.MapPost("/api/projects/{id}/vocal-takes", async (
    string id,
    DateTimeOffset baseProjectLastModifiedUtc,
    HttpRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId))
        return Results.BadRequest(new ApiError("Invalid project ID."));
    try
    {
        var mediaType = OriginalVocalTakeUpload.NormalizeMediaType(request.ContentType);
        var content = await OriginalVocalTakeUpload.ReadAsync(
            request.Body,
            request.ContentLength,
            cancellationToken);
        var editor = await workspace.AddOriginalVocalTakeAsync(
            projectId,
            baseProjectLastModifiedUtc,
            mediaType,
            content,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
});

app.MapDelete("/api/projects/{id}/vocal-takes/{assetId}", async (
    string id,
    string assetId,
    DateTimeOffset baseProjectLastModifiedUtc,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(assetId, out var assetGuid))
        return Results.BadRequest(new ApiError("Invalid project or vocal-take ID."));
    try
    {
        var editor = await workspace.RemoveOriginalVocalTakeAsync(
            projectId,
            new ProjectAssetId(assetGuid),
            baseProjectLastModifiedUtc,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/vocal-takes/{assetId}/name", async (
    string id,
    string assetId,
    RenameOriginalVocalTakeRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(assetId, out var assetGuid))
        return Results.BadRequest(new ApiError("Invalid project or vocal-take ID."));
    try
    {
        var editor = await workspace.RenameOriginalVocalTakeAsync(
            projectId,
            new ProjectAssetId(assetGuid),
            request.Name,
            request.BaseProjectLastModifiedUtc,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/vocal-takes/{assetId}/loudness-analysis", async (
    string id,
    string assetId,
    LoudnessAnalysisRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(assetId, out var assetGuid))
        return Results.BadRequest(new ApiError("Invalid project or vocal-take ID."));
    try
    {
        var editor = await workspace.ReplaceLoudnessObservationsAsync(
            projectId,
            new ProjectAssetId(assetGuid),
            request.BaseProjectLastModifiedUtc,
            request.Frames,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/performance-observations/{observationId}/review", async (
    string id,
    string observationId,
    PerformanceObservationReviewRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(observationId, out var observationGuid))
        return Results.BadRequest(new ApiError("Invalid project or performance-observation ID."));
    try
    {
        var editor = await workspace.SetPerformanceObservationReviewAsync(
            projectId,
            new PerformanceObservationId(observationGuid),
            request.Verdict,
            request.BaseProjectLastModifiedUtc,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/performance-observations/{observationId}/correction", async (
    string id,
    string observationId,
    PerformanceObservationCorrectionRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(observationId, out var observationGuid))
        return Results.BadRequest(new ApiError("Invalid project or performance-observation ID."));
    try
    {
        var editor = await workspace.SetPerformanceObservationCorrectionAsync(
            projectId,
            new PerformanceObservationId(observationGuid),
            request.Measurements,
            request.BaseProjectLastModifiedUtc,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/performance-observations/{observationId}/gesture", async (
    string id,
    string observationId,
    PerformanceObservationGestureRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(observationId, out var observationGuid))
        return Results.BadRequest(new ApiError("Invalid project or performance-observation ID."));
    try
    {
        var editor = await workspace.SetPerformanceObservationGestureAsync(
            projectId,
            new PerformanceObservationId(observationGuid),
            request.Promoted,
            request.BaseProjectLastModifiedUtc,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (InvalidOperationException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/vocal-takes/{assetId}/pitch-analysis", async (
    string id,
    string assetId,
    PitchAnalysisRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(assetId, out var assetGuid))
        return Results.BadRequest(new ApiError("Invalid project or vocal-take ID."));
    try
    {
        var editor = await workspace.ReplacePitchObservationsAsync(
            projectId,
            new ProjectAssetId(assetGuid),
            request.BaseProjectLastModifiedUtc,
            request.Frames,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
});

app.MapPut("/api/projects/{id}/vocal-takes/{assetId}/onset-analysis", async (
    string id,
    string assetId,
    OnsetAnalysisRequest request,
    ProjectWorkspace workspace,
    CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId) || !Guid.TryParse(assetId, out var assetGuid))
        return Results.BadRequest(new ApiError("Invalid project or vocal-take ID."));
    try
    {
        var editor = await workspace.ReplaceOnsetObservationsAsync(
            projectId,
            new ProjectAssetId(assetGuid),
            request.BaseProjectLastModifiedUtc,
            request.Events,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return editor is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Ok(ProjectResponse.From(editor));
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(new ApiError(exception.Message));
    }
    catch (KeyNotFoundException exception)
    {
        return Results.NotFound(new ApiError(exception.Message));
    }
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
    try
    {
        var response = await workspace.UseAsync(projectId, request.Project, editor =>
        {
            ApplyRequest(editor, request);
            return ProjectResponse.From(editor);
        }, cancellationToken);
        return response is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(response);
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
    try
    {
        var score = await workspace.UseAsync(projectId, request.Project, editor =>
            request.RhythmCandidateId is null
                ? ProsodyScorer.ScoreActivePhrase(
                    editor.Project,
                    request.SectionId,
                    request.LineId,
                    request.PhraseId)
                : ProsodyScorer.ScoreRhythmCandidate(
                    editor.Project,
                    request.SectionId,
                    request.LineId,
                    request.RhythmCandidateId.Value), cancellationToken);
        return score is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(score);
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
    try
    {
        var view = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => LyricTimelineProjector.Project(editor.Project, request.RhythmCandidateId),
            cancellationToken);
        return view is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(view);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/voice-leading-review", async (string id, VoiceLeadingReviewRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var review = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => VoiceLeadingAnalyzer.ReviewSection(editor.Project, request.SectionId),
            cancellationToken);
        return review is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(review);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/harmony-note-sketch", async (string id, HarmonyNoteSketchRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var sketch = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => HarmonyNoteSketcher.Project(editor.Project, request.SectionId),
            cancellationToken);
        return sketch is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(sketch);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/pitch-gesture-note-sketch", async (string id, PitchGestureNoteSketchRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var sketch = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => PitchGestureNoteSketcher.Project(editor.Project, request.AssetId),
            cancellationToken);
        return sketch is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(sketch);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/onset-gesture-note-sketch", async (string id, OnsetGestureNoteSketchRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var sketch = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => OnsetGestureNoteSketcher.Project(editor.Project, request.AssetId),
            cancellationToken);
        return sketch is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(sketch);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/loudness-gesture-note-sketch", async (string id, LoudnessGestureNoteSketchRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var sketch = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => LoudnessGestureNoteSketcher.Project(editor.Project, request.AssetId),
            cancellationToken);
        return sketch is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(sketch);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/loudness-gesture-expression-sketch", async (string id, LoudnessGestureExpressionSketchRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var sketch = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => LoudnessGestureExpressionSketcher.Project(editor.Project, request.AssetId),
            cancellationToken);
        return sketch is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(sketch);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/instrument-performance-sketch", async (string id, InstrumentPerformanceSketchRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var sketch = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => InstrumentPerformanceRetargeter.Project(editor.Project, request.AssetId),
            cancellationToken);
        return sketch is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(sketch);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/low-end-support-proposal", async (string id, LowEndSupportProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => LowEndSupportRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/pulse-proposal", async (string id, PulseProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => PulseRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/harmony-support-proposal", async (string id, HarmonySupportProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => HarmonySupportRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/texture-proposal", async (string id, TextureProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => TextureRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/hook-reinforcement-proposal", async (string id, HookReinforcementProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => HookReinforcementRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/countermelody-proposal", async (string id, CountermelodyProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => CountermelodyRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/accent-proposal", async (string id, AccentProposalRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var proposal = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => AccentRealizer.Propose(editor.Project, request.SectionId),
            cancellationToken);
        return proposal is null ? Results.NotFound(new ApiError("Project not found.")) : Results.Ok(proposal);
    }
    catch (Exception exception) when (exception is ArgumentException or KeyNotFoundException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/midi-export", async (string id, MidiExportRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var midi = await workspace.UseAsync(
            projectId,
            request.Project,
            editor => MidiFileExporter.Export(editor.Project),
            cancellationToken);
        return midi is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.File(midi, "audio/midi", "maskil-forge-sketch.mid");
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/portable-export", async (string id, PortableProjectExportRequest request, ProjectWorkspace workspace, IProjectRepository repository, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    try
    {
        var editor = await workspace.UseAsync(projectId, request.Project, current => current, cancellationToken);
        if (editor is null) return Results.NotFound(new ApiError("Project not found."));
        if (editor.Project.Assets.Count == 0)
        {
            return Results.File(
                PortableProjectExporter.Export(editor.Project),
                PortableProjectExporter.ContentType,
                "maskil-forge-project.maskil.json");
        }

        var assets = new Dictionary<ProjectAssetId, byte[]>();
        foreach (var asset in editor.Project.Assets)
        {
            await using var stream = await repository.OpenAssetAsync(editor.Project.Id, asset.Id, cancellationToken)
                ?? throw new InvalidProjectDataException($"Project asset '{asset.Id}' is missing.");
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            assets[asset.Id] = buffer.ToArray();
        }

        return Results.File(
            PortableProjectPackage.Export(editor.Project, assets),
            PortableProjectPackage.ContentType,
            "maskil-forge-project.maskil");
    }
    catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
    {
        return Validation(exception);
    }
});

app.MapPost("/api/projects/{id}/undo", async (string id, EditorStateRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var response = await workspace.UseAsync(projectId, request.Project, editor =>
    {
        if (!editor.Undo()) return null;
        return ProjectResponse.From(editor);
    }, cancellationToken);
    if (response is null)
    {
        var exists = await workspace.GetAsync(projectId, cancellationToken);
        return exists is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Conflict(new ApiError("Nothing to undo."));
    }
    return Results.Ok(response);
});

app.MapPost("/api/projects/{id}/redo", async (string id, EditorStateRequest request, ProjectWorkspace workspace, CancellationToken cancellationToken) =>
{
    if (!ProjectId.TryParse(id, out var projectId)) return Results.BadRequest(new ApiError("Invalid project ID."));
    if (request.Project.Id != projectId) return Results.BadRequest(new ApiError("Route and project IDs must match."));
    var response = await workspace.UseAsync(projectId, request.Project, editor =>
    {
        if (!editor.Redo()) return null;
        return ProjectResponse.From(editor);
    }, cancellationToken);
    if (response is null)
    {
        var exists = await workspace.GetAsync(projectId, cancellationToken);
        return exists is null
            ? Results.NotFound(new ApiError("Project not found."))
            : Results.Conflict(new ApiError("Nothing to redo."));
    }
    return Results.Ok(response);
});

if (webClient.IsAvailable)
{
    app.MapFallback(async context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            await context.Response.WriteAsJsonAsync(new ApiError("API route not found."), context.RequestAborted);
            return;
        }

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.SendFileAsync(webClient.IndexPath, context.RequestAborted);
    });
}

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
        case "import-song-structure": editor.Execute(new ImportSongStructureCommand(
            request.ProposedSections ?? throw new ArgumentException("Proposed sections are required."))); break;
        case "duplicate-section": editor.Execute(new DuplicateSectionCommand(RequiredSectionId(request))); break;
        case "reuse-section-foundation": editor.Execute(new ReuseSectionFoundationCommand(
            request.SourceSectionId ?? throw new ArgumentException("Source section ID is required."),
            RequiredSectionId(request))); break;
        case "rename-section": editor.Execute(new RenameSectionCommand(RequiredSectionId(request), Required(request.Title, "title"))); break;
        case "set-section-performance-intent": editor.Execute(new SetSectionPerformanceIntentCommand(
            RequiredSectionId(request),
            request.SectionDelivery ?? throw new ArgumentException("Section delivery is required."),
            request.PerformanceNotes ?? string.Empty)); break;
        case "set-section-intent": editor.Execute(new SetSectionIntentCommand(
            RequiredSectionId(request),
            request.StructuralFunction ?? throw new ArgumentException("Structural function is required."),
            request.SectionDelivery ?? throw new ArgumentException("Section delivery is required."),
            request.PerformanceNotes ?? string.Empty)); break;
        case "set-section-structural-function": editor.Execute(new SetSectionStructuralFunctionCommand(
            RequiredSectionId(request),
            request.StructuralFunction ?? throw new ArgumentException("Structural function is required."))); break;
        case "move-section": editor.Execute(new MoveSectionCommand(RequiredSectionId(request), request.TargetIndex ?? throw new ArgumentException("Target index is required."))); break;
        case "set-section-duration": editor.Execute(new SetSectionDurationCommand(
            RequiredSectionId(request),
            request.DurationBars ?? throw new ArgumentException("Section duration is required."))); break;
        case "set-section-arrangement": editor.Execute(new SetSectionArrangementCommand(
            RequiredSectionId(request),
            request.SectionEnergy ?? throw new ArgumentException("Section energy is required."),
            request.SectionDensity ?? throw new ArgumentException("Section density is required."))); break;
        case "set-section-role": editor.Execute(new SetSectionRoleCommand(
            RequiredSectionId(request),
            request.ArrangementRole ?? throw new ArgumentException("Arrangement role is required."),
            request.RolePresent ?? throw new ArgumentException("Role presence is required."))); break;
        case "add-musical-part": editor.Execute(new AddMusicalPartCommand(
            RequiredSectionId(request),
            request.ArrangementRole ?? throw new ArgumentException("Arrangement role is required."),
            Required(request.PartLabel, "Musical-part name"),
            request.NoteEventIds ?? throw new ArgumentException("Playable-note IDs are required."),
            request.InstrumentProfileId)); break;
        case "set-musical-part": editor.Execute(new SetMusicalPartCommand(
            request.MusicalPartId ?? throw new ArgumentException("Musical-part ID is required."),
            request.PartLabel ?? throw new ArgumentException("A musical-part name is required."),
            request.NoteEventIds ?? throw new ArgumentException("Playable-note IDs are required."),
            request.InstrumentProfileId)); break;
        case "remove-musical-part": editor.Execute(new RemoveMusicalPartCommand(
            request.MusicalPartId ?? throw new ArgumentException("Musical-part ID is required."))); break;
        case "use-low-end-support-proposal": editor.Execute(new UseLowEndSupportProposalCommand(RequiredSectionId(request))); break;
        case "use-pulse-proposal": editor.Execute(new UsePulseProposalCommand(RequiredSectionId(request))); break;
        case "use-harmony-support-proposal": editor.Execute(new UseHarmonySupportProposalCommand(RequiredSectionId(request))); break;
        case "use-texture-proposal": editor.Execute(new UseTextureProposalCommand(RequiredSectionId(request))); break;
        case "use-hook-reinforcement-proposal": editor.Execute(new UseHookReinforcementProposalCommand(RequiredSectionId(request))); break;
        case "use-countermelody-proposal": editor.Execute(new UseCountermelodyProposalCommand(RequiredSectionId(request))); break;
        case "use-accent-proposal": editor.Execute(new UseAccentProposalCommand(RequiredSectionId(request))); break;
        case "add-note-event": editor.Execute(new AddNoteEventCommand(
            request.NotePitch ?? throw new ArgumentException("Note pitch is required."),
            request.StartTick ?? throw new ArgumentException("Start tick is required."),
            request.DurationTicks ?? throw new ArgumentException("Duration in ticks is required."),
            request.Velocity ?? throw new ArgumentException("Velocity is required."))); break;
        case "set-note-event": editor.Execute(new SetNoteEventCommand(
            request.NoteEventId ?? throw new ArgumentException("Note-event ID is required."),
            request.NotePitch ?? throw new ArgumentException("Note pitch is required."),
            request.StartTick ?? throw new ArgumentException("Start tick is required."),
            request.DurationTicks ?? throw new ArgumentException("Duration in ticks is required."),
            request.Velocity ?? throw new ArgumentException("Velocity is required."))); break;
        case "remove-note-event": editor.Execute(new RemoveNoteEventCommand(
            request.NoteEventId ?? throw new ArgumentException("Note-event ID is required."))); break;
        case "use-harmony-note-sketch": editor.Execute(new UseHarmonyNoteSketchCommand(RequiredSectionId(request))); break;
        case "use-pitch-gesture-note-sketch": editor.Execute(new UsePitchGestureNoteSketchCommand(RequiredAssetId(request))); break;
        case "use-onset-gesture-note-sketch": editor.Execute(new UseOnsetGestureNoteSketchCommand(RequiredAssetId(request))); break;
        case "use-loudness-gesture-note-sketch": editor.Execute(new UseLoudnessGestureNoteSketchCommand(RequiredAssetId(request))); break;
        case "use-loudness-gesture-expression-sketch": editor.Execute(new UseLoudnessGestureExpressionSketchCommand(RequiredAssetId(request))); break;
        case "use-instrument-performance-sketch": editor.Execute(new UseInstrumentPerformanceSketchCommand(
            RequiredAssetId(request),
            request.InstrumentProfileId ?? throw new ArgumentException("An instrument profile is required."),
            request.MusicalPartId ?? throw new ArgumentException("Musical-part ID is required."))); break;
        case "remove-expression-curve": editor.Execute(new RemoveExpressionCurveCommand(
            request.ExpressionCurveId ?? throw new ArgumentException("Expression-curve ID is required."))); break;
        case "set-vocal-take-placement": editor.Execute(new SetVocalTakePlacementCommand(
            RequiredAssetId(request),
            request.Start ?? throw new ArgumentException("Start position is required."))); break;
        case "clear-vocal-take-placement": editor.Execute(new ClearVocalTakePlacementCommand(RequiredAssetId(request))); break;
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
        case "set-key":
            editor.Execute(new SetKeyCommand(
                request.Key ?? throw new ArgumentException("Musical key is required.")));
            break;
        case "add-harmony-chord":
            editor.Execute(new AddHarmonyChordCommand(
                RequiredSectionId(request),
                request.Chord ?? throw new ArgumentException("Chord is required."),
                request.BeatPosition ?? throw new ArgumentException("Harmony start position is required."),
                request.DurationBars ?? 1));
            break;
        case "set-harmony-chord":
            editor.Execute(new SetHarmonyChordCommand(
                RequiredSectionId(request),
                request.HarmonyChordId ?? throw new ArgumentException("Harmony chord ID is required."),
                request.Chord ?? throw new ArgumentException("Chord is required."),
                request.BeatPosition ?? throw new ArgumentException("Harmony start position is required."),
                request.DurationBars ?? throw new ArgumentException("Harmony duration is required.")));
            break;
        case "remove-harmony-chord":
            editor.Execute(new RemoveHarmonyChordCommand(
                RequiredSectionId(request),
                request.HarmonyChordId ?? throw new ArgumentException("Harmony chord ID is required.")));
            break;
        case "set-chord-voicing":
            editor.Execute(new SetChordVoicingCommand(
                RequiredSectionId(request),
                request.HarmonyChordId ?? throw new ArgumentException("Harmony chord ID is required."),
                request.RegisteredPitches,
                request.MinimumMidiNote ?? 21,
                request.MaximumMidiNote ?? 108));
            break;
        case "capture-harmony-candidate":
            editor.Execute(new CaptureHarmonyCandidateCommand(
                RequiredSectionId(request),
                request.CandidateLabel ?? throw new ArgumentException("Harmony option name is required.")));
            break;
        case "rename-harmony-candidate":
            editor.Execute(new RenameHarmonyCandidateCommand(
                RequiredSectionId(request),
                request.HarmonyCandidateId ?? throw new ArgumentException("Harmony candidate ID is required."),
                request.CandidateLabel ?? throw new ArgumentException("Harmony option name is required.")));
            break;
        case "remove-harmony-candidate":
            editor.Execute(new RemoveHarmonyCandidateCommand(
                RequiredSectionId(request),
                request.HarmonyCandidateId ?? throw new ArgumentException("Harmony candidate ID is required.")));
            break;
        case "apply-harmony-candidate":
            editor.Execute(new ApplyHarmonyCandidateCommand(
                RequiredSectionId(request),
                request.HarmonyCandidateId ?? throw new ArgumentException("Harmony candidate ID is required.")));
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
static ProjectAssetId RequiredAssetId(ProjectCommandRequest request) =>
    request.AssetId ?? throw new ArgumentException("Asset ID is required.");
static string Required(string? value, string name) =>
    string.IsNullOrWhiteSpace(value) ? throw new ArgumentException($"{name} is required.") : value;
static IResult Validation(Exception exception) =>
    Results.BadRequest(new ApiError(exception.Message));
static IResult? ValidatePortableProjectImport(PortableProjectImportRequest request)
{
    if (string.IsNullOrWhiteSpace(request.ProjectJson))
        return Results.BadRequest(new ApiError("Choose a portable project file to import."));
    return Encoding.UTF8.GetByteCount(request.ProjectJson) > 10 * 1024 * 1024
        ? Results.BadRequest(new ApiError("Portable project files cannot exceed 10 MB."))
        : null;
}

static async Task<(byte[]? Bytes, IResult? Error)> ReadPortablePackageAsync(HttpRequest request, CancellationToken cancellationToken)
{
    if (request.ContentLength is > PortableProjectPackage.MaximumByteLength)
        return (null, Results.BadRequest(new ApiError("Asset-owning project packages cannot exceed 25 MB.")));
    using var buffer = new MemoryStream();
    await request.Body.CopyToAsync(buffer, cancellationToken);
    if (buffer.Length > PortableProjectPackage.MaximumByteLength)
        return (null, Results.BadRequest(new ApiError("Asset-owning project packages cannot exceed 25 MB.")));
    if (buffer.Length == 0)
        return (null, Results.BadRequest(new ApiError("Choose an asset-owning .maskil project package to import.")));
    return (buffer.ToArray(), null);
}

static PortableProjectImportPreviewResponse CreatePortablePreview(SongProject project, int sourceSchemaVersion, bool identityConflict)
{
    var lyricLineCount = project.Sections.Count > 0
        ? project.Sections.Sum(section => section.LyricLines.Count)
        : project.RawLyricDraft.Split('\n').Count(line => !string.IsNullOrWhiteSpace(line));
    return new PortableProjectImportPreviewResponse(
        project.Id,
        project.Title,
        project.Artist,
        project.Genre,
        sourceSchemaVersion,
        SchemaVersion.Current.Value,
        project.Sections.Count,
        lyricLineCount,
        !string.IsNullOrWhiteSpace(project.RawLyricDraft),
        project.Sections.Select(section => section.Title).ToList(),
        identityConflict,
        project.Assets.Count);
}

public sealed record CreateProjectRequest(
    string Title,
    bool IsDeviceLyricCapture = false,
    string? Artist = null,
    SongGenre? Genre = null,
    string? Description = null,
    string? RawLyricDraft = null);
public sealed record UpdateProjectRequest(SongProject Project, DateTimeOffset BaseProjectLastModifiedUtc);
public sealed record RenameOriginalVocalTakeRequest(string Name, DateTimeOffset BaseProjectLastModifiedUtc);
public sealed record LoudnessAnalysisRequest(DateTimeOffset BaseProjectLastModifiedUtc, IReadOnlyList<LoudnessFrameReport> Frames);
public sealed record PitchAnalysisRequest(DateTimeOffset BaseProjectLastModifiedUtc, IReadOnlyList<PitchFrameReport> Frames);
public sealed record OnsetAnalysisRequest(DateTimeOffset BaseProjectLastModifiedUtc, IReadOnlyList<OnsetEventReport> Events);
public sealed record PerformanceObservationReviewRequest(
    DateTimeOffset BaseProjectLastModifiedUtc,
    PerformanceObservationReviewVerdict? Verdict);
public sealed record PerformanceObservationCorrectionRequest(
    DateTimeOffset BaseProjectLastModifiedUtc,
    IReadOnlyList<PerformanceMeasurement>? Measurements);
public sealed record PerformanceObservationGestureRequest(
    DateTimeOffset BaseProjectLastModifiedUtc,
    bool? Promoted);
public sealed record RecoverySnapshotRequest(SongProject Project, DateTimeOffset BaseProjectLastModifiedUtc, string SessionId);
public sealed record EditorStateRequest(SongProject Project);
public sealed record ProsodyScoreRequest(
    SongProject Project,
    SectionId SectionId,
    LyricLineId LineId,
    LyricPhraseId PhraseId,
    RhythmCandidateId? RhythmCandidateId = null);
public sealed record VoiceLeadingReviewRequest(SongProject Project, SectionId SectionId);
public sealed record HarmonyNoteSketchRequest(SongProject Project, SectionId SectionId);
public sealed record PitchGestureNoteSketchRequest(SongProject Project, ProjectAssetId AssetId);
public sealed record OnsetGestureNoteSketchRequest(SongProject Project, ProjectAssetId AssetId);
public sealed record LoudnessGestureNoteSketchRequest(SongProject Project, ProjectAssetId AssetId);
public sealed record LoudnessGestureExpressionSketchRequest(SongProject Project, ProjectAssetId AssetId);
public sealed record InstrumentPerformanceSketchRequest(SongProject Project, ProjectAssetId AssetId);
public sealed record LowEndSupportProposalRequest(SongProject Project, SectionId SectionId);
public sealed record PulseProposalRequest(SongProject Project, SectionId SectionId);
public sealed record HarmonySupportProposalRequest(SongProject Project, SectionId SectionId);
public sealed record TextureProposalRequest(SongProject Project, SectionId SectionId);
public sealed record HookReinforcementProposalRequest(SongProject Project, SectionId SectionId);
public sealed record CountermelodyProposalRequest(SongProject Project, SectionId SectionId);
public sealed record AccentProposalRequest(SongProject Project, SectionId SectionId);
public sealed record MidiExportRequest(SongProject Project);
public sealed record PortableProjectExportRequest(SongProject Project);
public sealed record PortableProjectImportRequest(string ProjectJson, bool ImportAsCopy = false);
public sealed record PortableProjectImportPreviewResponse(
    ProjectId Id,
    string Title,
    string Artist,
    SongGenre Genre,
    int SourceSchemaVersion,
    int CurrentSchemaVersion,
    int SectionCount,
    int LyricLineCount,
    bool HasRawLyrics,
    IReadOnlyList<string> SectionTitles,
    bool IdentityConflict,
    int OriginalVocalCount);
public sealed record LyricTimelineRequest(
    SongProject Project,
    RhythmCandidateId? RhythmCandidateId = null);
public sealed record StructurePreviewRequest(string Text);
public sealed record InstrumentRecommendationRequest(
    IReadOnlyList<ArrangementRole> Roles,
    InstrumentExpressiveQuality? Quality = null);
public sealed record InstrumentRangeReviewRequest(IReadOnlyList<InstrumentRangeReviewNote> Notes);
public sealed record ProjectCommandRequest(
    string Type,
    SongProject? Project = null,
    SectionId? SectionId = null,
    ProjectAssetId? AssetId = null,
    SectionId? SourceSectionId = null,
    SectionKind? Kind = null,
    SectionDelivery? SectionDelivery = null,
    StructuralFunction? StructuralFunction = null,
    string? Title = null,
    string? PerformanceNotes = null,
    decimal? Tempo = null,
    int? Numerator = null,
    int? Denominator = null,
    int? TargetIndex = null,
    int? DurationBars = null,
    IReadOnlyList<string>? Lyrics = null,
    IReadOnlyList<ProposedSongSection>? ProposedSections = null,
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
    MusicalKey? Key = null,
    ChordSymbol? Chord = null,
    HarmonyChordId? HarmonyChordId = null,
    HarmonyCandidateId? HarmonyCandidateId = null,
    IReadOnlyList<RegisteredPitch>? RegisteredPitches = null,
    int? MinimumMidiNote = null,
    int? MaximumMidiNote = null,
    SectionEnergy? SectionEnergy = null,
    SectionDensity? SectionDensity = null,
    ArrangementRole? ArrangementRole = null,
    bool? RolePresent = null,
    NoteEventId? NoteEventId = null,
    MusicalPartId? MusicalPartId = null,
    string? PartLabel = null,
    IReadOnlyList<NoteEventId>? NoteEventIds = null,
    string? InstrumentProfileId = null,
    RegisteredPitch? NotePitch = null,
    long? StartTick = null,
    long? DurationTicks = null,
    int? Velocity = null,
    MusicalPosition? Start = null,
    ExpressionCurveId? ExpressionCurveId = null,
    string? Text = null,
    IReadOnlyList<string>? Syllables = null);
public sealed record ApiError(string Error, string? Code = null, string? RecoveryCopyFileName = null);
public sealed record WorkspaceHealthResponse(
    string Status,
    string Persistence,
    int SchemaVersion,
    bool WebClientHosted,
    bool RemoteActivityLoggingEnabled);
public sealed record ProjectResponse(SongProject Project, bool CanUndo, bool CanRedo)
{
    public static ProjectResponse From(ProjectEditor editor) => new(editor.Project, editor.CanUndo, editor.CanRedo);
}
public sealed record RecoveryProjectResponse(
    SongProject Project,
    DateTimeOffset CapturedAtUtc,
    DateTimeOffset BaseProjectLastModifiedUtc);

public partial class Program;
