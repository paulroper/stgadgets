import { gadget, showState, textOf } from "@stgadgets/testing";
import { describe, expect, it } from "vitest";

/** The cells of each row of the splits table. */
function splits() {
  return [...gadget("SPLITS").querySelectorAll("tbody tr")].map((row) => [...row.querySelectorAll("td")].map((cell) => cell.textContent));
}

describe("DoDonPachi overlay", () => {
  it("shows the run while chaining on stage 1", async () => {
    await showState("ddonpach", "chaining");

    expect(textOf("SCORE")).toBe("000010500+1300");
    expect(textOf("CHAIN")).toBe("14 HIT");
    expect(textOf("LIVES", ".note")).toBe("next at 6,000,000");
    expect(textOf("STAGE CLEAR")).toBe("+200,000");
    expect(textOf("RANK")).toBe("25 / 63");
    expect(textOf("BOSS · 1-1")).toBe("in ~74 s");
    expect(textOf("2ND LOOP")).toBe("ON TRACK");
    expect(textOf("GAME SPEED")).toBe("100%");
  });

  it("shows dashes, not the last game's leftovers, on the title screen", async () => {
    await showState("ddonpach", "titleScreen");

    expect(textOf("SCORE")).toBe("---------");
    expect(textOf("HI-SCORE")).toBe("005908065");
    expect(textOf("RANK")).toBe("- / 63");
    expect(textOf("BOSS")).toBe("-");
    expect(textOf("2ND LOOP")).toBe("-");
  });

  it("shows a boss fight with splits so far and slowdown", async () => {
    await showState("ddonpach", "bossFight");

    expect(gadget(/^BOSS/).classList).toContain("boss-here");
    expect(textOf(/^BOSS/)).toBe("HERE!");
    expect(textOf("GAME SPEED")).toBe("52%");
    expect(textOf(/BEES$/, ".note")).toBe("next +8,000"); // 5 bees this stage and 2 perfect stages: the 8th value
    expect(splits().slice(0, 3)).toEqual([
      ["1-1", "0:26", "2,248,500", "0", "0", "0", "0%"],
      ["1-2", "0:31", "5,400,000", "0", "1", "0", "0%"],
      ["1-3", "0:41", "8,000,000", "1", "0", "0", "0%"],
    ]);
    expect(splits()[4]).toEqual(["1-5", "-", "-", "-", "-", "-", "-"]);
  });

  it("shows where rank is heading before the game recalculates it", async () => {
    await showState("ddonpach", "rankRising");

    expect(textOf("RANK")).toBe("25 / 63 → 31");
    expect(textOf("RANK", ".rank-change")).toBe("+6 ship & power +6");
  });

  it("celebrates a perfect stage of bees", async () => {
    await showState("ddonpach", "perfectBees");

    expect(textOf(/BEES$/)).toBe("13 / 13");
    expect(textOf(/BEES$/, ".note")).toBe("PERFECT!");
  });

  it("says when the second loop and extends are out of reach, and counts the continue", async () => {
    await showState("ddonpach", "continued");

    expect(textOf("2ND LOOP")).toBe("NOT THIS CREDIT");
    expect(textOf("LIVES", ".note")).toBe("no more extends");
    expect(textOf("STAGE CLEAR", ".note")).toBe("no miss lost · stars 0");
    const current = splits()[4];
    expect([current[0], current[3], current[5]]).toEqual(["1-5", "1", "1"]); // stage, deaths, continues
  });

  it("keeps the credit's progress on the continue screen", async () => {
    await showState("ddonpach", "continueScreen");

    expect(textOf("SCORE")).toBe("---------");
    expect(textOf("2ND LOOP")).toBe("NEEDS ONE"); // three deaths
    expect(textOf("BOSS")).toBe("-");
    const current = splits()[0];
    expect([current[0], current[3], current[4], current[5]]).toEqual(["1-1", "3", "1", "0"]); // stage, deaths, bombs, continues
  });

  it("says when the game is paused", async () => {
    await showState("ddonpach", "paused");

    expect(textOf("GAME SPEED")).toBe("PAUSED");
  });

  it("shows the second loop", async () => {
    await showState("ddonpach", "secondLoop");

    expect(textOf("2ND LOOP")).toBe("LOOP 2");
    expect(textOf("BOSS · 2-3")).toBe("in ~93 s");
    expect(textOf("RANK")).toBe("63 / 63");
    expect(splits().slice(0, 8).every((row) => row[1] !== "-")).toBe(true); // the first loop and 2-1, 2-2 are done
  });
});
