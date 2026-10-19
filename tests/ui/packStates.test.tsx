import { afterEach, describe, expect, it, vi } from "vitest";
import { showState, type GameStates } from "./testing";

afterEach(() => {
  vi.restoreAllMocks();
});

// Every pack's game states for tests (packs/<id>/tests/states.ts).
const packStates = Object.entries(
  import.meta.glob<GameStates>("../../packs/*/tests/states.ts", { eager: true, import: "default" }),
).flatMap(([path, states]) =>
  Object.entries(states).map(([state, values]) => ({ pack: path.split("/").at(-3)!, state, values })),
);

describe.each(packStates)("pack state: $pack $state", ({ pack, values }) => {
  it("renders the pack's layout without errors", async () => {
    const errors = vi.spyOn(console, "error");

    await showState(pack, values);

    expect(errors).not.toHaveBeenCalled();
  });
});
