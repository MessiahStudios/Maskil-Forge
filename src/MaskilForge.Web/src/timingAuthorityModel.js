export function plannedSongTiming(sectionPlacements = []) {
  const placements = sectionPlacements.filter(item =>
    Number.isFinite(item?.start?.bar)
    && Number.isFinite(item?.durationBars)
    && item.start.bar >= 1
    && item.durationBars >= 1)

  if (placements.length === 0) {
    return {
      sectionCount: 0,
      plannedBars: 0,
      endBarExclusive: null,
      label: 'No planned song form yet.',
      structureNotice: 'Add a section to establish a current arrangement plan. Lyrics alone do not determine musical duration.',
      midiNotice: 'No planned sections are stored, so MIDI duration follows the latest stored event.',
    }
  }

  const firstBar = Math.min(...placements.map(item => item.start.bar))
  const endBarExclusive = Math.max(...placements.map(item => item.start.bar + item.durationBars))
  const plannedBars = endBarExclusive - firstBar
  const barLabel = plannedBars === 1 ? 'bar' : 'bars'

  return {
    sectionCount: placements.length,
    plannedBars,
    endBarExclusive,
    label: `${plannedBars} planned ${barLabel} · current form ends when bar ${endBarExclusive} begins`,
    structureNotice: 'Section lengths are editable arrangement planning. Lyrics and syllable starts do not decide sung duration; the final performance can be shorter or longer.',
    midiNotice: `MIDI carries the current plan through the start of bar ${endBarExclusive}. Later stored notes or controller events can extend it; this is not the final performed duration.`,
  }
}
