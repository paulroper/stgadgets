// Values computed from map.yaml each tick (docs/memory-maps.md#derived-values).

// The march tempo, from the Computer Archeology disassembly: the first entry in ALIENS (0x1A11) that is at most
// aliens_remaining picks the delay at the same index in DELAYS (0x1A21), in frames between march notes.
const ALIENS = [50, 43, 36, 28, 22, 17, 13, 10, 8, 7, 6, 5, 4, 3, 2, 1];
const DELAYS = [52, 46, 39, 34, 28, 24, 21, 19, 16, 14, 13, 12, 11, 9, 7, 5];

// The saucer's score table at 0x1D54, in points. saucer.score_ptr walks the first 15 entries (a bug skips the 16th),
// stepping each time a player shot is removed; a hit scores the entry it steps to.
const SAUCER_SCORES = [100, 50, 50, 100, 150, 100, 100, 50, 300, 100, 100, 100, 50, 150, 100];
const SAUCER_300 = SAUCER_SCORES.indexOf(300);
const RACK_START_Y = 0x78;

const saucerIndex = ptr => (ptr >= 0x54 && ptr < 0x54 + SAUCER_SCORES.length ? ptr - 0x54 : null);

// Changes between this tick and the last are player 1's doing only if both ticks are player 1 playing.
const p1Playing = (v, prev) =>
  prev !== null &&
  v['game.running'] &&
  prev['game.running'] &&
  v['player.data'] === 0x21 &&
  prev['player.data'] === 0x21;

const POPUP_TICKS = 60;

const tempoIndex = aliens => (aliens > 0 && aliens <= 55 ? ALIENS.findIndex(threshold => threshold <= aliens) : -1);

export default {
  // Declared first: everything below is null outside play, when memory holds leftovers (aliens_remaining is 255 on
  // the start screen) rather than the game's state.
  'game.running': {
    description:
      'Standard: a game is being played. False in attract mode and while the start and game-over screens hold play still.',
    value: v => v['game.mode'] && v['game.moving'],
  },

  // Standard player names. 0x2015 is the current player's ship, so these are null while player 2 is up
  // (two-player games aren't verified yet).
  'p1.alive': {
    description: "Standard: player 1's ship is in play and not exploding.",
    value: v => (v['game.running'] && v['player.data'] === 0x21 ? v['player.state'] === 0xff : null),
  },
  // Player 1's shots this game, from the global shot counter.
  'p1.shots': {
    description:
      'Shots player 1 has fired this game (counted as they leave the screen, hit or miss); null in attract mode.',
    value: (v, prev) => {
      if (!v['game.mode']) return null;
      if (prev === null || prev['p1.shots'] === null) return 0;
      return prev['p1.shots'] + (p1Playing(v, prev) ? (v['shots.count'] - prev['shots.count'] + 256) % 256 : 0);
    },
  },

  'p1.kills': {
    description:
      "Aliens player 1 has shot this game (the saucer isn't an alien). Counts from when stgadgets joined if a game was already under way; null in attract mode.",
    value: (v, prev) => {
      if (!v['game.mode']) return null;
      if (prev === null || prev['p1.kills'] === null) return 0;
      // The start screen's 255 -> 55 isn't between two ticks of play, so it doesn't count.
      return prev['p1.kills'] + (p1Playing(v, prev) ? Math.max(prev.aliens_remaining - v.aliens_remaining, 0) : 0);
    },
  },

  'p1.accuracy': {
    description:
      'Aliens hit per shot this game, from 0 to 1; null before the first shot. Saucer hits and shots that hit bombs count as misses.',
    value: v => (v['p1.shots'] ? v['p1.kills'] / v['p1.shots'] : null),
  },

  // The latest score increase, for the "+N" popup by the score.
  'p1.last_gain': {
    description: "Points from player 1's most recent score increase this game; null until the first.",
    value: (v, prev) => {
      if (!v['game.mode'] || prev === null) return null;
      return p1Playing(v, prev) && v['p1.score'] > prev['p1.score']
        ? v['p1.score'] - prev['p1.score']
        : prev['p1.last_gain'];
    },
  },
  'p1.last_gain_age': {
    description: 'Ticks (about frames) since p1.last_gain.',
    value: (v, prev) => {
      if (v['p1.last_gain'] === null) return null;
      const gained = p1Playing(v, prev) && v['p1.score'] > prev['p1.score'];
      return gained || prev['p1.last_gain_age'] === null ? 0 : prev['p1.last_gain_age'] + 1;
    },
  },
  'p1.score_popup': {
    description: 'p1.last_gain for about a second after it happens, else null.',
    value: v => (v['p1.last_gain_age'] !== null && v['p1.last_gain_age'] < POPUP_TICKS ? v['p1.last_gain'] : null),
  },
  'p1.lives': {
    description: "Standard: player 1's ships including the one in play; a ship that's exploding no longer counts.",
    value: v => (v['p1.alive'] === null ? null : v['p1.ships_reserve'] + (v['p1.alive'] ? 1 : 0)),
  },

  'fleet.aliens': {
    description: 'aliens_remaining, or null outside play (it reads 255 on the start screen).',
    value: v => (v['game.running'] ? v.aliens_remaining : null),
  },

  'fleet.tempo': {
    description: 'Frames between march notes for the aliens left. fleet.sound_delay catches up on the next note.',
    value: v => {
      const i = tempoIndex(v.aliens_remaining);
      return !v['game.running'] || i < 0 ? null : DELAYS[i];
    },
  },

  'fleet.speedup_in': {
    description: 'Kills until the march speeds up again; null with one alien left (or none), or outside play.',
    value: v => {
      const i = tempoIndex(v.aliens_remaining);
      return !v['game.running'] || i < 0 || i === ALIENS.length - 1 ? null : v.aliens_remaining - ALIENS[i] + 1;
    },
  },

  'saucer.status': {
    description:
      "null outside play, else 'flying', 'due' (launches when no squiggly shot is out), 'counting', 'paused' (rack still at its starting height) or 'none' (under 8 aliens: no more saucers this wave).",
    value: v => {
      if (!v['game.running']) return null;
      if (v['saucer.active']) return 'flying';
      if (v.aliens_remaining < 8) return 'none';
      if (v['saucer.due']) return 'due';
      return v['rack.y'] >= RACK_START_Y ? 'paused' : 'counting';
    },
  },

  'saucer.appears_in': {
    description: 'Frames until the saucer is due: 0 when due, null when flying or none.',
    value: v => {
      switch (v['saucer.status']) {
        case 'counting':
        case 'paused':
          return v['saucer.timer'];
        case 'due':
          return 0;
        default:
          return null;
      }
    },
  },

  'saucer.next_score': {
    description: 'Points for hitting the saucer with the next shot (the one in flight, if any).',
    value: v => {
      const i = v['game.running'] ? saucerIndex(v['saucer.score_ptr']) : null;
      return i === null ? null : SAUCER_SCORES[(i + 1) % SAUCER_SCORES.length];
    },
  },

  'saucer.shots_to_300': {
    description: 'Which shot from now (1 = the next, or the one in flight) scores 300 if it hits the saucer.',
    value: v => {
      const i = v['game.running'] ? saucerIndex(v['saucer.score_ptr']) : null;
      return i === null ? null : ((SAUCER_300 - i - 1 + SAUCER_SCORES.length) % SAUCER_SCORES.length) + 1;
    },
  },
};
