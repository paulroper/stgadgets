// Values computed from map.yaml each tick (docs/memory-maps.md#derived-values). ROM addresses are the Japan B set's;
// see NOTES.md for how each rule was found.

const ATTRACT_DEMO = 0x80; // stage.counter bit 7: the attract mode's demo play

// pN.status bits 1-3 read 0xE whenever the ship was flying (with or without a bomb going off: 0x8E, 0x9E, 0xBE, 0xAE)
// and cleared the moment it was hit (0x80, 0x81) until the next pilot came in.
const FLYING = 0x0e;

const PILOT_DOWN = 0x01; // pN.team bit 0 (death at 0x2FA6E, extend at 0x3014E)
const MAPPED_PILOTS = 3; // pN.team.0-2; every team seen so far has three
const NO_MORE_EXTENDS = 0xffffffff; // pN.next_extend once the last extend is given (0x7EA4)

// Medals go up in hundreds to 900, then in thousands to 10,000 (shmups wiki); medal.value is twice the index.
const MEDAL_POINTS = [
  100, 200, 300, 400, 500, 600, 700, 800, 900, 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000,
];

// Small shot items needed for each shot level's next level (table at 0x2FCDE); level 3 is the top.
const SHOT_ITEMS_NEEDED = [2, 3, 4];
const SHOT_LEVEL_MAX = 3;
const FRAGMENTS_PER_BOMB = 40; // 0x2FD3E

// Rank is a 24-bit count that goes down as the game gets harder. Everything below works in "harder" rank units: the
// amount taken off the rank, so positive is harder, and 0x1000000 is the whole scale. The tables are trap15's, at the code addresses in NOTES.md.
const RANK_SCALE = 0x1000000;
const DEATH_RANK = [-0x400000, -0x100000, -0x40000, -0x10000]; // by pilots left after the death (0x2FA9E)
const EXTEND_RANK = [0, 0x80000, 0x100000, 0x200000, 0x400000]; // by pilots up before the extend (0x301DA)
const SMALL_SHOT_RANK = 0x100;
const FRAGMENT_RANK = 0x800;
const LARGE_SHOT_RANK = 0x8000;
const OPTION_RANK = 0x8000;
const LARGE_BOMB_RANK = 0x20000;
const MEDAL_RANK = points => (points >= 10000 ? 0x200 : points >= 1000 ? 0x100 : 0);
const CHANGE_TICKS = 180; // how long a rank change stays "latest"
const GAIN_TICKS = 60; // how long a score gain stays on screen, and how long between points still adds to it

// Bosses by the routine that runs them (enemies[].routine), from the Batrider MAME layout by baf, Olifante and DUBS.
// HP is shown for the listed parts while their status matches: & 0xF001 is 0x8000, unless the part gives its own.
const BOSSES = {
  0x0531b4: { name: 'CONFRICT', parts: { 0: { mask: 0xf201, value: 0x8200 }, 118: {} } },
  0x054e38: { name: 'BAZZCOK', parts: { 0: {} } },
  0x0566e2: { name: 'BOREDOM', parts: { 0: {} } },
  0x0581c8: { name: 'DEVIATE', parts: { 0: {}, 4: {}, 6: {} } },
  0x060cc4: { name: 'GRUBBY', parts: { 0: {}, 2: {} } },
  0x063ab2: { name: 'DISCHARGE', parts: { 0: {}, 2: {} } },
  0x06563a: { name: 'DISCHARGE', parts: { 0: {} } },
  0x065bfc: { name: 'DISCHARGE', parts: { 2: {} } },
  0x065d5a: { name: 'DISCHARGE', parts: { 2: {} } },
  0x065e82: { name: 'DISCHARGE', parts: { 2: {} } },
  0x075ae2: { name: 'AIRPORT', parts: { 0: {} } },
  0x07b282: { name: 'Z.CITY', parts: { 0: {}, 2: {} } },
  0x07bb96: { name: 'Z.CITY', parts: { 0: {} } },
  0x07d424: { name: 'Z.CITY', parts: { 0: {} } },
  0x07a37c: { name: 'BUILDING', parts: { 18: {} } },
  0x079b0c: { name: 'BUILDING', parts: { 0: {} } },
  0x0799be: { name: 'BUILDING', parts: { 28: {} } },
  0x04de40: { name: 'BASHINET', parts: { 0: {}, 2: {} } },
  0x0516d0: { name: 'BASHINET MK2', parts: { 0: {} } },
  0x049960: { name: 'BASHINET-R', parts: { 2: {} } },
  0x04c7f4: { name: 'GOB-ROBO', parts: { 0: {} } },
  0x067caa: { name: 'BLACK HEART', parts: { 0: {} } },
  0x069456: { name: 'BLACK HEART MK2', parts: { 0: {} } },
  0x06ad02: { name: 'GLOW SQUID', parts: { 0: {} } },
  0x06ae04: { name: 'GLOW SQUID', parts: { 116: {} } },
  0x06d604: { name: 'TSUMUJIMARU', parts: { 2: {}, 10: {} } },
  0x06e6c2: { name: 'HAYATEMARU', parts: { 0: {}, 2: {}, 4: {}, 6: {}, 8: {}, 10: {} } },
  0x05a348: { name: 'ENVY', parts: { 0: {} } },
  0x05ed6a: { name: 'BLUNT', parts: { 0: {} } },
  0x05c73e: { name: 'SOBUT', parts: { 0: {} } },
};
const BOSS_PART_MASK = 0xf001;
const BOSS_PART_VALUE = 0x8000;

