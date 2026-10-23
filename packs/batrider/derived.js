// Values computed from map.yaml each tick (docs/memory-maps.md#derived-values). ROM addresses are the Japan B set's;
// see NOTES.md for how each rule was found.

const ATTRACT_DEMO = 0x80; // stage.counter bit 7: the attract mode's demo play

// pN.status bits 1-3 read 0xE whenever the ship was flying (with or without a bomb going off: 0x8E, 0x9E, 0xBE, 0xAE)
// and cleared the moment it was hit (0x80, 0x81) until the next pilot came in.
const FLYING = 0x0e;

const PILOT_DOWN = 0x01; // pN.team bit 0 (death at 0x2FA6E, extend at 0x3014E)
const MAPPED_PILOTS = 3; // pN.team.0-2; every team seen so far has three

const playerIn = (v, n) => v['game.running'] && v[`p${n}.active`];

const alive = (v, n) => (playerIn(v, n) ? (v[`p${n}.status`] & FLYING) === FLYING : null);

// The team list only marks a pilot down when the next one comes in, so the one exploding is taken off here.
const lives = (v, n) => {
  if (!playerIn(v, n)) return null;
  const pilots = Math.min(v['team.last_index'] + 1, MAPPED_PILOTS);
  let up = 0;
  for (let i = 0; i < pilots; i++) if ((v[`p${n}.team.${i}`] & PILOT_DOWN) === 0) up++;
  return alive(v, n) ? up : up - 1;
};

export default {
  // Declared first: everything below is null outside play, when memory holds the last game's leftovers.
  'game.running': {
    description:
      'Standard: a game is being played. False in the attract demo, on the title and character select, on the continue screen and after game over.',
    value: v => {
      const stage = v['stage.counter'];
      return stage > 0 && (stage & ATTRACT_DEMO) === 0 && (v['p1.active'] || v['p2.active']);
    },
  },
  stage: {
    description: 'Standard: the stage number, from 1.',
    value: v => (v['game.running'] ? v['stage.counter'] : null),
  },

  'p1.alive': { description: "Standard: player 1's ship is flying, not exploding.", value: v => alive(v, 1) },
  'p2.alive': { description: "Standard: player 2's ship is flying, not exploding.", value: v => alive(v, 2) },
  'p1.lives': {
    description: "Standard: player 1's pilots not down, including the one flying.",
    value: v => lives(v, 1),
  },
  'p2.lives': {
    description: "Standard: player 2's pilots not down, including the one flying.",
    value: v => lives(v, 2),
  },
};
