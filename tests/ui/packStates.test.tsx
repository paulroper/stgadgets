import { afterEach, describe, expect, it, vi } from "vitest";
import { gameStates, packsWithStates } from "../../src/ui/src/gameStates";
import { showState } from "./testing";

afterEach(() => {
  vi.restoreAllMocks();
});

// Every game state of every pack (packs/<id>/tests/states.ts), computed with the pack's derived.js.
const packStates = (
  await Promise.all(
    packsWithStates.map(async pack =>
      Object.entries(await gameStates(pack)).map(([state, values]) => ({ pack, state, values })),
    ),
  )
).flat();

describe.each(packStates)("pack state: $pack $state", ({ pack, values }) => {
  it("renders the pack's layout without errors", async () => {
    const errors = vi.spyOn(console, "error");

    await showState(pack, values);

    expect(errors).not.toHaveBeenCalled();
  });
});
