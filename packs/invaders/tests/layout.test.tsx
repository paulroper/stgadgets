import { gadget, showState, textOf } from "@stgadgets/testing";
import { describe, expect, it } from "vitest";
import states from "./states";

describe("Space Invaders overlay", () => {
  it("shows the mystery ship flying and the points just scored", async () => {
    await showState("invaders", states.saucerFlying);

    expect(textOf("SCORE<1>")).toBe("0820+100");
    expect(textOf("MYSTERY SHIP")).toBe("FLYING!");
    expect(textOf("NEXT HIT", ".note")).toBe("300 on shot 9");
    expect(textOf("ACCURACY")).toBe("69%");
    expect(textOf("LIVES")).toBe("3");
    expect(gadget("LIVES").querySelectorAll(".lives-ship")).toHaveLength(3);
  });

  it("counts down to the mystery ship at the start of a wave", async () => {
    await showState("invaders", states.waveStart);

    expect(textOf("MYSTERY SHIP")).toBe("25.6 s");
    expect(textOf("MYSTERY SHIP", ".note")).toBe("paused until the rack drops");
    expect(textOf("MARCH TEMPO")).toBe("52 f/note");
    expect(textOf("SPEED-UP IN")).toBe("6 KILLS");
    expect(textOf("ACCURACY")).toBe("-");
  });

  it("flags the shot that scores 300", async () => {
    await showState("invaders", states.saucerJackpot);

    expect(textOf("MYSTERY SHIP")).toBe("ANY MOMENT");
    expect(textOf("NEXT HIT")).toBe("300");
    expect(textOf("NEXT HIT", ".note")).toBe("300 on this shot!");
    expect(textOf("MARCH TEMPO", ".note")).toBe("game catching up: 34");
    expect(textOf("SPEED-UP IN")).toBe("1 KILL");
  });

  it("shows the march at full speed with one invader left", async () => {
    await showState("invaders", states.lastInvader);

    expect(textOf("SPEED-UP IN")).toBe("MAX");
    expect(textOf("MYSTERY SHIP")).toBe("NO MORE");
  });

  it("keeps the score but clears the play readouts at game over", async () => {
    await showState("invaders", states.gameOver);

    expect(textOf("SCORE<1>")).toBe("0960");
    expect(textOf("HI-SCORE")).toBe("0960");
    expect(textOf("LIVES")).toBe("-");
    expect(textOf("MYSTERY SHIP")).toBe("-");
    expect(textOf("MARCH TEMPO")).toBe("- f/note");
  });
});
