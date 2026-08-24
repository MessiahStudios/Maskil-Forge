using MaskilForge.Domain;

namespace MaskilForge.Engine;

public sealed record InstrumentRoleRecommendation(
    ArrangementRole Role,
    IReadOnlyList<InstrumentProfile> Instruments);

/// <summary>
/// A transient, inspectable matching of catalog instruments to arrangement jobs.
/// It does not assign an instrument, rank a winner, or change the Song Graph.
/// </summary>
public sealed record InstrumentRecommendationSet(
    InstrumentExpressiveQuality? Quality,
    IReadOnlyList<InstrumentRoleRecommendation> Recommendations);

public static class InstrumentRoleRecommender
{
    public static InstrumentRecommendationSet Recommend(
        IReadOnlyList<ArrangementRole> roles,
        InstrumentExpressiveQuality? quality = null,
        InstrumentProfileCatalog? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(roles);
        if (roles.Count == 0) throw new ArgumentException("Choose at least one arrangement role.", nameof(roles));
        if (roles.Any(role => !Enum.IsDefined(role)))
            throw new ArgumentOutOfRangeException(nameof(roles), "Arrangement role is invalid.");
        if (quality is not null && !Enum.IsDefined(quality.Value))
            throw new ArgumentOutOfRangeException(nameof(quality), "Expressive quality is invalid.");

        catalog ??= InstrumentProfileCatalogLoader.Current;
        var orderedRoles = roles.Distinct().ToList();
        var recommendations = orderedRoles.Select(role => new InstrumentRoleRecommendation(
            role,
            catalog.Instruments
                .Where(instrument => instrument.Roles.Contains(role))
                .Where(instrument => quality is null || instrument.ExpressiveQualities.Contains(quality.Value))
                .ToList())).ToList();

        return new InstrumentRecommendationSet(quality, recommendations);
    }
}