// Each scene's name, as the game shows it at the start of the stage (table at 0x11C0C, by stage.scene). 8-11 are the
// hidden bosses' stages.
const SCENE_NAMES = {
  1: 'M.CITY',
  2: 'AIRPORT',
  3: 'SKY HIGH',
  4: 'S.SYSTEM',
  5: 'HIGH-WAY',
  6: 'Z.CITY',
  7: 'BUILDING',
  8: 'BASHImk2',
  9: 'BASHI-R',
  10: 'B.H.mk2',
  11: 'G.SQUID',
};

// Each course's stages, from the master script's branch by course (0x21C20): scene ids, and 'order N' for the stage
// chosen in that slot of stage.order, whose value v is scene v + 1 (the table at 0x21EE8). The boss course runs a
// different script and has no route here.
const ROUTES = {
  0: [1, 'order 0', 5], // training (0x21C9A)
  2: [1, 'order 0', 'order 1', 'order 2', 5], // normal (0x21CB4)
  4: [1, 'order 0', 'order 1', 'order 2', 5, 6, 7], // advanced (0x21CEA)
};
const ROUTE_ROWS = 7;
// A hidden boss's stage comes after this many of the course's stages, when its condition is met (the conditional
// calls in the course scripts: BASHImk2 before the HIGH-WAY, B.H.mk2 after it, BASHI-R and G.SQUID after Z.CITY).
const HIDDEN_AFTER = { 8: 4, 10: 5, 9: 6, 11: 6 };

const routeScenes = v =>
  (ROUTES[v.course] ?? []).map(stage =>
    typeof stage === 'number' ? stage : 1 + (v[`stage.order.${2 + Number(stage.split(' ')[1])}`] & 3),
  );

// Where each scene's boss arrives, in stage.scroll: the position its stage script waits for before spawning it (NOTES.md,
// stage scripts). Only scenes whose boss has been seen arriving at that position are listed.
const BOSS_AT = { 1: 0x6f000 };
const FRAMES_PER_SECOND = 60;

const playerIn = (v, n) => v['game.running'] && v[`p${n}.active`];

const alive = (v, n) => (playerIn(v, n) ? (v[`p${n}.status`] & FLYING) === FLYING : null);

const pilotsUp = (v, n) => {
  const pilots = Math.min(v['team.last_index'] + 1, MAPPED_PILOTS);
  let up = 0;
  for (let i = 0; i < pilots; i++) if ((v[`p${n}.team.${i}`] & PILOT_DOWN) === 0) up++;
  return up;
};

// The team list only marks a pilot down when the next one comes in, so the one exploding is taken off here.
const lives = (v, n) => {
  if (!playerIn(v, n)) return null;
  return alive(v, n) ? pilotsUp(v, n) : pilotsUp(v, n) - 1;
};

// Both ticks are the same game in play, so changes between them happened in play.
const bothRunning = (v, prev) => prev !== null && v['game.running'] && prev['game.running'];

const bombStock = (v, n) => v[`p${n}.bombs`] * FRAGMENTS_PER_BOMB + v[`p${n}.bomb_fragments`];

