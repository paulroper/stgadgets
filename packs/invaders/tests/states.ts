// Game states for the overlay's tests: every variable the layout reads, as the server sends them (map and derived.js
// alike). `saucerFlying` was taken from real play; the others start from it and change what that moment changes.

import type { GameStates } from "@stgadgets/testing";

/** Mid-wave with 15 invaders left: the mystery ship is flying and was just hit for 100. */
const saucerFlying = {
  "p1.score": 820,
  "p1.score_popup": 100,
  hiscore: 0,
  credits: 1,
  "p1.lives": 3,
  "p1.kills": 40,
  "p1.shots": 58,
  "p1.accuracy": 40 / 58,
  "fleet.aliens": 15,
  "fleet.tempo": 21,
  "fleet.sound_delay": 21,
  "fleet.speedup_in": 3,
  "saucer.status": "flying",
  "saucer.appears_in": null,
  "saucer.next_score": 100,
  "saucer.shots_to_300": 9,
} satisfies GameStates[string];

export default {
  saucerFlying,

  /** A new wave: 55 invaders at the slowest march, and the mystery ship held until the rack drops. */
  waveStart: {
    ...saucerFlying,
    "p1.score": 0,
    "p1.score_popup": null,
    "p1.kills": 0,
    "p1.shots": 0,
    "p1.accuracy": null,
    "fleet.aliens": 55,
    "fleet.tempo": 52,
    "fleet.sound_delay": 52,
    "fleet.speedup_in": 6,
    "saucer.status": "paused",
    "saucer.appears_in": 1536,
    "saucer.next_score": 50,
    "saucer.shots_to_300": 8,
  },

  /** The next shot scores 300 if it hits the mystery ship, which is due any moment. */
  saucerJackpot: {
    ...saucerFlying,
    "p1.score_popup": null,
    "fleet.aliens": 22,
    "fleet.tempo": 28,
    "fleet.sound_delay": 34,
    "fleet.speedup_in": 1,
    "saucer.status": "due",
    "saucer.appears_in": 0,
    "saucer.next_score": 300,
    "saucer.shots_to_300": 1,
  },

  /** One invader left: the march is at full speed and no more mystery ships come this wave. */
  lastInvader: {
    ...saucerFlying,
    "p1.score_popup": null,
    "p1.lives": 1,
    "fleet.aliens": 1,
    "fleet.tempo": 5,
    "fleet.sound_delay": 5,
    "fleet.speedup_in": null,
    "saucer.status": "none",
    "saucer.next_score": 150,
    "saucer.shots_to_300": 11,
  },

  /** Game over: derived values are null outside play; the score and high score stay on screen. */
  gameOver: {
    "p1.score": 960,
    "p1.score_popup": null,
    hiscore: 960,
    credits: 1,
    "p1.lives": null,
    "p1.kills": 7,
    "p1.shots": 12,
    "p1.accuracy": 7 / 12,
    "fleet.aliens": null,
    "fleet.tempo": null,
    "fleet.sound_delay": 16,
    "fleet.speedup_in": null,
    "saucer.status": null,
    "saucer.appears_in": null,
    "saucer.next_score": null,
    "saucer.shots_to_300": null,
  },
} satisfies GameStates;
