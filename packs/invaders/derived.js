// Values computed from map.yaml each tick (docs/memory-maps.md#derived-values).

// The march tempo, from the Computer Archeology disassembly: the first entry in ALIENS (0x1A11) that is at most
// aliens_remaining picks the delay at the same index in DELAYS (0x1A21), in frames between march notes.
const ALIENS = [50, 43, 36, 28, 22, 17, 13, 10, 8, 7, 6, 5, 4, 3, 2, 1];
const DELAYS = [52, 46, 39, 34, 28, 24, 21, 19, 16, 14, 13, 12, 11, 9, 7, 5];

const tempoIndex = aliens => aliens > 0 ? ALIENS.findIndex(threshold => threshold <= aliens) : -1;

export default {
  'fleet.tempo': {
    description: 'Frames between march notes for the aliens left. fleet.sound_delay catches up on the next note.',
    value: v => {
      const i = tempoIndex(v.aliens_remaining);
      return i < 0 ? null : DELAYS[i];
    },
  },

  'fleet.speedup_in': {
    description: 'Kills until the march speeds up again; null with one alien left (or none).',
    value: v => {
      const i = tempoIndex(v.aliens_remaining);
      return i < 0 || i === ALIENS.length - 1 ? null : v.aliens_remaining - ALIENS[i] + 1;
    },
  },
};
