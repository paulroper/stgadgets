// Values computed from map.yaml each tick (docs/memory-maps.md#derived-values). ROM addresses are the World set's;
// see NOTES.md for how each rule was found.

// game.state values while a game is being played: both players, P2 only, P1 only. 7 also covers the stage results
// and the ending, which don't hold play still the way a "get ready" screen does.
const PLAYING = [0, 6, 7];
const CONTINUE_SCREEN = 8;

const IN_PLAY = 0x80;     // pN.flags bit 7: the ship is in play
const EXPLODING = 0x01;   // pN.flags bit 0: hit, and exploding until it respawns
const SOLO = 0x40;        // game.flags bit 6: required for the second loop (0x400CA)

const FIRST_CREDIT = 0x100;   // pN.continues before any continue

// Rank (routine at 0x3362). The difficulty's table, indexed by stage.index; settings.dips bits 0-1 pick it (pointers
// at 0x5092). These are the World set's: epozzobon's ddonpachj notes give other easy and very hard tables.
const RANK_BY_STAGE = [
  [16, 20, 24, 28, 32, 36, 40, 44],    // normal (checked in play)
  [0, 0, 0, 0, 0, 0, 0, 0],            // easy
  [32, 38, 44, 50, 56, 62, 68, 74],    // hard
  [63, 63, 63, 63, 63, 63, 63, 0],     // very hard: rank is always at its cap
];
const RANK_MAX = 63;
const SURVIVAL_FRAMES_PER_RANK = 2048;

// Bees (pickup at 0x4D2D2). Each stage has 13; the 13th makes the stage perfect. A bee is worth BEE_POINTS[bees
// already collected this stage + perfect stages so far], capped at the last entry (table at 0x4D3A4).
const BEES_PER_STAGE = 13;
const BEE_POINTS = [
  100, 200, 400, 800, 1000, 2000, 4000, 8000, 10000, 20000, 40000, 80000, 100000,
  200000, 300000, 400000, 500000, 600000, 700000, 800000, 900000, 1000000,
];
const STAGES_PER_LOOP = 7;   // loop 1 has 6; loop 2 adds stage 7

// The second loop (check at 0x400B4): solo, one credit, and any of these.
const LOOP2_MAX_DEATHS = 2;
const LOOP2_MAX_HITS = [270, 300, 330];   // by ship: A, B, C (table at 0x40038)
const LOOP2_SCORE = 50000000;
const LOOP2_PERFECT_STAGES = 4;

const POPUP_TICKS = 60;

// The board's refresh rate (MAME cave.cpp: 15625 / 271.5). The RetroArch provider reads once per emulated frame,
// so a tick is one of these.
const FRAMES_PER_SECOND = 15625 / 271.5;

// Slowdown: the game's frame_counter goes up once per pass of its main loop, which waits for vblank, so it skips a
// frame whenever a pass runs long. SPEED_SMOOTHING sets how quickly game.speed follows (about half a second).
const SPEED_SMOOTHING = 1 / 30;
const PAUSED_AFTER_TICKS = 30;   // longer than slowdown ever holds a frame: the game (or the emulator) is paused

// Stage clear bonus (results code at 0xF24 and 0x10CA): 1,000 / 500 points a star, and the no-miss bonus by stage
// (tables at 0x14C8, in score digits): loop 1 stages 1-6, then loop 2 stages 1-7.
const STAR_POINTS = { large: 1000, small: 500 };
const NO_MISS_BONUS = [200000, 300000, 400000, 500000, 600000, 700000, 2000000, 3000000, 4000000, 5000000, 6000000, 7000000, 8000000];

// Boss positions in stage.progress, stages 1-6 (RetroAchievements code notes; not yet checked).
const BOSS_PROGRESS = [0x200, 0x160, 0x1B0, 0x200, 0x250, 0x121];
// The scroll's cruising speed: after a burst of 1 step a frame at the start, the stage settles to 1 step every 16 frames.
const FRAMES_PER_SCROLL_STEP = 16;
const SCROLL_STOPPED_AFTER_TICKS = 32; // scroll steps come every 16-17 ticks at most while the stage moves