/**
 * The rank changes this tick that the game's own tables account for, by what caused them: one player's death, extend
 * and pickups. A respawn loads the next pilot's power and bombs, so nothing else counts on that tick.
 */
function playerEvents(v, prev, n) {
  const events = [];
  if (!v[`p${n}.active`] || !prev[`p${n}.active`]) return events;

  if (v[`p${n}.ship`] !== prev[`p${n}.ship`]) {
    const left = Math.min(pilotsUp(v, n), DEATH_RANK.length - 1);
    events.push({ part: 'deaths', reason: 'death', amount: DEATH_RANK[left] });
    return events;
  }
  const up = pilotsUp(v, n);
  const upBefore = pilotsUp(prev, n);
  if (up > upBefore) events.push({ part: 'extends', reason: 'extend', amount: EXTEND_RANK[upBefore] });

  const level = v[`p${n}.shot_level`];
  const levelBefore = prev[`p${n}.shot_level`];
  const items = v[`p${n}.shot_items`];
  const itemsBefore = prev[`p${n}.shot_items`];
  if (level > levelBefore) {
    const fromSmall = items === 0 && itemsBefore === SHOT_ITEMS_NEEDED[levelBefore] - 1;
    events.push(
      fromSmall
        ? { part: 'items', reason: 'small shot', amount: SMALL_SHOT_RANK }
        : { part: 'items', reason: 'large shot', amount: LARGE_SHOT_RANK },
    );
  } else if (items > itemsBefore) {
    events.push({ part: 'items', reason: 'small shot', amount: (items - itemsBefore) * SMALL_SHOT_RANK });
  }

  if (v[`p${n}.option_level`] > prev[`p${n}.option_level`] || v[`p${n}.options`] > prev[`p${n}.options`])
    events.push({ part: 'items', reason: 'option', amount: OPTION_RANK });

  const bombs = v[`p${n}.bombs`];
  const bombsBefore = prev[`p${n}.bombs`];
  const fragmentsGained = bombStock(v, n) - bombStock(prev, n);
  if (bombs > bombsBefore && v[`p${n}.bomb_fragments`] === prev[`p${n}.bomb_fragments`])
    events.push({ part: 'items', reason: 'large bomb', amount: LARGE_BOMB_RANK });
  else if (fragmentsGained > 0)
    events.push({ part: 'items', reason: 'bomb fragment', amount: fragmentsGained * FRAGMENT_RANK });

  return events;
}

/** A medal taken this tick (medal.value moved up), by either player. */
function medalEvent(v, prev) {
  if (v['medal.value'] <= prev['medal.value']) return null;
  const points = MEDAL_POINTS[prev['medal.value'] / 2];
  return points === undefined ? null : { part: 'items', reason: `medal ${points}`, amount: MEDAL_RANK(points) };
}

/**
 * Everything that changed the rank this tick: time, the events above, and the rest (shots and grazing). The rank stops
 * at rank.min and at 0, so when the events would take it past either, they only moved it as far as it went: the
 * biggest takes the difference, and nothing is left for shots.
 */
function rankTick(v, prev) {
  const frames = v.frame_counter - prev.frame_counter;
  const harder = prev.rank - v.rank;
  // The game was paused (the frame counter runs in the vblank interrupt, but rank doesn't move).
  const drift = harder === 0 ? 0 : frames * (v['rank.per_frame'] + v.credits);
  const events = [1, 2].flatMap(n => playerEvents(v, prev, n));
  const medal = medalEvent(v, prev);
  if (medal) events.push(medal);

  const explained = events.reduce((sum, e) => sum + e.amount, 0);
  const unclamped = prev.rank - drift - explained;
  if (events.length > 0 && (unclamped > v['rank.min'] || unclamped < 0)) {
    const biggest = events.reduce((a, b) => (Math.abs(b.amount) > Math.abs(a.amount) ? b : a));
    const others = events.filter(e => e !== biggest).reduce((sum, e) => sum + e.amount, 0);
    biggest.amount = harder - drift - others;
    return { drift, events, shots: 0 };
  }
  return { drift, events, shots: harder - drift - explained };
}

const RANK_PARTS = ['time', 'shots', 'items', 'deaths', 'extends'];

const percent = amount => Math.round((amount / RANK_SCALE) * 1e4) / 100;

