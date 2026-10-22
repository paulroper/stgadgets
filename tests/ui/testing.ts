// What pack tests (packs/<id>/tests) import as @stgadgets/testing: show a pack's overlay in one of its game states
// (packs/<id>/tests/states.ts) without a server or an emulator, and find gadgets in it. It stands in for a test package
// that pack authors will install once packs can live outside this repository.

import { waitFor } from "@testing-library/react";
import { gameStates } from "../../src/ui/src/gameStates";
import type { Vars } from "../../src/ui/src/sdk";
import { root, startApp, type FakeServer } from "./fakeServer";

export type { GameStates, GameStateStep } from "../../src/ui/src/gameStates";
export { gameStates, root, startApp, type FakeServer };

/**
 * Starts the UI, has the fake server match `pack` with a game state's values on its first tick, and waits for the
 * pack's layout to load. `state` is the name of one of the pack's states, or the values themselves. Further ticks can
 * be sent with the returned server.
 */
export async function showState(pack: string, state: string | Vars) {
  const values = typeof state === "string" ? await stateValues(pack, state) : state;
  const server = await startApp();
  server.startGame(pack, values);
  await waitFor(() => {
    if (!root().querySelector(".layout")) throw new Error(`The layout for ${pack} didn't load`);
  });
  return server;
}

/** Every variable in one of a pack's game states, computed from its map values with the pack's derived.js. */
export async function stateValues(pack: string, state: string): Promise<Vars> {
  const states = await gameStates(pack);
  return states[state] ?? failWith(`No game state '${state}' for ${pack}. States: ${Object.keys(states).join(", ")}`);
}

function failWith(message: string): never {
  throw new Error(message);
}

const labelOf = (el: HTMLElement) => el.querySelector(".label")?.textContent?.trim() ?? "";

/**
 * The gadget on screen whose label matches: the whole label text for a string, e.g. "LIVES", or a pattern, e.g.
 * /^BOSS/. Fails, listing the labels on screen, if there isn't one.
 */
export function gadget(label: string | RegExp): HTMLElement {
  const gadgets = [...root().querySelectorAll<HTMLElement>(".gadget")];
  const matches = (text: string) => (typeof label === "string" ? text === label : label.test(text));
  const found = gadgets.find(el => matches(labelOf(el)));
  if (!found) {
    throw new Error(
      `No gadget labelled ${label}. On screen: ${gadgets.map(el => JSON.stringify(labelOf(el))).join(", ")}`,
    );
  }
  return found;
}

/** The text of a gadget's part: its value (".value") by default, or e.g. ".note". */
export function textOf(label: string | RegExp, part = ".value") {
  return gadget(label).querySelector(part)?.textContent ?? null;
}