// Splits: one per stage played, 1-1 to 1-6 then 2-1 to 2-7.
const SPLITS = [1, 2, 3, 4, 5, 6].map(n => `1-${n}`).concat([1, 2, 3, 4, 5, 6, 7].map(n => `2-${n}`));

const NO_MORE_EXTENDS = 0xFFFF;   // pN.next_extend once every extend is given (0x44CF8)

// A whole number holding BCD, e.g. 0x00600000 -> 600000. Its hex digits are the decimal digits.
const bcdDigits = raw => Number.parseInt(raw.toString(16), 10);

// A player who isn't in the game has no lives; one on the continue screen has 0 too, but play is held then.
const inGame = (v, p) => v['game.running'] && v[`${p}.stock`] > 0;

// The score has 9 digits: memory holds the first 8, and the last is the continues used.
const score = (digits, lastDigit) => digits === null || lastDigit === null ? null : digits * 10 + lastDigit;

// A hit tops up the chain gauge, so the gauge rising means a hit this tick.
const hitNow = (v, prev, p) => prev !== null && v[`${p}.hit_timer`] > prev[`${p}.hit_timer`];

// When frame_counter moves after standing still: the ticks it stood still for, if few enough to be slowdown rather
// than a pause.
const slowdownFrames = prev => {
  const still = prev?.['game.still_ticks'] ?? 0;
  return still <= PAUSED_AFTER_TICKS ? still : 0;
};

// "1-3" for loop 1 stage 3; null outside the stages (loop 1's ending, loop 2's credits).
function stageKey(v) {
  const stage = v['stage.index'];
  const stages = v.loop === 0 ? 6 : v.loop === 1 ? 7 : 0;
  return stage < stages ? `${v.loop + 1}-${stage + 1}` : null;
}

// The per-player part of rank: counted while the ship is in play and not exploding.
function rankFromPlayer(v, p) {
  const flags = v[`${p}.flags`];
  if (!(flags & IN_PLAY) || flags & EXPLODING) return null;
  let part = v[`${p}.ship`] + 2 - 2 * v[`${p}.style`] + 2 * v[`${p}.shot_power`] + v[`${p}.laser_power`];
  if (v[`${p}.continues`] === FIRST_CREDIT) {
    part += v['stage.index'];
    if (v[`${p}.deaths_stage`] === 0) part += v['stage.index'];
  }
  return part;
}

// Rank's parts, in the order the reasons for a change are listed.
const RANK_PARTS = [
  ['rank.from_ships', 'ship & power'],
  ['rank.from_lives', 'lives'],
  ['rank.from_bees', 'bees'],
  ['rank.from_time', 'survival'],
  ['rank.from_stage', 'stage'],
  ['rank.from_loop', 'loop'],
];

