using System.Text.Json.Serialization;

namespace MaskilForge.Domain;

public enum SongGenre
{
    Unspecified,
    Pop,
    Rock,
    Folk,
    Country,
    RAndB,
    HipHop,
    Electronic,
    Cinematic,
    Alternative,
    Other
}

public sealed class SongProject
{
    private readonly List<SongSection> _sections;
    private readonly List<Track> _tracks;
    private readonly List<CreativeLock> _locks;
    private readonly List<SectionArrangement> _arrangement;
    private readonly List<SectionRoleAssignment> _arrangementRoles;
    private readonly List<NoteEvent> _noteEvents;
    private readonly List<MusicalPart> _musicalParts;
    private readonly List<ProjectAsset> _assets;
    private readonly List<PerformanceObservation> _performanceObservations;
    private readonly List<PerformanceObservationReview> _performanceObservationReviews;
    private readonly List<PerformanceObservationCorrection> _performanceObservationCorrections;
    private readonly List<PerformanceObservationGesture> _performanceObservationGestures;
    private readonly List<VocalTakePlacement> _vocalTakePlacements;
    private readonly List<ExpressionCurve> _expressionCurves;

    [JsonConstructor]
    public SongProject(
        ProjectId id,
        SchemaVersion schemaVersion,
        string title,
        SongTimeline timeline,
        IReadOnlyList<SongSection>? sections = null,
        IReadOnlyList<Track>? tracks = null,
        string artist = "",
        SongGenre genre = SongGenre.Unspecified,
        string description = "",
        string rawLyricDraft = "",
        DateTimeOffset createdUtc = default,
        DateTimeOffset lastModifiedUtc = default,
        IReadOnlyList<CreativeLock>? locks = null,
        MusicalKey? key = null,
        IReadOnlyList<SectionArrangement>? arrangement = null,
        IReadOnlyList<SectionRoleAssignment>? arrangementRoles = null,
        IReadOnlyList<NoteEvent>? noteEvents = null,
        IReadOnlyList<MusicalPart>? musicalParts = null,
        IReadOnlyList<ProjectAsset>? assets = null,
        IReadOnlyList<PerformanceObservation>? performanceObservations = null,
        IReadOnlyList<PerformanceObservationReview>? performanceObservationReviews = null,
        IReadOnlyList<PerformanceObservationCorrection>? performanceObservationCorrections = null,
        IReadOnlyList<PerformanceObservationGesture>? performanceObservationGestures = null,
        IReadOnlyList<VocalTakePlacement>? vocalTakePlacements = null,
        IReadOnlyList<ExpressionCurve>? expressionCurves = null)
    {
        if (id.Value == Guid.Empty) throw new ArgumentException("A project ID is required.", nameof(id));
        if (schemaVersion.Value < 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        Id = id;
        SchemaVersion = schemaVersion;
        Rename(title);
        SetArtist(artist);
        SetGenre(genre);
        SetDescription(description);
        SetRawLyricDraft(rawLyricDraft);
        Timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _sections = sections?.ToList() ?? [];
        _tracks = tracks?.ToList() ?? [];
        _locks = locks?.Select(CloneLock).ToList() ?? [];
        _arrangement = arrangement?.ToList() ?? [];
        _arrangementRoles = arrangementRoles?.ToList() ?? [];
        _noteEvents = noteEvents?.OrderBy(item => item.StartTick).ThenBy(item => item.Pitch.MidiNumber).ToList() ?? [];
        _musicalParts = musicalParts?.ToList() ?? [];
        _assets = assets?.ToList() ?? [];
        _performanceObservations = performanceObservations?.ToList() ?? [];
        _performanceObservationReviews = performanceObservationReviews?.ToList() ?? [];
        _performanceObservationCorrections = performanceObservationCorrections?.ToList() ?? [];
        _performanceObservationGestures = performanceObservationGestures?.ToList() ?? [];
        _vocalTakePlacements = vocalTakePlacements?.ToList() ?? [];
        _expressionCurves = expressionCurves?.ToList() ?? [];
        Key = key ?? MusicalKey.Default;
        EnsureUniqueIds();
        Timeline.ValidateSectionOrder(_sections.Select(section => section.Id).ToList());
        ValidateAllSyllablePlacements(TimeSignature);
        ValidateAllRhythmCandidates(TimeSignature);
        ValidateAllHarmonyChords(TimeSignature);
        ValidateAllHarmonyCandidates(TimeSignature);
        ValidateLockReferences();
        ValidatePerformanceObservationReferences();
        ValidatePerformanceObservationReviewReferences();
        ValidatePerformanceObservationCorrectionReferences();
        ValidatePerformanceObservationGestureReferences();
        ValidateVocalTakePlacementReferences();
        CreatedUtc = createdUtc == default ? DateTimeOffset.UtcNow : createdUtc;
        LastModifiedUtc = lastModifiedUtc == default ? CreatedUtc : lastModifiedUtc;
    }

    public ProjectId Id { get; }
    public SchemaVersion SchemaVersion { get; }
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public SongGenre Genre { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string RawLyricDraft { get; private set; } = string.Empty;
    public DateTimeOffset CreatedUtc { get; private set; }
    public DateTimeOffset LastModifiedUtc { get; private set; }
    public SongTimeline Timeline { get; }
    [JsonIgnore] public TempoEvent Tempo => Timeline.TempoMap.Events[0];
    [JsonIgnore] public TimeSignatureEvent TimeSignature => Timeline.TimeSignatureMap.Events[0];
    [JsonIgnore] public LyricDocument Lyrics => new(
        RawLyricDraft,
        _sections.SelectMany(section => section.LyricLines.Select(line => new LyricDocumentLine(section.Id, line))).ToList());
    public IReadOnlyList<SongSection> Sections => _sections;
    public IReadOnlyList<Track> Tracks => _tracks;
    public IReadOnlyList<CreativeLock> Locks => _locks;
    public IReadOnlyList<SectionArrangement> Arrangement => _arrangement;
    public IReadOnlyList<SectionRoleAssignment> ArrangementRoles => _arrangementRoles;
    public IReadOnlyList<NoteEvent> NoteEvents => _noteEvents;
    public IReadOnlyList<MusicalPart> MusicalParts => _musicalParts;
    public IReadOnlyList<ProjectAsset> Assets => _assets;
    public IReadOnlyList<PerformanceObservation> PerformanceObservations => _performanceObservations;
    public IReadOnlyList<PerformanceObservationReview> PerformanceObservationReviews => _performanceObservationReviews;
    public IReadOnlyList<PerformanceObservationCorrection> PerformanceObservationCorrections => _performanceObservationCorrections;
    public IReadOnlyList<PerformanceObservationGesture> PerformanceObservationGestures => _performanceObservationGestures;
    public IReadOnlyList<VocalTakePlacement> VocalTakePlacements => _vocalTakePlacements;
    public IReadOnlyList<ExpressionCurve> ExpressionCurves => _expressionCurves;
    public MusicalKey Key { get; private set; } = MusicalKey.Default;

    public static SongProject Create(string title) => new(
        ProjectId.New(),
        SchemaVersion.Current,
        title,
        SongTimeline.CreateDefault());

    public void RegisterAsset(ProjectAsset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (_assets.Any(item => item.Id == asset.Id))
            throw new InvalidOperationException($"Project asset '{asset.Id}' is already registered.");
        _assets.Add(asset);
        Touch();
    }

    public ProjectAsset RemoveAsset(ProjectAssetId assetId)
    {
        var asset = _assets.SingleOrDefault(item => item.Id == assetId)
            ?? throw new KeyNotFoundException($"Project asset '{assetId}' was not found.");
        var removedObservationIds = _performanceObservations
            .Where(item => item.SourceAssetId == assetId)
            .Select(item => item.Id)
            .ToHashSet();
        _assets.Remove(asset);
        _performanceObservations.RemoveAll(item => item.SourceAssetId == assetId);
        _vocalTakePlacements.RemoveAll(item => item.AssetId == assetId);
        RemoveDependentObservationRecords(removedObservationIds);
        Touch();
        return asset;
    }

    public void RegisterPerformanceObservation(PerformanceObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        if (_performanceObservations.Any(item => item.Id == observation.Id))
            throw new InvalidOperationException($"Performance observation '{observation.Id}' is already registered.");
        ValidatePerformanceObservationReference(observation);
        _performanceObservations.Add(observation);
        Touch();
    }

    public PerformanceObservation RemovePerformanceObservation(PerformanceObservationId observationId)
    {
        var observation = _performanceObservations.SingleOrDefault(item => item.Id == observationId)
            ?? throw new KeyNotFoundException($"Performance observation '{observationId}' was not found.");
        _performanceObservations.Remove(observation);
        RemoveDependentObservationRecords([observationId]);
        Touch();
        return observation;
    }

    public PerformanceObservationReview SetPerformanceObservationReview(
        PerformanceObservationId observationId,
        PerformanceObservationReviewVerdict verdict,
        DateTimeOffset reviewedUtc)
    {
        if (_performanceObservations.All(item => item.Id != observationId))
            throw new KeyNotFoundException($"Performance observation '{observationId}' was not found.");
        if (reviewedUtc == default) throw new ArgumentException("A review time is required.", nameof(reviewedUtc));

        var index = _performanceObservationReviews.FindIndex(item => item.ObservationId == observationId);
        var review = index < 0
            ? new PerformanceObservationReview(
                PerformanceObservationReviewId.New(), observationId, verdict, reviewedUtc, reviewedUtc)
            : _performanceObservationReviews[index].Revise(verdict, reviewedUtc);
        if (index < 0) _performanceObservationReviews.Add(review);
        else _performanceObservationReviews[index] = review;
        if (verdict != PerformanceObservationReviewVerdict.Inaccurate)
            _performanceObservationCorrections.RemoveAll(item => item.ObservationId == observationId);
        RefreshOrDropGesture(observationId, reviewedUtc);
        Touch();
        return review;
    }

    public PerformanceObservationReview ClearPerformanceObservationReview(PerformanceObservationId observationId)
    {
        var review = _performanceObservationReviews.SingleOrDefault(item => item.ObservationId == observationId)
            ?? throw new KeyNotFoundException($"Performance observation '{observationId}' has not been reviewed.");
        _performanceObservationReviews.Remove(review);
        _performanceObservationCorrections.RemoveAll(item => item.ObservationId == observationId);
        _performanceObservationGestures.RemoveAll(item => item.ObservationId == observationId);
        Touch();
        return review;
    }

    public PerformanceObservationCorrection SetPerformanceObservationCorrection(
        PerformanceObservationId observationId,
        IReadOnlyList<PerformanceMeasurement> measurements,
        DateTimeOffset updatedUtc)
    {
        var observation = _performanceObservations.SingleOrDefault(item => item.Id == observationId)
            ?? throw new KeyNotFoundException($"Performance observation '{observationId}' was not found.");
        var review = _performanceObservationReviews.SingleOrDefault(item => item.ObservationId == observationId);
        if (review is null || review.Verdict != PerformanceObservationReviewVerdict.Inaccurate)
            throw new InvalidOperationException("A correction can be stored only after the artist marks the claim inaccurate.");
        if (updatedUtc == default) throw new ArgumentException("A correction time is required.", nameof(updatedUtc));
        PerformanceObservationCorrection.ValidateAgainst(observation, measurements);

        var index = _performanceObservationCorrections.FindIndex(item => item.ObservationId == observationId);
        var correction = index < 0
            ? new PerformanceObservationCorrection(
                PerformanceObservationCorrectionId.New(), observationId, measurements, updatedUtc, updatedUtc)
            : _performanceObservationCorrections[index].Revise(measurements, updatedUtc);
        if (index < 0) _performanceObservationCorrections.Add(correction);
        else _performanceObservationCorrections[index] = correction;
        RefreshOrDropGesture(observationId, updatedUtc);
        Touch();
        return correction;
    }

    public PerformanceObservationCorrection ClearPerformanceObservationCorrection(PerformanceObservationId observationId)
    {
        var correction = _performanceObservationCorrections.SingleOrDefault(item => item.ObservationId == observationId)
            ?? throw new KeyNotFoundException($"Performance observation '{observationId}' has no artist correction.");
        _performanceObservationCorrections.Remove(correction);
        RefreshOrDropGesture(observationId, DateTimeOffset.UtcNow);
        Touch();
        return correction;
    }

    public PerformanceObservationGesture SetPerformanceObservationGesture(
        PerformanceObservationId observationId,
        DateTimeOffset updatedUtc)
    {
        if (updatedUtc == default) throw new ArgumentException("A gesture time is required.", nameof(updatedUtc));
        if (_performanceObservations.All(item => item.Id != observationId))
            throw new KeyNotFoundException($"Performance observation '{observationId}' was not found.");
        if (!TryGetApprovedGestureMeasurements(observationId, out var measurements))
            throw new InvalidOperationException("A gesture can be stored only for an accurate claim or an inaccurate claim that already has a correction.");

        var index = _performanceObservationGestures.FindIndex(item => item.ObservationId == observationId);
        var gesture = index < 0
            ? new PerformanceObservationGesture(
                PerformanceObservationGestureId.New(), observationId, measurements, updatedUtc, updatedUtc)
            : _performanceObservationGestures[index].Revise(measurements, updatedUtc);
        if (index < 0) _performanceObservationGestures.Add(gesture);
        else _performanceObservationGestures[index] = gesture;
        Touch();
        return gesture;
    }

    public PerformanceObservationGesture ClearPerformanceObservationGesture(PerformanceObservationId observationId)
    {
        var gesture = _performanceObservationGestures.SingleOrDefault(item => item.ObservationId == observationId)
            ?? throw new KeyNotFoundException($"Performance observation '{observationId}' has no artist gesture.");
        _performanceObservationGestures.Remove(gesture);
        Touch();
        return gesture;
    }

    public void ReplacePerformanceObservations(
        ProjectAssetId sourceAssetId,
        string analyzerId,
        string kind,
        IReadOnlyList<PerformanceObservation> replacements)
    {
        ArgumentNullException.ThrowIfNull(replacements);
        var normalizedAnalyzerId = string.IsNullOrWhiteSpace(analyzerId)
            ? throw new ArgumentException("An analyzer ID is required.", nameof(analyzerId))
            : analyzerId.Trim();
        var normalizedKind = string.IsNullOrWhiteSpace(kind)
            ? throw new ArgumentException("An observation kind is required.", nameof(kind))
            : kind.Trim();

        if (_assets.All(asset => asset.Id != sourceAssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{sourceAssetId}' was not found.");
        if (replacements.Any(item => item.SourceAssetId != sourceAssetId
            || !string.Equals(item.AnalyzerId, normalizedAnalyzerId, StringComparison.Ordinal)
            || !string.Equals(item.Kind, normalizedKind, StringComparison.Ordinal)))
            throw new ArgumentException("Replacement observations must match the requested source, analyzer, and kind.", nameof(replacements));
        if (replacements.Select(item => item.Id).Distinct().Count() != replacements.Count)
            throw new ArgumentException("Replacement observation IDs must be unique.", nameof(replacements));

        foreach (var observation in replacements)
            ValidatePerformanceObservationReference(observation);
        var retainedIds = _performanceObservations
            .Where(item => item.SourceAssetId != sourceAssetId
                || !string.Equals(item.AnalyzerId, normalizedAnalyzerId, StringComparison.Ordinal)
                || !string.Equals(item.Kind, normalizedKind, StringComparison.Ordinal))
            .Select(item => item.Id)
            .ToHashSet();
        if (replacements.Any(item => retainedIds.Contains(item.Id)))
            throw new ArgumentException("Replacement observation IDs must be unique across the project.", nameof(replacements));

        var removedObservationIds = _performanceObservations
            .Where(item => item.SourceAssetId == sourceAssetId
                && string.Equals(item.AnalyzerId, normalizedAnalyzerId, StringComparison.Ordinal)
                && string.Equals(item.Kind, normalizedKind, StringComparison.Ordinal))
            .Select(item => item.Id)
            .ToHashSet();
        _performanceObservations.RemoveAll(item => item.SourceAssetId == sourceAssetId
            && string.Equals(item.AnalyzerId, normalizedAnalyzerId, StringComparison.Ordinal)
            && string.Equals(item.Kind, normalizedKind, StringComparison.Ordinal));
        RemoveDependentObservationRecords(removedObservationIds);
        _performanceObservations.AddRange(replacements);
        Touch();
    }

    public VocalTakePlacement? FindVocalTakePlacement(ProjectAssetId assetId) =>
        _vocalTakePlacements.SingleOrDefault(item => item.AssetId == assetId);

    public long VocalTakeStartTick(ProjectAssetId assetId)
    {
        var placement = FindVocalTakePlacement(assetId);
        return placement is null ? 0 : Timeline.ToAbsoluteTicks(placement.Start);
    }

    public VocalTakePlacement SetVocalTakePlacement(ProjectAssetId assetId, MusicalPosition start)
    {
        EnsureOriginalVocalAsset(assetId);
        ValidateMusicalPosition(start, TimeSignature);
        var now = DateTimeOffset.UtcNow;
        var index = _vocalTakePlacements.FindIndex(item => item.AssetId == assetId);
        var placement = index < 0
            ? new VocalTakePlacement(VocalTakePlacementId.New(), assetId, start, now, now)
            : _vocalTakePlacements[index].Relocate(start, now);
        if (index < 0) _vocalTakePlacements.Add(placement);
        else _vocalTakePlacements[index] = placement;
        Touch();
        return placement;
    }

    public VocalTakePlacement ClearVocalTakePlacement(ProjectAssetId assetId)
    {
        var existing = FindVocalTakePlacement(assetId)
            ?? throw new KeyNotFoundException($"Vocal take '{assetId}' has no song placement.");
        _vocalTakePlacements.Remove(existing);
        Touch();
        return existing;
    }

    public void RestoreVocalTakePlacement(VocalTakePlacement placement)
    {
        ArgumentNullException.ThrowIfNull(placement);
        EnsureOriginalVocalAsset(placement.AssetId);
        ValidateMusicalPosition(placement.Start, TimeSignature);
        _vocalTakePlacements.RemoveAll(item => item.Id == placement.Id || item.AssetId == placement.AssetId);
        _vocalTakePlacements.Add(placement);
        Touch();
    }

    public ProjectAsset RenameAsset(ProjectAssetId assetId, string name)
    {
        var index = _assets.FindIndex(item => item.Id == assetId);
        if (index < 0) throw new KeyNotFoundException($"Project asset '{assetId}' was not found.");
        var renamed = _assets[index].Rename(name);
        _assets[index] = renamed;
        Touch();
        return renamed;
    }

    public void Rename(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Project title is required.", nameof(title));
        if (title.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(title), "Project title cannot exceed 200 characters.");
        Title = title.Trim();
        Touch();
    }

    public void SetArtist(string artist)
    {
        ArgumentNullException.ThrowIfNull(artist);
        if (artist.Trim().Length > 200) throw new ArgumentOutOfRangeException(nameof(artist), "Artist cannot exceed 200 characters.");
        Artist = artist.Trim();
        Touch();
    }

    public void SetGenre(SongGenre genre)
    {
        if (!Enum.IsDefined(genre)) throw new ArgumentOutOfRangeException(nameof(genre), "Genre is invalid.");
        Genre = genre;
        Touch();
    }

    public void SetKey(MusicalKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        Touch();
    }

    public void SetDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);
        if (description.Length > 2_000) throw new ArgumentOutOfRangeException(nameof(description), "Description cannot exceed 2,000 characters.");
        Description = description.Trim();
        Touch();
    }

    public void SetRawLyricDraft(string rawLyricDraft)
    {
        ArgumentNullException.ThrowIfNull(rawLyricDraft);
        if (rawLyricDraft.Length > 100_000) throw new ArgumentOutOfRangeException(nameof(rawLyricDraft), "Raw lyrics cannot exceed 100,000 characters.");
        RawLyricDraft = rawLyricDraft;
        Touch();
    }

    public void SetTempo(decimal beatsPerMinute) { Timeline.TempoMap.SetInitialTempo(beatsPerMinute); Touch(); }

    public void SetTimeSignature(int numerator, int denominator)
    {
        EnsureNoMusicalPartsBeforeTimelineStructureChange();
        var proposed = new TimeSignatureEvent(0, numerator, denominator);
        ValidateAllSyllablePlacements(proposed);
        ValidateAllRhythmCandidates(proposed);
        ValidateAllHarmonyChords(proposed);
        ValidateAllHarmonyCandidates(proposed);
        ValidateVocalTakePlacements(proposed);
        Timeline.TimeSignatureMap.SetInitialTimeSignature(numerator, denominator);
        Touch();
    }

    public SongSection AddSection(SectionKind kind, string? title = null)
    {
        var section = SongSection.Create(kind, title);
        InsertSection(_sections.Count, section);
        return section;
    }

    public void InsertSection(int index, SongSection section, int durationBars = 8)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (index < 0 || index > _sections.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_sections.Any(item => item.Id == section.Id)) throw new InvalidOperationException($"Section '{section.Id}' already exists.");
        _sections.Insert(index, section);
        Timeline.ReflowSections(
            _sections.Select(item => item.Id).ToList(),
            new Dictionary<SectionId, int> { [section.Id] = durationBars });
        Touch();
    }

    public (SongSection Section, int Index, int DurationBars) RemoveSection(SectionId sectionId)
    {
        EnsureNoMusicalPartsBeforeTimelineStructureChange();
        var index = IndexOf(sectionId);
        var section = _sections[index];
        if (section.LyricLines.Any(line => IsLyricLineLocked(line.Id) || line.Phrases.Any(phrase => IsPhraseRhythmLocked(line.Id, phrase.Id))))
            throw new InvalidOperationException("Unlock the lyric or phrase rhythm in this section before removing it.");
        var durationBars = Timeline.FindSection(sectionId).DurationBars;
        _sections.RemoveAt(index);
        _arrangement.RemoveAll(item => item.SectionId == sectionId);
        _arrangementRoles.RemoveAll(item => item.SectionId == sectionId);
        _musicalParts.RemoveAll(item => item.SectionId == sectionId);
        Timeline.ReflowSections(_sections.Select(item => item.Id).ToList());
        ReconcileLocks();
        Touch();
        return (section, index, durationBars);
    }

    public void RenameSection(SectionId sectionId, string title) { FindSection(sectionId).Rename(title); Touch(); }

    public void SetSectionPerformanceIntent(SectionId sectionId, SectionDelivery delivery, string performanceNotes)
    {
        FindSection(sectionId).SetPerformanceIntent(delivery, performanceNotes);
        Touch();
    }

    public void SetSectionStructuralFunction(SectionId sectionId, StructuralFunction structuralFunction)
    {
        FindSection(sectionId).SetStructuralFunction(structuralFunction);
        Touch();
    }

    public SectionArrangement SetSectionArrangement(SectionId sectionId, SectionEnergy energy, SectionDensity density)
    {
        FindSection(sectionId);
        var index = _arrangement.FindIndex(item => item.SectionId == sectionId);
        var updated = index < 0
            ? new SectionArrangement(SectionArrangementId.New(), sectionId, energy, density, ArrangementProvenance.Manual)
            : new SectionArrangement(_arrangement[index].Id, sectionId, energy, density, ArrangementProvenance.Manual);
        if (index < 0) _arrangement.Add(updated); else _arrangement[index] = updated;
        Touch();
        return updated;
    }

    public SectionArrangement? FindSectionArrangement(SectionId sectionId) =>
        _arrangement.SingleOrDefault(item => item.SectionId == sectionId);

    public void RestoreSectionArrangement(SectionId sectionId, SectionArrangement? arrangement)
    {
        FindSection(sectionId);
        _arrangement.RemoveAll(item => item.SectionId == sectionId);
        if (arrangement is not null) _arrangement.Add(arrangement);
        Touch();
    }

    public SectionRoleAssignment? FindSectionRole(SectionId sectionId, ArrangementRole role) =>
        _arrangementRoles.SingleOrDefault(item => item.SectionId == sectionId && item.Role == role);

    public SectionRoleAssignment SetSectionRole(SectionId sectionId, ArrangementRole role)
    {
        FindSection(sectionId);
        var existing = FindSectionRole(sectionId, role);
        if (existing is not null) return existing;
        var created = new SectionRoleAssignment(SectionRoleAssignmentId.New(), sectionId, role, ArrangementProvenance.Manual);
        _arrangementRoles.Add(created);
        Touch();
        return created;
    }

    public SectionRoleAssignment RemoveSectionRole(SectionId sectionId, ArrangementRole role)
    {
        if (_musicalParts.Any(item => item.SectionId == sectionId && item.Role == role))
            throw new InvalidOperationException("Remove the musical part using this role before clearing the role from the section.");
        var existing = FindSectionRole(sectionId, role)
            ?? throw new KeyNotFoundException($"Arrangement role '{role}' is not assigned to section '{sectionId}'.");
        _arrangementRoles.Remove(existing);
        Touch();
        return existing;
    }

    public void RestoreSectionRole(SectionRoleAssignment assignment)
    {
        FindSection(assignment.SectionId);
        _arrangementRoles.RemoveAll(item => item.SectionId == assignment.SectionId && item.Role == assignment.Role);
        _arrangementRoles.Add(assignment);
        Touch();
    }

    public void RestoreSectionRoles(SectionId sectionId, IEnumerable<SectionRoleAssignment> assignments)
    {
        FindSection(sectionId);
        _arrangementRoles.RemoveAll(item => item.SectionId == sectionId);
        _arrangementRoles.AddRange(assignments);
        Touch();
    }

    public MusicalPart AddMusicalPart(SectionId sectionId, ArrangementRole role, string label, IReadOnlyList<NoteEventId> noteEventIds, string? instrumentProfileId = null)
    {
        FindSection(sectionId);
        if (FindSectionRole(sectionId, role) is null)
            throw new InvalidOperationException($"Assign the {role} role to this section before creating its musical part.");
        var created = new MusicalPart(MusicalPartId.New(), sectionId, role, label, noteEventIds, ArrangementProvenance.Manual, instrumentProfileId);
        RestoreMusicalPart(created);
        return created;
    }

    public MusicalPart RemoveMusicalPart(MusicalPartId musicalPartId)
    {
        var existing = _musicalParts.SingleOrDefault(item => item.Id == musicalPartId)
            ?? throw new KeyNotFoundException($"Musical part '{musicalPartId}' was not found.");
        _musicalParts.Remove(existing);
        Touch();
        return existing;
    }

    public MusicalPart SetMusicalPart(MusicalPartId musicalPartId, string label, IReadOnlyList<NoteEventId> noteEventIds, string? instrumentProfileId)
    {
        var existing = _musicalParts.SingleOrDefault(item => item.Id == musicalPartId)
            ?? throw new KeyNotFoundException($"Musical part '{musicalPartId}' was not found.");
        var updated = existing.With(label, noteEventIds, instrumentProfileId);
        ValidateMusicalPartReferences(updated);
        _musicalParts[_musicalParts.IndexOf(existing)] = updated;
        Touch();
        return updated;
    }

    public void RestoreMusicalPart(MusicalPart musicalPart)
    {
        ArgumentNullException.ThrowIfNull(musicalPart);
        ValidateMusicalPartReferences(musicalPart);
        _musicalParts.RemoveAll(item => item.Id == musicalPart.Id);
        _musicalParts.Add(musicalPart);
        Touch();
    }

    public void RestoreMusicalParts(SectionId sectionId, IEnumerable<MusicalPart> musicalParts)
    {
        FindSection(sectionId);
        _musicalParts.RemoveAll(item => item.SectionId == sectionId);
        foreach (var musicalPart in musicalParts) RestoreMusicalPart(musicalPart);
        Touch();
    }

    public void SetSectionDuration(SectionId sectionId, int durationBars)
    {
        EnsureNoMusicalPartsBeforeTimelineStructureChange();
        var section = FindSection(sectionId);
        if (section.LyricLines.SelectMany(line => line.SyllablePlacements).Any(item => item.Position.Bar > durationBars))
            throw new InvalidOperationException("Section duration cannot end before an existing syllable placement. Clear or move the placement first.");
        if (section.LyricLines.SelectMany(line => line.RhythmCandidates).SelectMany(item => item.Events).Any(item => item.BeatPosition.Bar > durationBars))
            throw new InvalidOperationException("Section duration cannot end before an existing rhythm option. Remove that option first.");
        if (section.Harmony.Any(item => item.Start.Bar > durationBars || item.Start.Bar + item.DurationBars - 1 > durationBars))
            throw new InvalidOperationException("Section duration cannot end before an existing harmony chord. Move or shorten the chord first.");
        if (section.HarmonyCandidates.SelectMany(item => item.Events).Any(item => item.Start.Bar > durationBars || item.Start.Bar + item.DurationBars - 1 > durationBars))
            throw new InvalidOperationException("Section duration cannot end before an existing harmony option. Remove that option first.");
        Timeline.SetSectionDuration(sectionId, durationBars, _sections.Select(item => item.Id).ToList());
        Touch();
    }

    public NoteEvent AddNoteEvent(RegisteredPitch pitch, long startTick, long durationTicks, int velocity)
    {
        var created = new NoteEvent(NoteEventId.New(), pitch, startTick, durationTicks, velocity);
        RestoreNoteEvent(created);
        return created;
    }

    public NoteEvent SetNoteEvent(NoteEventId noteEventId, RegisteredPitch pitch, long startTick, long durationTicks, int velocity)
    {
        var index = _noteEvents.FindIndex(item => item.Id == noteEventId);
        if (index < 0) throw new KeyNotFoundException($"Note event '{noteEventId}' was not found.");
        var existing = _noteEvents[index];
        var updated = existing.With(pitch, startTick, durationTicks, velocity);
        _noteEvents[index] = updated;
        try
        {
            foreach (var part in _musicalParts.Where(item => item.NoteEventIds.Contains(noteEventId)))
                ValidateMusicalPartReferences(part);
        }
        catch
        {
            _noteEvents[index] = existing;
            throw;
        }
        SortNoteEvents();
        Touch();
        return updated;
    }

    public NoteEvent RemoveNoteEvent(NoteEventId noteEventId)
    {
        if (_musicalParts.Any(item => item.NoteEventIds.Contains(noteEventId)))
            throw new InvalidOperationException("Remove this note from its musical part before deleting the note.");
        var existing = _noteEvents.SingleOrDefault(item => item.Id == noteEventId)
            ?? throw new KeyNotFoundException($"Note event '{noteEventId}' was not found.");
        _noteEvents.Remove(existing);
        Touch();
        return existing;
    }

    public void RestoreNoteEvent(NoteEvent noteEvent)
    {
        ArgumentNullException.ThrowIfNull(noteEvent);
        _noteEvents.RemoveAll(item => item.Id == noteEvent.Id);
        _noteEvents.Add(noteEvent);
        SortNoteEvents();
        Touch();
    }

    public ExpressionCurve AddExpressionCurve(
        string name,
        ExpressionCurveKind kind,
        IReadOnlyList<ExpressionCurvePoint> points,
        string? instrumentProfileId = null)
    {
        var created = new ExpressionCurve(ExpressionCurveId.New(), name, kind, points, instrumentProfileId);
        RestoreExpressionCurve(created);
        return created;
    }

    public ExpressionCurve RemoveExpressionCurve(ExpressionCurveId expressionCurveId)
    {
        var existing = _expressionCurves.SingleOrDefault(item => item.Id == expressionCurveId)
            ?? throw new KeyNotFoundException($"Expression curve '{expressionCurveId}' was not found.");
        _expressionCurves.Remove(existing);
        Touch();
        return existing;
    }

    public void RestoreExpressionCurve(ExpressionCurve curve)
    {
        ArgumentNullException.ThrowIfNull(curve);
        _expressionCurves.RemoveAll(item => item.Id == curve.Id);
        _expressionCurves.Add(curve);
        Touch();
    }

    public ExpressionCurve? FindExpressionCurve(ExpressionCurveId expressionCurveId) =>
        _expressionCurves.SingleOrDefault(item => item.Id == expressionCurveId);

    public HarmonyChord AddHarmonyChord(SectionId sectionId, ChordSymbol chord, BeatPosition start, int durationBars = 1)
    {
        ValidateHarmonySpan(sectionId, start, durationBars, TimeSignature);
        var created = FindSection(sectionId).AddHarmonyChord(chord, start, durationBars);
        Touch();
        return created;
    }

    public HarmonyChord SetHarmonyChord(
        SectionId sectionId,
        HarmonyChordId harmonyChordId,
        ChordSymbol chord,
        BeatPosition start,
        int durationBars)
    {
        ValidateHarmonySpan(sectionId, start, durationBars, TimeSignature);
        var section = FindSection(sectionId);
        var existing = section.FindHarmonyChord(harmonyChordId);
        var updated = existing.With(chord, start, durationBars);
        section.UpsertHarmonyChord(updated);
        Touch();
        return updated;
    }

    public HarmonyChord RemoveHarmonyChord(SectionId sectionId, HarmonyChordId harmonyChordId)
    {
        var removed = FindSection(sectionId).RemoveHarmonyChord(harmonyChordId);
        Touch();
        return removed;
    }

    public HarmonyChord SetChordVoicing(SectionId sectionId, HarmonyChordId harmonyChordId, IReadOnlyList<RegisteredPitch>? pitches, int minimumMidiNote = 21, int maximumMidiNote = 108)
    {
        var section = FindSection(sectionId);
        var existing = section.FindHarmonyChord(harmonyChordId);
        ChordVoicing? voicing = null;
        if (pitches is { Count: > 0 })
        {
            var prior = existing.Voicing;
            var voices = pitches.Select((pitch, position) => new ChordVoice(
                prior is not null && position < prior.Voices.Count ? prior.Voices[position].Id : ChordVoiceId.New(),
                position, pitch, HarmonyProvenance.Manual)).ToList();
            voicing = new ChordVoicing(prior?.Id ?? ChordVoicingId.New(), minimumMidiNote, maximumMidiNote, voices);
        }
        var updated = existing.With(voicing: voicing, replaceVoicing: true);
        section.UpsertHarmonyChord(updated);
        Touch();
        return updated;
    }

    public void RestoreChordVoicing(SectionId sectionId, HarmonyChordId harmonyChordId, ChordVoicing? voicing)
    {
        var section = FindSection(sectionId);
        var existing = section.FindHarmonyChord(harmonyChordId);
        section.UpsertHarmonyChord(existing.With(voicing: voicing, replaceVoicing: true));
        Touch();
    }

    public void ReinsertHarmonyChord(SectionId sectionId, HarmonyChord chord)
    {
        ArgumentNullException.ThrowIfNull(chord);
        ValidateHarmonySpan(sectionId, chord.Start, chord.DurationBars, TimeSignature);
        FindSection(sectionId).UpsertHarmonyChord(chord);
        Touch();
    }

    public void ReplaceSectionHarmony(SectionId sectionId, IEnumerable<HarmonyChord> chords)
    {
        ArgumentNullException.ThrowIfNull(chords);
        var replacement = chords.ToList();
        foreach (var chord in replacement)
            ValidateHarmonySpan(sectionId, chord.Start, chord.DurationBars, TimeSignature);
        FindSection(sectionId).SetHarmony(replacement);
        Touch();
    }

    public HarmonyCandidate CaptureHarmonyCandidate(SectionId sectionId, string label)
    {
        var section = FindSection(sectionId);
        if (section.Harmony.Count == 0)
            throw new InvalidOperationException("Add at least one harmony chord before capturing an option.");
        var candidate = section.CaptureHarmonyCandidate(label);
        Touch();
        return candidate;
    }

    public void ReinsertHarmonyCandidate(SectionId sectionId, int index, HarmonyCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        foreach (var item in candidate.Events)
            ValidateHarmonySpan(sectionId, item.Start, item.DurationBars, TimeSignature);
        FindSection(sectionId).InsertHarmonyCandidate(index, candidate);
        Touch();
    }

    public (HarmonyCandidate Candidate, int Index) RemoveHarmonyCandidate(SectionId sectionId, HarmonyCandidateId candidateId)
    {
        var removed = FindSection(sectionId).RemoveHarmonyCandidate(candidateId);
        Touch();
        return removed;
    }

    public void RenameHarmonyCandidate(SectionId sectionId, HarmonyCandidateId candidateId, string label)
    {
        FindSection(sectionId).RenameHarmonyCandidate(candidateId, label);
        Touch();
    }

    public void ApplyHarmonyCandidate(SectionId sectionId, HarmonyCandidateId candidateId)
    {
        var section = FindSection(sectionId);
        var candidate = section.FindHarmonyCandidate(candidateId);
        foreach (var item in candidate.Events)
            ValidateHarmonySpan(sectionId, item.Start, item.DurationBars, TimeSignature);
        var existing = section.Harmony.ToList();
        section.SetHarmony(candidate.Events.Select((item, position) => new HarmonyChord(
            position < existing.Count ? existing[position].Id : HarmonyChordId.New(),
            item.Chord,
            item.Start,
            item.DurationBars,
            candidate.Provenance,
            position < existing.Count && existing[position].Chord.PitchClasses.Select(x => x.Value).SequenceEqual(item.Chord.PitchClasses.Select(x => x.Value)) ? existing[position].Voicing : null)));
        Touch();
    }

    public void SetSyllablePlacement(
        SectionId sectionId,
        LyricLineId lineId,
        SyllableId syllableId,
        BeatPosition? position,
        PlacementProvenance provenance = PlacementProvenance.Manual)
    {
        EnsurePhraseRhythmUnlockedForSyllable(lineId, syllableId);
        if (position is not null) ValidateBeatPosition(sectionId, position.Value, TimeSignature);
        FindSection(sectionId).FindLyricLine(lineId).SetSyllablePlacement(syllableId, position, provenance);
        Touch();
    }

    public MusicalPosition ResolveSyllablePosition(SectionId sectionId, BeatPosition position)
    {
        ValidateBeatPosition(sectionId, position, TimeSignature);
        var section = Timeline.FindSection(sectionId);
        return new MusicalPosition(section.Start.Bar + position.Bar - 1, position.Beat, position.Tick);
    }

    public RhythmCandidate CaptureRhythmCandidate(
        SectionId sectionId,
        LyricLineId lineId,
        LyricPhraseId phraseId,
        string label,
        RhythmCandidateProvenance provenance = RhythmCandidateProvenance.Manual)
    {
        var candidate = FindSection(sectionId).FindLyricLine(lineId)
            .CaptureRhythmCandidate(phraseId, label, provenance);
        Touch();
        return candidate;
    }

    public void ApplyRhythmCandidate(SectionId sectionId, LyricLineId lineId, RhythmCandidateId candidateId)
    {
        var line = FindSection(sectionId).FindLyricLine(lineId);
        var candidate = line.RhythmCandidates.SingleOrDefault(item => item.Id == candidateId)
            ?? throw new KeyNotFoundException($"Rhythm candidate '{candidateId}' was not found.");
        EnsurePhraseRhythmUnlocked(lineId, candidate.PhraseId);
        foreach (var item in candidate.Events) ValidateBeatPosition(sectionId, item.BeatPosition, TimeSignature);
        line.ApplyRhythmCandidate(candidateId);
        Touch();
    }

    public CreativeLock LockLyricLine(LyricLineId lineId, LockProvenance provenance = LockProvenance.Manual)
    {
        EnsureLineExists(lineId);
        if (IsLyricLineLocked(lineId))
            throw new InvalidOperationException($"Lyric line '{lineId}' is already locked.");
        var lockItem = new CreativeLock(CreativeLockId.New(), CreativeLockScope.LyricLine, lineId, null, provenance);
        _locks.Add(lockItem);
        Touch();
        return lockItem;
    }

    public CreativeLock LockPhraseRhythm(
        LyricLineId lineId,
        LyricPhraseId phraseId,
        LockProvenance provenance = LockProvenance.Manual)
    {
        EnsurePhraseExists(lineId, phraseId);
        if (IsPhraseRhythmLocked(lineId, phraseId))
            throw new InvalidOperationException($"Phrase rhythm '{phraseId}' is already locked.");
        var lockItem = new CreativeLock(
            CreativeLockId.New(),
            CreativeLockScope.PhraseRhythm,
            lineId,
            phraseId,
            provenance);
        _locks.Add(lockItem);
        Touch();
        return lockItem;
    }

    public (CreativeLock Lock, int Index) Unlock(CreativeLockId lockId)
    {
        var index = _locks.FindIndex(item => item.Id == lockId);
        if (index < 0) throw new KeyNotFoundException($"Creative lock '{lockId}' was not found.");
        var lockItem = _locks[index];
        _locks.RemoveAt(index);
        Touch();
        return (lockItem, index);
    }

    public void InsertLock(int index, CreativeLock lockItem)
    {
        ArgumentNullException.ThrowIfNull(lockItem);
        if (index < 0 || index > _locks.Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (_locks.Any(item => item.Id == lockItem.Id))
            throw new InvalidOperationException($"Creative lock '{lockItem.Id}' already exists.");
        _locks.Insert(index, CloneLock(lockItem));
        ValidateLockReferences();
        Touch();
    }

    public void RestoreLocks(IReadOnlyList<CreativeLock> locks)
    {
        ArgumentNullException.ThrowIfNull(locks);
        _locks.Clear();
        _locks.AddRange(locks.Select(CloneLock));
        ValidateLockReferences();
    }

    public bool IsLyricLineLocked(LyricLineId lineId) =>
        _locks.Any(item => item.Scope == CreativeLockScope.LyricLine && item.LineId == lineId);

    public bool IsPhraseRhythmLocked(LyricLineId lineId, LyricPhraseId phraseId) =>
        _locks.Any(item =>
            item.Scope == CreativeLockScope.PhraseRhythm
            && item.LineId == lineId
            && item.PhraseId == phraseId);

    public void EnsureLyricLineUnlocked(LyricLineId lineId)
    {
        if (IsLyricLineLocked(lineId))
            throw new InvalidOperationException("This lyric line is locked. Unlock it before editing the words.");
    }

    public void EnsurePhraseRhythmUnlocked(LyricLineId lineId, LyricPhraseId phraseId)
    {
        if (IsPhraseRhythmLocked(lineId, phraseId))
            throw new InvalidOperationException("This phrase rhythm is locked. Unlock it before changing placements.");
    }

    public void EnsurePhraseRhythmUnlockedForSyllable(LyricLineId lineId, SyllableId syllableId)
    {
        var line = FindLine(lineId);
        var phrase = line.Phrases.FirstOrDefault(item =>
            item.WordIds.SelectMany(wordId => line.Words.Single(word => word.Id == wordId).Syllables)
                .Any(syllable => syllable.Id == syllableId));
        if (phrase is not null) EnsurePhraseRhythmUnlocked(lineId, phrase.Id);
    }

    public void ReconcileLocks()
    {
        var lines = _sections.SelectMany(section => section.LyricLines).ToDictionary(item => item.Id);
        var surviving = _locks.Where(item =>
        {
            if (!lines.TryGetValue(item.LineId, out var line)) return false;
            if (item.Scope == CreativeLockScope.LyricLine) return true;
            return item.PhraseId is not null && line.Phrases.Any(phrase => phrase.Id == item.PhraseId);
        }).Select(CloneLock).ToList();
        _locks.Clear();
        _locks.AddRange(surviving);
    }

    public void MoveSection(SectionId sectionId, int targetIndex)
    {
        EnsureNoMusicalPartsBeforeTimelineStructureChange();
        if (targetIndex < 0 || targetIndex >= _sections.Count) throw new ArgumentOutOfRangeException(nameof(targetIndex));
        var currentIndex = IndexOf(sectionId);
        if (currentIndex == targetIndex) return;
        var section = _sections[currentIndex];
        _sections.RemoveAt(currentIndex);
        _sections.Insert(targetIndex, section);
        Timeline.ReflowSections(_sections.Select(item => item.Id).ToList());
        Touch();
    }

    public SongSection FindSection(SectionId sectionId) =>
        _sections.SingleOrDefault(section => section.Id == sectionId)
        ?? throw new KeyNotFoundException($"Section '{sectionId}' was not found.");

    public int IndexOf(SectionId sectionId)
    {
        var index = _sections.FindIndex(section => section.Id == sectionId);
        return index >= 0 ? index : throw new KeyNotFoundException($"Section '{sectionId}' was not found.");
    }

    private void EnsureUniqueIds()
    {
        if (_sections.Select(section => section.Id).Distinct().Count() != _sections.Count)
            throw new ArgumentException("Section IDs must be unique.");
        if (_tracks.Select(track => track.Id).Distinct().Count() != _tracks.Count)
            throw new ArgumentException("Track IDs must be unique.");
        if (_noteEvents.Select(item => item.Id).Distinct().Count() != _noteEvents.Count)
            throw new ArgumentException("Note-event IDs must be unique.");
        if (_musicalParts.Select(item => item.Id).Distinct().Count() != _musicalParts.Count)
            throw new ArgumentException("Musical-part IDs must be unique.");
        if (_assets.Select(item => item.Id).Distinct().Count() != _assets.Count)
            throw new ArgumentException("Project asset IDs must be unique.");
        if (_performanceObservations.Select(item => item.Id).Distinct().Count() != _performanceObservations.Count)
            throw new ArgumentException("Performance observation IDs must be unique.");
        if (_performanceObservationReviews.Select(item => item.Id).Distinct().Count() != _performanceObservationReviews.Count)
            throw new ArgumentException("Performance observation review IDs must be unique.");
        if (_performanceObservationReviews.Select(item => item.ObservationId).Distinct().Count() != _performanceObservationReviews.Count)
            throw new ArgumentException("A performance observation can have only one artist review.");
        if (_performanceObservationCorrections.Select(item => item.Id).Distinct().Count() != _performanceObservationCorrections.Count)
            throw new ArgumentException("Performance observation correction IDs must be unique.");
        if (_performanceObservationCorrections.Select(item => item.ObservationId).Distinct().Count() != _performanceObservationCorrections.Count)
            throw new ArgumentException("A performance observation can have only one artist correction.");
        if (_performanceObservationGestures.Select(item => item.Id).Distinct().Count() != _performanceObservationGestures.Count)
            throw new ArgumentException("Performance observation gesture IDs must be unique.");
        if (_performanceObservationGestures.Select(item => item.ObservationId).Distinct().Count() != _performanceObservationGestures.Count)
            throw new ArgumentException("A performance observation can have only one artist gesture.");
        if (_vocalTakePlacements.Select(item => item.Id).Distinct().Count() != _vocalTakePlacements.Count)
            throw new ArgumentException("Vocal-take placement IDs must be unique.");
        if (_vocalTakePlacements.Select(item => item.AssetId).Distinct().Count() != _vocalTakePlacements.Count)
            throw new ArgumentException("An original vocal take can have only one song placement.");
        if (_expressionCurves.Select(item => item.Id).Distinct().Count() != _expressionCurves.Count)
            throw new ArgumentException("Expression-curve IDs must be unique.");
        if (_arrangement.Select(item => item.Id).Distinct().Count() != _arrangement.Count)
            throw new ArgumentException("Section arrangement IDs must be unique.");
        if (_arrangement.Select(item => item.SectionId).Distinct().Count() != _arrangement.Count)
            throw new ArgumentException("A section can have only one arrangement plan.");
        if (_arrangement.Any(item => _sections.All(section => section.Id != item.SectionId)))
            throw new ArgumentException("Every section arrangement must reference an existing section.");
        if (_arrangementRoles.Select(item => item.Id).Distinct().Count() != _arrangementRoles.Count)
            throw new ArgumentException("Section role-assignment IDs must be unique.");
        if (_arrangementRoles.Select(item => (item.SectionId, item.Role)).Distinct().Count() != _arrangementRoles.Count)
            throw new ArgumentException("A role can be assigned only once within a section.");
        if (_arrangementRoles.Any(item => _sections.All(section => section.Id != item.SectionId)))
            throw new ArgumentException("Every arrangement role must reference an existing section.");
        foreach (var musicalPart in _musicalParts) ValidateMusicalPartReferences(musicalPart);
        var lines = _sections.SelectMany(section => section.LyricLines).ToList();
        if (lines.Select(line => line.Id).Distinct().Count() != lines.Count)
            throw new ArgumentException("Lyric line IDs must be unique across the project.");
        var words = lines.SelectMany(line => line.Words).ToList();
        if (words.Select(word => word.Id).Distinct().Count() != words.Count)
            throw new ArgumentException("Lyric word IDs must be unique across the project.");
        var syllables = words.SelectMany(word => word.Syllables).ToList();
        if (syllables.Select(syllable => syllable.Id).Distinct().Count() != syllables.Count)
            throw new ArgumentException("Syllable IDs must be unique across the project.");
        var punctuation = lines.SelectMany(line => line.Punctuation).ToList();
        if (punctuation.Select(item => item.Id).Distinct().Count() != punctuation.Count)
            throw new ArgumentException("Punctuation IDs must be unique across the project.");
        var phrases = lines.SelectMany(line => line.Phrases).ToList();
        if (phrases.Select(item => item.Id).Distinct().Count() != phrases.Count)
            throw new ArgumentException("Lyric phrase IDs must be unique across the project.");
        var patterns = phrases.Where(item => item.Prosody is not null).Select(item => item.Prosody!).ToList();
        if (patterns.Select(item => item.Id).Distinct().Count() != patterns.Count)
            throw new ArgumentException("Prosodic pattern IDs must be unique across the project.");
        var prosodicUnits = patterns.SelectMany(item => item.Units).ToList();
        if (prosodicUnits.Select(item => item.Id).Distinct().Count() != prosodicUnits.Count)
            throw new ArgumentException("Prosodic unit IDs must be unique across the project.");
        var syllablePlacements = lines.SelectMany(line => line.SyllablePlacements).ToList();
        if (syllablePlacements.Select(item => item.Id).Distinct().Count() != syllablePlacements.Count)
            throw new ArgumentException("Syllable placement IDs must be unique across the project.");
        var rhythmCandidates = lines.SelectMany(line => line.RhythmCandidates).ToList();
        if (rhythmCandidates.Select(item => item.Id).Distinct().Count() != rhythmCandidates.Count)
            throw new ArgumentException("Rhythm candidate IDs must be unique across the project.");
        var rhythmEvents = rhythmCandidates.SelectMany(item => item.Events).ToList();
        if (rhythmEvents.Select(item => item.Id).Distinct().Count() != rhythmEvents.Count)
            throw new ArgumentException("Rhythm candidate event IDs must be unique across the project.");
        var breathPoints = lines.SelectMany(line => line.BreathPoints).ToList();
        if (breathPoints.Select(item => item.Id).Distinct().Count() != breathPoints.Count)
            throw new ArgumentException("Breath point IDs must be unique across the project.");
        if (_locks.Select(item => item.Id).Distinct().Count() != _locks.Count)
            throw new ArgumentException("Creative lock IDs must be unique across the project.");
        var harmonyChords = _sections.SelectMany(section => section.Harmony).ToList();
        if (harmonyChords.Select(item => item.Id).Distinct().Count() != harmonyChords.Count)
            throw new ArgumentException("Harmony chord IDs must be unique across the project.");
        var harmonyCandidates = _sections.SelectMany(section => section.HarmonyCandidates).ToList();
        if (harmonyCandidates.Select(item => item.Id).Distinct().Count() != harmonyCandidates.Count)
            throw new ArgumentException("Harmony candidate IDs must be unique across the project.");
        var harmonyCandidateEvents = harmonyCandidates.SelectMany(item => item.Events).ToList();
        if (harmonyCandidateEvents.Select(item => item.Id).Distinct().Count() != harmonyCandidateEvents.Count)
            throw new ArgumentException("Harmony candidate event IDs must be unique across the project.");
    }

    private void ValidatePerformanceObservationReferences()
    {
        foreach (var observation in _performanceObservations)
            ValidatePerformanceObservationReference(observation);
    }

    private void ValidatePerformanceObservationReference(PerformanceObservation observation)
    {
        if (_assets.All(asset => asset.Id != observation.SourceAssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new ArgumentException($"Performance observation '{observation.Id}' must reference an existing original vocal asset.");
    }

    private void EnsureOriginalVocalAsset(ProjectAssetId assetId)
    {
        if (_assets.All(asset => asset.Id != assetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
            throw new KeyNotFoundException($"Original vocal asset '{assetId}' was not found.");
    }

    private void ValidateVocalTakePlacementReferences() => ValidateVocalTakePlacements(TimeSignature);

    private void ValidateVocalTakePlacements(TimeSignatureEvent meter)
    {
        foreach (var placement in _vocalTakePlacements)
        {
            if (_assets.All(asset => asset.Id != placement.AssetId || asset.Kind != ProjectAssetKind.OriginalVocalTake))
                throw new ArgumentException($"Vocal-take placement '{placement.Id}' must reference an existing original vocal asset.");
            ValidateMusicalPosition(placement.Start, meter);
        }
    }

    private void ValidateMusicalPosition(MusicalPosition position, TimeSignatureEvent meter)
    {
        if (position.Beat > meter.Numerator)
            throw new ArgumentOutOfRangeException(nameof(position), $"Beat must be between 1 and {meter.Numerator} for the current meter.");
        var ticksPerBeat = checked(Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        if (position.Tick >= ticksPerBeat)
            throw new ArgumentOutOfRangeException(nameof(position), $"Tick must be between 0 and {ticksPerBeat - 1} for the current meter.");
    }

    private void ValidatePerformanceObservationReviewReferences()
    {
        foreach (var review in _performanceObservationReviews)
            if (_performanceObservations.All(observation => observation.Id != review.ObservationId))
                throw new ArgumentException($"Performance observation review '{review.Id}' must reference an existing observation.");
    }

    private void ValidatePerformanceObservationCorrectionReferences()
    {
        var reviews = _performanceObservationReviews.ToDictionary(item => item.ObservationId);
        var observations = _performanceObservations.ToDictionary(item => item.Id);
        foreach (var correction in _performanceObservationCorrections)
        {
            if (!observations.TryGetValue(correction.ObservationId, out var observation))
                throw new ArgumentException($"Performance observation correction '{correction.Id}' must reference an existing observation.");
            if (!reviews.TryGetValue(correction.ObservationId, out var review)
                || review.Verdict != PerformanceObservationReviewVerdict.Inaccurate)
                throw new ArgumentException($"Performance observation correction '{correction.Id}' can exist only for an inaccurate claim.");
            PerformanceObservationCorrection.ValidateAgainst(observation, correction.Measurements);
        }
    }

    private void ValidatePerformanceObservationGestureReferences()
    {
        foreach (var gesture in _performanceObservationGestures)
        {
            if (!TryGetApprovedGestureMeasurements(gesture.ObservationId, out var approved))
                throw new ArgumentException($"Performance observation gesture '{gesture.Id}' can exist only for an accurate claim or an inaccurate claim that already has a correction.");
            var observation = _performanceObservations.Single(item => item.Id == gesture.ObservationId);
            PerformanceObservationGesture.ValidateAgainst(observation, gesture.Measurements);
            if (!PerformanceObservationGesture.ValuesEqual(gesture.Measurements, approved))
                throw new ArgumentException($"Performance observation gesture '{gesture.Id}' must copy the currently approved measurements.");
        }
    }

    private bool TryGetApprovedGestureMeasurements(
        PerformanceObservationId observationId,
        out IReadOnlyList<PerformanceMeasurement> measurements)
    {
        measurements = [];
        var observation = _performanceObservations.SingleOrDefault(item => item.Id == observationId);
        if (observation is null) return false;
        var review = _performanceObservationReviews.SingleOrDefault(item => item.ObservationId == observationId);
        if (review is null) return false;
        if (review.Verdict == PerformanceObservationReviewVerdict.Accurate)
        {
            measurements = observation.Measurements;
            return true;
        }

        var correction = _performanceObservationCorrections.SingleOrDefault(item => item.ObservationId == observationId);
        if (review.Verdict != PerformanceObservationReviewVerdict.Inaccurate || correction is null) return false;
        measurements = correction.Measurements;
        return true;
    }

    private void RefreshOrDropGesture(PerformanceObservationId observationId, DateTimeOffset updatedUtc)
    {
        var index = _performanceObservationGestures.FindIndex(item => item.ObservationId == observationId);
        if (index < 0) return;
        if (!TryGetApprovedGestureMeasurements(observationId, out var measurements))
        {
            _performanceObservationGestures.RemoveAt(index);
            return;
        }

        _performanceObservationGestures[index] = _performanceObservationGestures[index].Revise(measurements, updatedUtc);
    }

    private void RemoveDependentObservationRecords(IEnumerable<PerformanceObservationId> observationIds)
    {
        var removed = observationIds as IReadOnlySet<PerformanceObservationId> ?? observationIds.ToHashSet();
        _performanceObservationReviews.RemoveAll(item => removed.Contains(item.ObservationId));
        _performanceObservationCorrections.RemoveAll(item => removed.Contains(item.ObservationId));
        _performanceObservationGestures.RemoveAll(item => removed.Contains(item.ObservationId));
    }

    private void ValidateLockReferences()
    {
        var lines = _sections.SelectMany(section => section.LyricLines).ToDictionary(item => item.Id);
        foreach (var lockItem in _locks)
        {
            if (!lines.TryGetValue(lockItem.LineId, out var line))
                throw new ArgumentException($"Creative lock '{lockItem.Id}' references a lyric line that does not exist.");
            if (lockItem.Scope == CreativeLockScope.PhraseRhythm
                && (lockItem.PhraseId is null || line.Phrases.All(phrase => phrase.Id != lockItem.PhraseId)))
                throw new ArgumentException($"Creative lock '{lockItem.Id}' references a phrase that does not exist.");
        }

        var lineLocks = _locks.Where(item => item.Scope == CreativeLockScope.LyricLine).Select(item => item.LineId).ToList();
        if (lineLocks.Count != lineLocks.Distinct().Count())
            throw new ArgumentException("A lyric line can have only one lyric lock.");
        var phraseLocks = _locks
            .Where(item => item.Scope == CreativeLockScope.PhraseRhythm)
            .Select(item => (item.LineId, item.PhraseId!.Value))
            .ToList();
        if (phraseLocks.Count != phraseLocks.Distinct().Count())
            throw new ArgumentException("A phrase can have only one rhythm lock.");
    }

    private void ValidateMusicalPartReferences(MusicalPart musicalPart)
    {
        FindSection(musicalPart.SectionId);
        if (FindSectionRole(musicalPart.SectionId, musicalPart.Role) is null)
            throw new ArgumentException("Every musical part must reference an assigned section role.");
        var notes = musicalPart.NoteEventIds.Select(id => _noteEvents.SingleOrDefault(item => item.Id == id)
            ?? throw new ArgumentException($"Musical part '{musicalPart.Id}' references a note that does not exist.")).ToList();
        var placement = Timeline.FindSection(musicalPart.SectionId);
        var sectionStart = Timeline.ToAbsoluteTicks(placement.Start);
        var meter = TimeSignature;
        var ticksPerBeat = checked(Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        var sectionEnd = checked(sectionStart + (long)placement.DurationBars * meter.Numerator * ticksPerBeat);
        if (notes.Any(note => note.StartTick < sectionStart || note.StartTick >= sectionEnd))
            throw new ArgumentException("Every note in a musical part must begin within its section.");
    }

    private LyricLine FindLine(LyricLineId lineId) =>
        _sections.SelectMany(section => section.LyricLines).SingleOrDefault(line => line.Id == lineId)
        ?? throw new KeyNotFoundException($"Lyric line '{lineId}' was not found.");

    private void EnsureLineExists(LyricLineId lineId) => FindLine(lineId);

    private void EnsureNoMusicalPartsBeforeTimelineStructureChange()
    {
        if (_musicalParts.Count > 0)
            throw new InvalidOperationException("Remove musical parts before changing section order, duration, or meter. Their approved notes will remain in the song.");
    }

    private void EnsurePhraseExists(LyricLineId lineId, LyricPhraseId phraseId)
    {
        var line = FindLine(lineId);
        if (line.Phrases.All(item => item.Id != phraseId))
            throw new KeyNotFoundException($"Lyric phrase '{phraseId}' was not found in lyric line '{lineId}'.");
    }

    private static CreativeLock CloneLock(CreativeLock lockItem) => new(
        lockItem.Id,
        lockItem.Scope,
        lockItem.LineId,
        lockItem.PhraseId,
        lockItem.Provenance);

    private void SortNoteEvents() => _noteEvents.Sort((left, right) =>
    {
        var tickComparison = left.StartTick.CompareTo(right.StartTick);
        return tickComparison != 0 ? tickComparison : left.Pitch.MidiNumber.CompareTo(right.Pitch.MidiNumber);
    });

    private void ValidateAllSyllablePlacements(TimeSignatureEvent meter)
    {
        foreach (var section in _sections)
        foreach (var placement in section.LyricLines.SelectMany(line => line.SyllablePlacements))
            ValidateBeatPosition(section.Id, placement.Position, meter);
    }

    private void ValidateAllRhythmCandidates(TimeSignatureEvent meter)
    {
        foreach (var section in _sections)
        foreach (var rhythmEvent in section.LyricLines.SelectMany(line => line.RhythmCandidates).SelectMany(item => item.Events))
            ValidateBeatPosition(section.Id, rhythmEvent.BeatPosition, meter);
    }

    private void ValidateAllHarmonyChords(TimeSignatureEvent meter)
    {
        foreach (var section in _sections)
        foreach (var chord in section.Harmony)
            ValidateHarmonySpan(section.Id, chord.Start, chord.DurationBars, meter);
    }

    private void ValidateAllHarmonyCandidates(TimeSignatureEvent meter)
    {
        foreach (var section in _sections)
        foreach (var harmonyEvent in section.HarmonyCandidates.SelectMany(item => item.Events))
            ValidateHarmonySpan(section.Id, harmonyEvent.Start, harmonyEvent.DurationBars, meter);
    }

    private void ValidateHarmonySpan(SectionId sectionId, BeatPosition start, int durationBars, TimeSignatureEvent meter)
    {
        ValidateBeatPosition(sectionId, start, meter);
        var section = Timeline.FindSection(sectionId);
        if (start.Bar + durationBars - 1 > section.DurationBars)
            throw new ArgumentOutOfRangeException(nameof(durationBars), "Harmony chord must end within the section duration.");
    }

    private void ValidateBeatPosition(SectionId sectionId, BeatPosition position, TimeSignatureEvent meter)
    {
        var section = Timeline.FindSection(sectionId);
        if (position.Bar > section.DurationBars)
            throw new ArgumentOutOfRangeException(nameof(position), $"Bar must be between 1 and {section.DurationBars} within this section.");
        if (position.Beat > meter.Numerator)
            throw new ArgumentOutOfRangeException(nameof(position), $"Beat must be between 1 and {meter.Numerator} for the current meter.");
        var ticksPerBeat = checked(Timeline.TicksPerQuarterNote * 4 / meter.Denominator);
        if (position.Tick >= ticksPerBeat)
            throw new ArgumentOutOfRangeException(nameof(position), $"Tick must be between 0 and {ticksPerBeat - 1} for the current meter.");
    }

    public void Touch() => LastModifiedUtc = DateTimeOffset.UtcNow;
}
