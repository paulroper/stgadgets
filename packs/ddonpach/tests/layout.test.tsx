import { gadget, showState, textOf } from "@stgadgets/testing";
import { describe, expect, it } from "vitest";
import states from "./states";

describe("DoDonPachi overlay", () => {
  it("shows the run while chaining on stage 1", async () => {
    await showState("ddonpach", states.chaining);

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
    await showState("ddonpach", states.titleScreen);

    expect(textOf("SCORE")).toBe("---------");
    expect(textOf("HI-SCORE")).toBe("005908065");
    expect(textOf("RANK")).toBe("- / 63");
    expect(textOf("BOSS")).toBe("-");
    expect(textOf("2ND LOOP")).toBe("-");
  });

  it("shows a boss fight with splits so far and slowdown", async () => {
    await showState("ddonpach", states.bossFight);

    expect(gadget(/^BOSS/).classList).toContain("boss-here");
    expect(textOf(/^BOSS/)).toBe("HERE!");
    expect(textOf("GAME SPEED")).toBe("62%");
    expect(textOf(/BEES$/, ".note")).toBe("no perfect this stage");
    const rows = [...gadget("SPLITS").querySelectorAll("tbody tr")].map((row) =>
      [...row.querySelectorAll("td")].map((cell) => cell.textContent),
    );
    expect(rows.slice(0, 5)).toEqual([
      ["1-1", "2:26", "3,120,400", "0", "0", "4%"],
      ["1-2", "2:36", "6,870,150", "0", "1", "11%"],
      ["1-3", "2:48", "9,902,410", "1", "2", "8%"],
      ["1-4", "2:05", "4,922,340", "0", "0", "13%"],
      ["1-5", "-", "-", "-", "-", "-"],
    ]);
  });

  it("shows where rank is heading before the game recalculates it", async () => {
    await showState("ddonpach", states.rankRising);

    expect(textOf("RANK")).toBe("25 / 63 → 29");
    expect(textOf("RANK", ".rank-change")).toBe("+4 ship & power +4");
  });

  it("celebrates a perfect stage of bees", async () => {
    await showState("ddonpach", states.perfectBees);

    expect(textOf(/BEES$/)).toBe("13 / 13");
    expect(textOf(/BEES$/, ".note")).toBe("PERFECT!");
  });

  it("says when the second loop and extends are out of reach", async () => {
    await showState("ddonpach", states.continued);

    expect(textOf("2ND LOOP")).toBe("NOT THIS CREDIT");
    expect(textOf("LIVES", ".note")).toBe("no more extends");
    expect(textOf("STAGE CLEAR", ".note")).toBe("no miss lost · stars 0");
  });

  it("keeps the credit's progress on the continue screen", async () => {
    await showState("ddonpach", states.continueScreen);

    expect(textOf("SCORE")).toBe("---------");
    expect(textOf("2ND LOOP")).toBe("ON TRACK");
  });

  it("says when the game is paused", async () => {
    await showState("ddonpach", states.paused);

    expect(textOf("GAME SPEED")).toBe("PAUSED");
  });

  it("shows the second loop", async () => {
    await showState("ddonpach", states.secondLoop);

    expect(textOf("2ND LOOP")).toBe("LOOP 2");
    expect(textOf("BOSS · 2-3")).toBe("in ~74 s");
    expect(textOf("RANK")).toBe("63 / 63");
  });
});