const players = Object.fromEntries(['p1', 'p2'].flatMap(p => [
  [`${p}.score`, {
    description: `Standard: player ${p[1]}'s score as shown, including the last digit (continues used).`,
    value: v => v['game.running'] ? score(v[`${p}.score_digits`], v[`${p}.continues`] - FIRST_CREDIT) : null,
  }],
  [`${p}.alive`, {
    description: `Standard: player ${p[1]}'s ship is in play and not exploding.`,
    value: v => inGame(v, p) ? (v[`${p}.flags`] & IN_PLAY) !== 0 : null,
  }],
  [`${p}.lives`, {
    description: `Standard: player ${p[1]}'s ships including the one in play; an exploding ship no longer counts.`,
    // pN.stock drops at respawn, so take the exploding ship off it until then.
    value: v => inGame(v, p) ? v[`${p}.stock`] - (v[`${p}.flags`] & EXPLODING) : null,
  }],
  [`${p}.next_life_at`, {
    description: `The score at which player ${p[1]} gets the next extra life; null when no more are due, or outside play.`,
    value: v => {
      const raw = v[`${p}.next_extend`];
      return inGame(v, p) && raw !== NO_MORE_EXTENDS ? bcdDigits(raw) * 10 : null;
    },
  }],
  [`${p}.more_lives`, {
    description: `Player ${p[1]} has an extra life still to come from score.`,
    value: v => inGame(v, p) ? v[`${p}.next_extend`] !== NO_MORE_EXTENDS : null,
  }],
  [`${p}.bombs_held`, {
    description: `Player ${p[1]}'s bombs in stock, while in play (pN.bombs keeps leftovers outside play).`,
    value: v => inGame(v, p) ? v[`${p}.bombs`] : null,
  }],
  [`${p}.bomb_capacity`, {
    description: `Player ${p[1]}'s bomb slots, while in play.`,
    value: v => inGame(v, p) ? v[`${p}.bomb_slots`] : null,
  }],

  // Power: shot and laser levels, 0-4. Power-ups raise both; a death or continue can split them.
  [`${p}.shot_level`, {
    description: `Player ${p[1]}'s shot power, 0-4 (4 is max).`,
    value: v => inGame(v, p) ? v[`${p}.shot_power`] : null,
  }],
  [`${p}.laser_level`, {
    description: `Player ${p[1]}'s laser power, 0-4 (4 is max).`,
    value: v => inGame(v, p) ? v[`${p}.laser_power`] : null,
  }],

  // Chain
  [`${p}.chain`, {
    description: `Hits in player ${p[1]}'s chain while it's alive (the gauge hasn't run out), else 0.`,
    value: v => v['game.running'] ? (v[`${p}.hit_timer`] > 0 ? v[`${p}.hits`] : 0) : null,
  }],
  [`${p}.chain_gauge`, {
    description: `Player ${p[1]}'s chain gauge from 0 (chain over) to 1 (just hit).`,
    value: v => v['game.running'] && v['chain.timer_reload'] > 1
      ? Math.min(v[`${p}.hit_timer`] / (v['chain.timer_reload'] - 1), 1)
      : null,
  }],

  // Points per hit. A kill in a chain scores its base value times the chain's length. The score only updates
  // every other frame, so a hit's points can land on the tick after it.
  [`${p}.hit_now`, {
    description: `Player ${p[1]} hit something this tick (the chain gauge rose).`,
    value: (v, prev) => v['game.running'] ? hitNow(v, prev, p) : null,
  }],
  [`${p}.hit_recent`, {
    description: `Player ${p[1]} hit something this tick or the last.`,
    value: (v, prev) => v['game.running'] ? v[`${p}.hit_now`] || prev?.[`${p}.hit_now`] === true : null,
  }],
  [`${p}.hit_gain`, {
    description: `Points from player ${p[1]}'s latest hit, kept until the next; null until the first hit this game. Excludes points that came without a hit, such as stars, laser scratching and the MAXIMUM bonus.`,
    value: (v, prev) => {
      if (!v['game.running'] || prev === null || prev[`${p}.score`] === null) return null;
      const gained = v[`${p}.score`] - prev[`${p}.score`];
      return gained > 0 && v[`${p}.hit_recent`] ? gained : prev[`${p}.hit_gain`];
    },
  }],
  [`${p}.hit_gain_age`, {
    description: `Ticks since ${p}.hit_gain changed.`,
    value: (v, prev) => {
      if (v[`${p}.hit_gain`] === null) return null;
      const changed = prev === null || prev[`${p}.hit_gain`] === null || (v[`${p}.score`] > prev[`${p}.score`] && v[`${p}.hit_recent`]);
      return changed ? 0 : prev[`${p}.hit_gain_age`] + 1;
    },
  }],
  [`${p}.score_popup`, {
    description: `${p}.hit_gain for about a second after the hit, else null.`,
    value: v => v[`${p}.hit_gain_age`] !== null && v[`${p}.hit_gain_age`] < POPUP_TICKS ? v[`${p}.hit_gain`] : null,
  }],
]));

// Per-stage splits for player 1: what to count each tick while the stage is being played.
const SPLIT_FIELDS = {
  // Counted when frame_counter moves, so ticks spent paused don't count.
  frames: { description: 'frames played', count: (v, prev) => v['game.frame_advance'] === 0 ? 0 : 1 + slowdownFrames(prev) },
  game_frames: { description: 'frames the game itself advanced (fewer than frames when slowed down)', count: v => Math.min(v['game.frame_advance'], 1) },
  points: { description: 'points scored', count: (v, prev) => prev['p1.score'] === null || v['p1.score'] === null ? 0 : Math.max(v['p1.score'] - prev['p1.score'], 0) },
  deaths: { description: 'deaths', count: v => v['p1.died'] ? 1 : 0 },
  bombs: { description: 'bombs used', count: v => v['p1.bombed'] ? 1 : 0 },
};

