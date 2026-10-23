import { gadget, showState, textOf } from "@stgadgets/testing";
import { describe, expect, it } from "vitest";

/** Each route row's text: number, stage and state. */
function route() {
  return [...gadget("ROUTE").querySelectorAll("li")].map(row => row.textContent);
}

/** Each row of the dropped items: the item, and how many have fallen (pips lit). */
function drops() {
  return [...gadget("DROPPED").querySelectorAll("li")].map(row => [
    row.querySelector(".drops-name")?.textContent,
    row.querySelectorAll(".drop.on").length,
    row.querySelector(".drops-special")?.textContent,
  ]);
}

describe("Armed Police Batrider overlay", () => {
  it("shows the run on stage 1, with the large shot's rank cost", async () => {
    await showState("batrider", "playing");

    expect(textOf("SCORE")).toBe("00018940");
    expect(textOf("HI-SCORE")).toBe("01613210");
    expect(textOf("LIVES")).toBe("3");
    expect(textOf("LIVES", ".note")).toBe("next at 1,500,000");
    expect(textOf("💣 BOMBS", ".note")).toBe("20 / 40 fragments");
    expect(textOf("POWER", ".power-row")).toBe("SHOT3 small to go");
    expect(textOf("🏅 NEXT MEDAL")).toBe("400");
    expect(textOf("RANK")).toBe("3.33 %");
    expect(textOf("RANK", ".rank-change")).toBe("+0.20% large shot");
    expect(textOf("RANK", ".rank-parts")).toBe("time+0.01%shots & graze0.00%items+0.20%extends0.00%deaths0.00%");
    expect(route()).toEqual(["1MANHATTAN◀", "2SEWAGENEXT", "3AIRPORT", "4SKY HIGH", "5HIGHWAY"]);
    expect(textOf("BOSS")).toBe("- HP");
  });

  it("shows the boss's name and the HP left in its body", async () => {
    await showState("batrider", "bossFight");

    expect(textOf("BOSS · CONFRICT")).toBe("60 HP");
    expect(gadget("BOSS · CONFRICT").querySelector<HTMLElement>(".bar-fill")?.style.width).toBe(`${(60 / 84) * 100}%`);
  });

  it("counts a pilot down, resets their power, and shows the death easing rank", async () => {
    await showState("batrider", "afterDeath");

    expect(textOf("LIVES")).toBe("2");
    expect(gadget("LIVES").querySelectorAll(".pilot.on")).toHaveLength(2);
    expect(textOf("POWER", ".power-row")).toBe("SHOT2 small to go");
    expect(textOf("RANK", ".rank-change")).toBe("-1.56% death");
    expect(route().slice(0, 3)).toEqual(["1MANHATTAN✓", "2SEWAGE◀", "3AIRPORTNEXT"]);
  });

  it("says which items are one away from special", async () => {
    await showState("batrider", "specialReady");

    expect(drops()).toEqual([
      ["SHOT", 3, "MAX SHOT"],
      ["BIG SHOT", 0, ""],
      ["OPTION", 2, ""],
      ["1UP", 0, ""],
    ]);
  });

  it("marks everything at its top", async () => {
    await showState("batrider", "maxedOut");

    expect(textOf("POWER", ".power-level")).toBe("MAX");
    expect(textOf("💣 BOMBS", ".note")).toBe("FULL: fragments score 1,000");
    expect(textOf("🏅 NEXT MEDAL")).toBe("10,000");
    expect(gadget("🏅 NEXT MEDAL").classList).toContain("medal-max");
    expect(textOf("LIVES", ".note")).toBe("no more extends");
  });

  it("shows dashes, not the last game's leftovers, on the title screen", async () => {
    await showState("batrider", "titleScreen");

    expect(textOf("SCORE")).toBe("--------");
    expect(textOf("LIVES")).toBe("-");
    expect(textOf("RANK")).toBe("- %");
    expect(textOf("🏅 NEXT MEDAL")).toBe("-");
    expect(route()).toEqual(["1-", "2-", "3-", "4-", "5-"]);
  });
});