function findBoss(enemies) {
  let found = null;
  for (const enemy of enemies) {
    const boss = BOSSES[enemy.routine];
    const part = boss?.parts[enemy.part];
    if (!part || enemy.hp === 0) continue;
    if ((enemy.status & (part.mask ?? BOSS_PART_MASK)) === (part.value ?? BOSS_PART_VALUE))
      found = { name: boss.name, part: enemy.part, hp: enemy.hp };
  }
  return found;
}

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
  'p1.score': {
    description: "Standard: player 1's score, or null outside play, when memory holds the last game's.",
    value: v => (playerIn(v, 1) ? v['p1.score_raw'] : null),
  },
  'p2.score': {
    description: "Standard: player 2's score, or null outside play.",
    value: v => (playerIn(v, 2) ? v['p2.score_raw'] : null),
  },
  'p1.next_extend_at': {
    description: 'The score that gives player 1 the next extend, or null once there are no more.',
    value: v => (playerIn(v, 1) && v['p1.next_extend'] !== NO_MORE_EXTENDS ? v['p1.next_extend'] : null),
  },
  'p1.score_gain': {
    description: 'Points player 1 has just scored: what scoring in quick succession added up to, for about a second.',
    value: (v, prev) => {
      if (!bothRunning(v, prev)) return null;
      const gained = v['p1.score'] - prev['p1.score'];
      const recent = prev['p1.score_gain_age'] !== null && prev['p1.score_gain_age'] < GAIN_TICKS - 1;
      if (gained > 0) return (recent ? prev['p1.score_gain'] : 0) + gained;
      return recent ? prev['p1.score_gain'] : null;
    },
  },
  'p1.score_gain_age': {
    description: 'Ticks since player 1 last scored, while p1.score_gain is shown.',
    value: (v, prev) => {
      if (v['p1.score_gain'] === null) return null;
      return v['p1.score'] !== prev['p1.score'] ? 0 : prev['p1.score_gain_age'] + 1;
    },
  },

  'p1.shot_items_needed': {
    description: 'Small shot items player 1 still needs for the next shot level, or null at the top level.',
    value: v =>
      playerIn(v, 1) && v['p1.shot_level'] < SHOT_LEVEL_MAX
        ? SHOT_ITEMS_NEEDED[v['p1.shot_level']] - v['p1.shot_items']
        : null,
  },

  'medal.next': {
    description: 'Points the next medal released will be worth.',
    value: v =>
      v['game.running'] ? (MEDAL_POINTS[Math.min(v['medal.value'] / 2, MEDAL_POINTS.length - 1)] ?? null) : null,
  },

  // Rank. rank.percent follows the MAME layout: 0% is the easiest rank there could be, 100% the hardest.
  'rank.percent': {
    description: 'Rank as a percentage: 0 easiest, 100 hardest.',
    value: v => (v['game.running'] ? percent(RANK_SCALE - v.rank) : null),
  },
  'rank.drift': {
    description: 'How much harder the rank gets every frame, in rank units: the timer rank plus unused credits.',
    value: v => (v['game.running'] ? v['rank.per_frame'] + v.credits : null),
  },
  'rank.tick': {
    description: "This tick's rank changes, as text for tooling: time, events and the rest.",
    value: (v, prev) => {
      if (!bothRunning(v, prev)) return null;
      const { drift, events, shots } = rankTick(v, prev);
      return [`time ${drift}`, ...events.map(e => `${e.reason} ${e.amount}`), `shots ${shots}`].join(', ');
    },
  },
  ...Object.fromEntries(
    RANK_PARTS.map(part => [
      `rank.from_${part}`,
      {
        description: `How much harder the rank has got from ${part} this game, in rank units (negative is easier).`,
        value: (v, prev) => {
          if (!v['game.running']) return null;
          if (!bothRunning(v, prev)) return 0;
          const { drift, events, shots } = rankTick(v, prev);
          const amount =
            part === 'time'
              ? drift
              : part === 'shots'
                ? shots
                : events.filter(e => e.part === part).reduce((sum, e) => sum + e.amount, 0);
          return prev[`rank.from_${part}`] + amount;
        },
      },
    ]),
  ),
  'rank.change': {
    description: 'The latest rank change with a known cause (a death, an extend, a pickup), in rank units.',
    value: (v, prev) => {
      if (!bothRunning(v, prev)) return null;
      const { events } = rankTick(v, prev);
      const amount = events.reduce((sum, e) => sum + e.amount, 0);
      return events.length > 0 && amount !== 0 ? amount : prev['rank.change'];
    },
  },
  'rank.change_reason': {
    description: 'What caused rank.change.',
    value: (v, prev) => {
      if (!bothRunning(v, prev)) return null;
      const { events } = rankTick(v, prev);
      const counted = events.filter(e => e.amount !== 0);
      return counted.length > 0 ? counted.map(e => e.reason).join(' + ') : prev['rank.change_reason'];
    },
  },
  'rank.change_age': {
    description: 'Ticks since rank.change, while it is recent.',
    value: (v, prev) => {
      if (v['rank.change'] === null) return null;
      const fresh = rankTick(v, prev).events.some(e => e.amount !== 0);
      if (fresh) return 0;
      return prev['rank.change_age'] === null || prev['rank.change_age'] >= CHANGE_TICKS
        ? null
        : prev['rank.change_age'] + 1;
    },
  },

  // The boss on screen, by the MAME layout's table of boss routines and parts.
  'boss.name': {
    description: 'The boss whose HP is shown, or null when there is none.',
    value: v => (v['game.running'] ? (findBoss(v.enemies)?.name ?? null) : null),
  },
  'boss.part': {
    description: 'The part of the boss whose HP is shown (enemies[].part).',
    value: v => (v['game.running'] ? (findBoss(v.enemies)?.part ?? null) : null),
  },
  'boss.hp': {
    description: "The boss part's HP.",
    value: v => (v['game.running'] ? (findBoss(v.enemies)?.hp ?? null) : null),
  },
  'boss.hp_max': {
    description: 'The most HP this boss part has had since it appeared: the full bar.',
    value: (v, prev) => {
      if (v['boss.hp'] === null) return null;
      const same = prev !== null && prev['boss.name'] === v['boss.name'] && prev['boss.part'] === v['boss.part'];
      return same ? Math.max(prev['boss.hp_max'], v['boss.hp']) : v['boss.hp'];
    },
  },

  'boss.progress': {
    description: 'How far the stage has scrolled towards its boss (0-1), on stages whose boss position is known.',
    value: v => {
      const at = BOSS_AT[v['stage.scene']];
      return v['game.running'] && at !== undefined ? Math.min(v['stage.scroll'] / at, 1) : null;
    },
  },
  'boss.eta': {
    description: 'Roughly how many seconds until the boss arrives, at the current scroll speed.',
    value: v => {
      const at = BOSS_AT[v['stage.scene']];
      if (v['boss.progress'] === null || v['boss.progress'] >= 1 || v['stage.scroll_speed'] <= 0) return null;
      return (at - v['stage.scroll']) / v['stage.scroll_speed'] / FRAMES_PER_SECOND;
    },
  },

  // The route: the course's stages by name, and where the run is on it. Found by scene rather than stage number, since a
  // hidden boss's stage adds to stage.counter.
  ...Object.fromEntries(
    Array.from({ length: ROUTE_ROWS }, (_, i) => [
      `route.${i + 1}`,
      {
        description: `The name of the course's stage ${i + 1}, or null past the course's last stage or on the boss course.`,
        value: v => (v['game.running'] ? (SCENE_NAMES[routeScenes(v)[i]] ?? null) : null),
      },
    ]),
  ),
  'route.at': {
    description: "The course's stage being played (1 for the first), or null on a hidden boss's stage.",
    value: v => {
      if (!v['game.running']) return null;
      const at = routeScenes(v).indexOf(v['stage.scene']);
      return at < 0 ? null : at + 1;
    },
  },
  'route.done': {
    description: "How many of the course's stages are behind the run.",
    value: v => {
      if (!v['game.running'] || !ROUTES[v.course]) return null;
      return v['route.at'] !== null ? v['route.at'] - 1 : (HIDDEN_AFTER[v['stage.scene']] ?? null);
    },
  },
  'route.extra': {
    description: "The hidden boss's stage being played, between the course's stages, or null.",
    value: v =>
      v['game.running'] && HIDDEN_AFTER[v['stage.scene']] !== undefined ? SCENE_NAMES[v['stage.scene']] : null,
  },
};