// The current stage's split, one value per field. Each restarts at 0 when a new stage starts.
const currentSplit = Object.fromEntries(Object.entries(SPLIT_FIELDS).map(([field, { description, count }]) => [
  `split.${field}`,
  {
    description: `This stage's ${description}; kept through the continue screen. null outside play.`,
    value: (v, prev) => {
      if (!v['game.in_progress']) return null;
      const before = prev?.[`split.${field}`] ?? null;
      if (v['split.stage'] === null || v['split.stage'] !== prev?.['split.stage']) return v['split.stage'] === null ? before : 0;
      return before === null ? 0 : before + count(v, prev);
    },
  },
]));

// Bees collected in each stage of the current loop, as the game counts them (deaths reset the count).
const beeStages = Object.fromEntries(Array.from({ length: STAGES_PER_LOOP }, (_, i) => [`bees.stage${i + 1}`, {
  description: `Bees counted in stage ${i + 1} of this loop: live while it's being played, then kept. null for stages not reached (or played before stgadgets joined).`,
  value: (v, prev) => {
    if (!v['game.in_progress']) return null;
    const current = v['stage.index'];
    if (current === i) return v['bees.stage'];
    if (prev === null || prev.loop !== v.loop) return null;
    return current > i ? prev[`bees.stage${i + 1}`] : null;
  },
}]));

