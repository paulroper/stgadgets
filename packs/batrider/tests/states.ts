// Game states for the overlay's tests and for looking at the layout (docs/packs.md#testing-the-ui). Each is a run of
// ticks of map values; the pack's derived.js works out everything else, as the server would.

import type { GameStates } from "@stgadgets/testing";

const ENEMY_SLOTS = 96;
const EMPTY = { status: 0, active: false, routine: 0, part: 0, hp: 0 };
const CONFRICT = 0x0531b4; // stage 1's boss
const BOSS_BODY = 0x8200; // status the body shows while it can be hit (CONFRICT's part 0)

/** The enemy table, empty but for the entries given by slot. */
function enemies(entries: Record<number, Partial<typeof EMPTY>> = {}) {
  return Array.from({ length: ENEMY_SLOTS }, (_, i) => ({ ...EMPTY, ...entries[i] }));
}

// Normal at the defaults: rank starts at its minimum, and the timer rank takes 4 off it every frame.
const RANK_MIN = 0xf80000;
const DRIFT = 4;
const LARGE_SHOT = 0x8000;
const FIRST_DEATH = 0x40000; // eased, with two pilots left

export default {
  // Stage 1 of a game on the normal course with the Police team (ships 0x00, 0x10, 0x20), solo, one credit.
  start: {
    "stage.counter": 1,
    "stage.scene": 1,
    "stage.order.2": 3,
    "stage.order.3": 1,
    "stage.order.4": 2,
    course: 2,
    "team.last_index": 2,
    frame_counter: 5000,
    hiscore: 1613210,
    credits: 0,
    "settings.difficulty": 1,
    "settings.timer_rank": 1,
    "settings.extend_every": 1500000,
    "settings.extend_last": 0,
    rank: RANK_MIN,
    "rank.per_frame": DRIFT,
    "rank.min": RANK_MIN,
    "rank.start_multiplier": 0x100,
    "p1.status": 0x8e,
    "p1.active": true,
    "p1.ship": 0x00,
    "p1.team.0": 0x00,
    "p1.team.1": 0x10,
    "p1.team.2": 0x20,
    "p1.score_raw": 12340,
    "p1.next_extend": 1500000,
    "p1.continues": 0,
    "p1.bombs": 1,
    "p1.bomb_fragments": 20,
    "p1.shot_charge": 0,
    "p1.shot_level": 0,
    "p1.shot_items": 0,
    "p1.options": 0,
    "p1.option_formation": 0,
    "p1.option_level": 0,
    "p2.status": 0,
    "p2.active": false,
    "p2.ship": 0,
    "p2.team.0": 0,
    "p2.team.1": 0,
    "p2.team.2": 0,
    "p2.score_raw": 0,
    "p2.next_extend": 0,
    "p2.continues": 0,
    "p2.option_formation": 0,
    "p2.bombs": 0,
    "p2.bomb_fragments": 0,
    "p2.shot_charge": 0,
    "p2.shot_level": 0,
    "p2.shot_items": 0,
    "p2.options": 0,
    "p2.option_level": 0,
    "medal.value": 0,
    "drop.position": 0,
    "dropped.small_shot": 0,
    "dropped.bomb": 0,
    "dropped.medal": 0,
    "dropped.large_shot": 0,
    "dropped.large_bomb": 0,
    "dropped.option": 0,
    "dropped.extend": 0,
    "special.shot": 0,
    "special.option": 0,
    "hidden.glow_squid": 0,
    "hidden.bashinet_r": 0,
    "hidden.bashinet_mk2": 0,
    "hidden.gob_robo": 0,
    "hidden.bashinet": 0,
    "hidden.black_heart": 0,
    enemies: enemies(),
  },
  // A frame passes every tick, and the timer rank takes its share.
  add: { frame_counter: 1, rank: -DRIFT },

  states: {
    /** Ten seconds into stage 1: a chain of three medals, then a large shot that has just raised the shot level. */
    playing: [
      { add: { "p1.score_raw": 20 }, repeat: 300 },
      { set: { "medal.value": 6, "p1.score_raw": 18940 }, repeat: 300 },
      { set: { "p1.shot_level": 1, rank: RANK_MIN - DRIFT * 600 - LARGE_SHOT } },
    ],

    /** Stage 1's boss, its body worn down from 84 HP to 60. */
    bossFight: [
      { set: { enemies: enemies({ 29: { status: BOSS_BODY, active: true, routine: CONFRICT, hp: 84 } }) }, repeat: 60 },
      { set: { enemies: enemies({ 29: { status: BOSS_BODY, active: true, routine: CONFRICT, hp: 60 } }) } },
    ],

    /** Sewage System, stage 2, just after the first pilot went down: two left, and rank eased. */
    afterDeath: [
      { set: { "stage.counter": 2, "stage.scene": 4, "p1.shot_level": 2, "p1.option_level": 1 }, repeat: 300 },
      { set: { "p1.status": 0x81 }, repeat: 90 },
      {
        set: {
          "p1.status": 0x8e,
          "p1.ship": 0x10,
          "p1.team.0": 0x01,
          "p1.shot_level": 0,
          "p1.option_level": 0,
          rank: RANK_MIN - DRIFT * 390 + FIRST_DEATH,
        },
      },
    ],

    /** Three small shots let fall in a row and two options: the next small shot is a special level-up. */
    specialReady: [{ set: { "dropped.small_shot": 3, "dropped.option": 2 } }],

    /** Everything at its top: shot level 3, four bombs and 40 fragments, and 10,000 point medals. */
    maxedOut: [
      {
        set: {
          "p1.shot_level": 3,
          "p1.bombs": 4,
          "p1.bomb_fragments": 40,
          "medal.value": 36,
          "p1.next_extend": 0xffffffff,
        },
      },
    ],

    /** The title screen: memory still holds the last game's score, and no one is playing. */
    titleScreen: [{ set: { "stage.counter": 0, "p1.active": false, "p1.status": 0, rank: 0 } }],
  },
} satisfies GameStates;
