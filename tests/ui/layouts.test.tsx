import { render, screen, waitFor } from "@testing-library/react";
import type { ComponentType } from "react";
import { describe, expect, it } from "vitest";
import { root, startApp } from "./fakeServer";

// A pack ID with no UI of its own, so it gets the generic layout.
const NO_UI = "no-such-pack";

/** The label and value of each gadget on screen, in order. */
function readouts() {
  return [...root().querySelectorAll(".gadget")].map(gadget => [
    gadget.querySelector(".label")?.textContent,
    gadget.querySelector(".value")?.textContent,
  ]);
}

describe("generic layout", () => {
  it("shows a gadget for each standard variable the game's map defines", async () => {
    const server = await startApp();

    server.startGame(NO_UI, { "p1.score": 1230, hiscore: 5000, "p1.lives": 3, credits: 1 });

    expect(readouts()).toEqual([
      ["1UP", "1230"],
      ["HI-SCORE", "5000"],
      ["LIVES", "3"],
      ["CREDIT", "1"],
      ["READ → PAINT", "–"],
    ]);
  });

  it("shows only the latency meter when the map defines no standard variables", async () => {
    const server = await startApp();

    server.startGame(NO_UI, { "boss.hp": 100 });

    expect(readouts()).toEqual([["READ → PAINT", "–"]]);
  });

  it("pads scores with zeros", async () => {
    const server = await startApp();

    server.startGame(NO_UI, { "p1.score": 50 });

    expect(screen.getByText("0050")).toBeDefined();
  });

  it("shows dashes for values the game hasn't set", async () => {
    const server = await startApp();

    server.startGame(NO_UI, { "p1.score": null, stage: null });

    expect(readouts().slice(0, 2)).toEqual([
      ["1UP", "----"],
      ["STAGE", "-"],
    ]);
  });

  it("updates as the game plays", async () => {
    const server = await startApp();
    server.startGame(NO_UI, { "p1.score": 0, "p1.lives": 3 });

    server.tick({ "p1.score": 150 });
    server.tick({ "p1.lives": 2 });

    expect(readouts().slice(0, 2)).toEqual([
      ["1UP", "0150"],
      ["LIVES", "2"],
    ]);
  });

  it("keeps values that a tick doesn't change", async () => {
    const server = await startApp();
    server.startGame(NO_UI, { "p1.score": 150, hiscore: 5000 });

    server.tick({ "p1.score": 300 });

    expect(screen.getByText("5000")).toBeDefined();
  });

  it("starts afresh when the game changes", async () => {
    const server = await startApp();
    server.startGame(NO_UI, { "p1.score": 150, "p1.bombs": 3 });

    server.startGame("another-pack-with-no-ui", { "p1.score": 0 });

    expect(readouts()).toEqual([
      ["1UP", "0000"],
      ["READ → PAINT", "–"],
    ]);
  });

  it("shows read → paint latency once ticks are painted", async () => {
    const server = await startApp();
    server.startGame(NO_UI, { "p1.score": 0 });

    for (let score = 10; score <= 50; score += 10) {
      server.tick({ "p1.score": score });
      await new Promise(resolve => requestAnimationFrame(resolve));
    }

    await waitFor(() => expect(screen.getByText(/ms/).textContent).toMatch(/^[\d.]+ \/ [\d.]+ ms p50\/p95 · \d+ Hz$/));
  });
});

// Every pack that has its own UI (packs/<id>/ui/layout.tsx).
const packLayouts = Object.entries(import.meta.glob<{ default: ComponentType }>("../../packs/*/ui/layout.tsx")).map(
  ([path, load]) => ({ pack: path.split("/").at(-3)!, load }),
);

describe.each(packLayouts)("pack layout: $pack", ({ pack, load }) => {
  it("is shown instead of the generic layout", async () => {
    const server = await startApp();
    server.startGame(pack, { "p1.score": 150, hiscore: 5000 });

    // The same layout rendered directly, sharing the app's connection.
    const { default: Layout } = await load();
    const expected = render(<Layout />).container;

    await waitFor(() => expect(root().innerHTML).toBe(expected.innerHTML));
    expect(root().innerHTML).not.toBe("");
  });
});