export default {
  // Declared first: everything below is null outside play, when memory holds leftovers (the last game's score
  // stays in memory through the title and ship select screens).
  'game.running': {
    description: 'Standard: a game is being played (game.state 0, 6 or 7). False on the title, ship select and continue screens, and in the demo.',
    value: v => PLAYING.includes(v['game.state']),
  },
  'game.in_progress': {
    description: 'game.running, or on the continue screen: values that last a whole credit (bee history) are kept through it.',
    value: v => v['game.running'] || v['game.state'] === CONTINUE_SCREEN,
  },

  hiscore: {
    description: 'Standard: the high score as shown, including its last digit.',
    value: v => score(v.hiscore_digits, v['hiscore.last_digit']),
  },

  stage: {
    description: 'Standard: the stage, from 1. Loop 2 starts again at 1 (see loop).',
    value: v => v['game.running'] ? v['stage.index'] + 1 : null,
  },

  ...players,

  // --- Slowdown --------------------------------------------------------------------------------------------------

  'game.frame_advance': {
    description: 'How far frame_counter moved since the last tick: 1 at full speed, 0 on a frame lost to slowdown.',
    value: (v, prev) => prev === null ? 0 : (v.frame_counter - prev.frame_counter + 0x10000) % 0x10000,
  },
  'game.still_ticks': {
    description: 'Ticks since frame_counter last moved.',
    value: (v, prev) => v['game.frame_advance'] > 0 || prev === null ? 0 : prev['game.still_ticks'] + 1,
  },
  'game.paused': {
    description: 'frame_counter has stood still for longer than slowdown ever holds it: paused, in the game or the emulator.',
    value: v => v['game.still_ticks'] > PAUSED_AFTER_TICKS,
  },
  'game.speed': {
    description: 'How fast the game is running, from 0 to 1: the share of frames its main loop finished in time, over about the last half second. Held while frame_counter stands still, then updated when it moves (so up to a frame or two late), and pauses are left out.',
    value: (v, prev) => {
      if (!v['game.running']) return null;
      let speed = prev?.['game.speed'] ?? 1;
      if (v['game.frame_advance'] === 0) return speed;   // decided when it moves again: slowdown or a pause?
      const lost = slowdownFrames(prev);
      speed *= (1 - SPEED_SMOOTHING) ** lost;
      return speed + SPEED_SMOOTHING * (1 - speed);
    },
  },

  // --- Stage clear bonus -------------------------------------------------------------------------------------------

  'p1.no_miss_bonus': {
    description: "The no-miss bonus clearing this stage would pay player 1: 0 after a death this stage.",
    value: v => {
      const key = v['game.running'] ? stageKey(v) : null;
      if (key === null || !inGame(v, 'p1')) return null;
      return v['p1.deaths_stage'] === 0 ? NO_MISS_BONUS[SPLITS.indexOf(key)] : 0;
    },
  },
  'p1.star_bonus': {
    description: "Player 1's stars this stage, as points at the stage clear. Deaths reset the count.",
    value: v => inGame(v, 'p1') ? v['p1.stars_large'] * STAR_POINTS.large + v['p1.stars_small'] * STAR_POINTS.small : null,
  },
  'p1.stage_clear_bonus': {
    description: 'Points clearing this stage would add for player 1 right now: no-miss bonus plus stars.',
    value: v => v['p1.no_miss_bonus'] === null || v['p1.star_bonus'] === null ? null : v['p1.no_miss_bonus'] + v['p1.star_bonus'],
  },

  // --- Distance to the boss ----------------------------------------------------------------------------------------

  'boss.at': {
    description: 'stage.progress where this stage\'s boss appears; null where it isn\'t known (stage 7).',
    value: v => v['game.running'] ? BOSS_PROGRESS[v['stage.index']] ?? null : null,
  },
  'boss.progress': {
    description: 'How far through the stage towards the boss, 0 to 1 (1 once the boss is reached).',
    value: v => v['boss.at'] === null ? null : Math.min(Math.max(v['stage.progress'], 0) / v['boss.at'], 1),
  },
  'stage.ticks_since_scroll': {
    description: 'Ticks since stage.progress last moved.',
    value: (v, prev) => {
      if (!v['game.running']) return null;
      return prev === null || prev['stage.ticks_since_scroll'] === null || v['stage.progress'] !== prev['stage.progress'] ? 0 : prev['stage.ticks_since_scroll'] + 1;
    },
  },
  'stage.scrolling': {
    description: 'The stage is scrolling. It stops for bosses, and for a few seconds at times mid-stage; rank is only recalculated while it moves.',
    value: v => v['stage.ticks_since_scroll'] === null ? null : v['stage.ticks_since_scroll'] <= SCROLL_STOPPED_AFTER_TICKS,
  },
  'boss.eta': {
    description: 'Seconds until the boss at the cruising scroll speed; 0 once it\'s reached. Holds still while the scroll stops, and leaves out stops still to come.',
    value: v => {
      if (v['boss.at'] === null) return null;
      const remaining = Math.max(v['boss.at'] - v['stage.progress'], 0);
      return remaining * FRAMES_PER_SCROLL_STEP / FRAMES_PER_SECOND;
    },
  },

  // --- Splits ------------------------------------------------------------------------------------------------------

  'split.stage': {
    description: "The split being timed: '1-1' to '1-6', then '2-1' to '2-7'. null outside the stages.",
    value: v => v['game.running'] ? stageKey(v) : null,
  },
  'p1.died': {
    description: 'Player 1 was hit this tick.',
    value: (v, prev) => prev !== null && inGame(v, 'p1') && (v['p1.flags'] & EXPLODING) !== 0 && (prev['p1.flags'] & EXPLODING) === 0,
  },
  'p1.bombed': {
    description: 'Player 1 used a bomb this tick: one fewer in stock while the ship is in play.',
    value: (v, prev) => prev !== null && inGame(v, 'p1') && (v['p1.flags'] & IN_PLAY) !== 0 && v['p1.bombs'] === prev['p1.bombs'] - 1,
  },
  ...currentSplit,
  'split.done': {
    description: "Finished stages this credit, as JSON: [{ stage: '1-1', frames, game_frames, points, deaths, bombs }, ...]. A stage is added when the next one starts.",
    value: (v, prev) => {
      if (!v['game.in_progress']) return '[]';
      const done = prev?.['split.done'] ?? '[]';
      const finished = prev?.['split.stage'];
      if (!finished || v['split.stage'] === finished || v['split.stage'] === null) return done;
      const split = { stage: finished };
      for (const field of Object.keys(SPLIT_FIELDS)) split[field] = prev[`split.${field}`];
      return JSON.stringify([...JSON.parse(done), split]);
    },
  },

  // --- Rank: the formula at 0x3362, split into its parts ----------------------------------------------------------
  // The game only recalculates rank as the stage scrolls, so `rank` (memory) catches up with rank.model up to about
  // a second later. Gadgets should show `rank` and use the parts to explain it.

  'rank.from_ships': {
    description: "Rank from the players' ships: ship (A 0, B 1, C 2) + 2 for shot style + 2 x shot power + laser power, plus the stage on the first credit (twice with no deaths this stage); halved with two players. An exploding ship adds nothing.",
    value: v => {
      if (!v['game.running']) return null;
      const parts = ['p1', 'p2'].map(p => rankFromPlayer(v, p)).filter(x => x !== null);
      const sum = parts.reduce((a, b) => a + b, 0);
      return parts.length === 2 ? Math.floor(sum / 2) : sum;
    },
  },
  'rank.from_lives': {
    description: 'Rank from lives in stock: 2 per life, both players.',
    value: v => v['game.running'] ? 2 * (v['p1.stock'] + v['p2.stock']) : null,
  },
  'rank.from_bees': {
    description: 'Rank from bees collected this stage: 1 each.',
    value: v => v['game.running'] ? v['bees.stage'] : null,
  },
  'rank.from_time': {
    description: 'Rank from time since the last death: 1 per 2048 frames (about 34 s).',
    value: v => v['game.running'] ? Math.floor(v['rank.survival_frames'] / SURVIVAL_FRAMES_PER_RANK) : null,
  },
  'rank.time_point_in': {
    description: 'Seconds until survival time adds the next point of rank, while the ship is in play; null otherwise (the clock is reset when hit, and restarts after respawning).',
    value: v => {
      if (!v['game.running'] || !v['p1.alive']) return null;
      const frames = SURVIVAL_FRAMES_PER_RANK - (v['rank.survival_frames'] % SURVIVAL_FRAMES_PER_RANK);
      return frames / FRAMES_PER_SECOND;
    },
  },
  'rank.from_stage': {
    description: "Rank from the stage, by difficulty: 16 on stage 1 at normal, +4 a stage; 0 on easy; 32 +6 a stage on hard; 63 on very hard. Only normal is checked in play.",
    value: v => v['game.running'] ? RANK_BY_STAGE[v['settings.dips'] & 3][v['stage.index']] ?? null : null,
  },
  'rank.from_loop': {
    description: 'Rank from the loop: +32 on the second.',
    value: v => v['game.running'] ? 32 * v.loop : null,
  },
  'rank.model': {
    description: 'Rank as the formula gives it right now, 0-63: what rank becomes at its next recalculation.',
    value: v => {
      const parts = RANK_PARTS.map(([name]) => v[name]);
      return parts.includes(null) ? null : Math.min(parts.reduce((a, b) => a + b, 0), RANK_MAX);
    },
  },
  'rank.heading_to': {
    description: "What rank becomes at its next recalculation, while that's coming (the stage is scrolling) and differs from rank; else null. During a boss, rank stays put.",
    value: v => v['rank.model'] !== null && v['stage.scrolling'] && v['rank.model'] !== v.rank ? v['rank.model'] : null,
  },
  'rank.change': {
    description: 'How much the latest change moved rank.model (+ or -); kept until the next. null until the first change.',
    value: (v, prev) => {
      if (v['rank.model'] === null || prev === null || prev['rank.model'] === null) return null;
      return v['rank.model'] !== prev['rank.model'] ? v['rank.model'] - prev['rank.model'] : prev['rank.change'];
    },
  },
  'rank.change_reason': {
    description: "What moved rank.model in its latest change, e.g. 'ship & power +3' or 'bees -1, survival -1, ship & power -3'.",
    value: (v, prev) => {
      if (v['rank.model'] === null || prev === null || prev['rank.model'] === null) return null;
      if (v['rank.model'] === prev['rank.model']) return prev['rank.change_reason'];
      return RANK_PARTS
        .map(([name, label]) => [label, v[name] - prev[name]])
        .filter(([, delta]) => delta !== 0)
        .map(([label, delta]) => `${label} ${delta > 0 ? '+' : ''}${delta}`)
        .join(', ');
    },
  },
  'rank.change_age': {
    description: 'Ticks since rank.change.',
    value: (v, prev) => {
      if (v['rank.change'] === null) return null;
      return prev === null || prev['rank.change_age'] === null || v['rank.model'] !== prev['rank.model'] ? 0 : prev['rank.change_age'] + 1;
    },
  },

  // --- Bees ---------------------------------------------------------------------------------------------------------

  'bees.collected': {
    description: 'Bees counted this stage, 0-13. A death resets the count.',
    value: v => v['game.running'] ? v['bees.stage'] : null,
  },
  'bees.total': {
    description: 'Bees in each stage.',
    value: v => v['game.running'] ? BEES_PER_STAGE : null,
  },
  'bees.perfect': {
    description: 'Stages completed with every bee this game (the count the second loop checks).',
    value: v => v['game.in_progress'] ? v['bees.perfect_stages'] : null,
  },
  'bees.next_value': {
    description: 'Points for the next bee; null once the stage is perfect.',
    value: v => {
      if (!v['game.running'] || v['bees.stage'] >= BEES_PER_STAGE) return null;
      return BEE_POINTS[Math.min(v['bees.stage'] + v['bees.perfect_stages'], BEE_POINTS.length - 1)];
    },
  },
  'bees.perfect_possible': {
    description: "False once a death has reset this stage's bee count (bees already taken don't come back). A bee scrolled past also rules it out, which memory doesn't show.",
    value: (v, prev) => {
      if (!v['game.running']) return null;
      if (v['bees.stage'] >= BEES_PER_STAGE) return true;
      const newStage = prev === null || prev['bees.perfect_possible'] === null || prev['stage.index'] !== v['stage.index'] || prev.loop !== v.loop;
      return newStage || (prev['bees.perfect_possible'] && v['bees.stage'] >= prev['bees.stage']);
    },
  },
  ...beeStages,

  // --- Second loop ----------------------------------------------------------------------------------------------------

  'loop2.eligible': {
    description: "Still on the first loop, solo, on the first credit. Without these, no condition below helps. P1 only: P2's side isn't checked yet.",
    value: v => v['game.in_progress'] && v.loop === 0
      ? (v['game.flags'] & SOLO) !== 0 && v['p1.continues'] === FIRST_CREDIT
      : null,
  },
  'loop2.hits_needed': {
    description: 'The max chain that unlocks the second loop for P1\'s ship: 270 (A), 300 (B), 330 (C).',
    value: v => v['loop2.eligible'] === null ? null : LOOP2_MAX_HITS[v['p1.ship']] ?? null,
  },
  'loop2.by_deaths': {
    description: 'Lost two ships or fewer this game: true until the third death.',
    value: v => v['loop2.eligible'] === null ? null : v['p1.deaths_first_credit'] <= LOOP2_MAX_DEATHS,
  },
  'loop2.by_hits': {
    description: 'The max chain this game has reached loop2.hits_needed.',
    value: v => v['loop2.eligible'] === null ? null : v['p1.max_hits'] >= v['loop2.hits_needed'],
  },
  'loop2.by_score': {
    description: 'Score is at least 50,000,000.',
    value: v => v['loop2.eligible'] === null ? null : v['p1.score_digits'] * 10 >= LOOP2_SCORE,
  },
  'loop2.by_bees': {
    description: 'At least 4 stages completed with every bee.',
    value: v => v['loop2.eligible'] === null ? null : v['bees.perfect_stages'] >= LOOP2_PERFECT_STAGES,
  },
  'loop2.possible': {
    description: 'Clearing stage 6 now would lead to the second loop: eligible, and at least one condition met. Checked against the code, not yet in play.',
    value: v => v['loop2.eligible'] === null ? null
      : v['loop2.eligible'] && (v['loop2.by_deaths'] || v['loop2.by_hits'] || v['loop2.by_score'] || v['loop2.by_bees']),
  },
};
