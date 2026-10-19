// What pack tests (packs/<id>/tests) import as @stgadgets/testing: show a pack's overlay in a given game state without
// a server or an emulator, and find gadgets in it. It stands in for a test package that pack authors will install once
// packs can live outside this repository.

import { waitFor } from "@testing-library/react";
import type { Vars } from "../../src/ui/src/sdk";
import { root, startApp, type FakeServer } from "./fakeServer";

export { root, startApp, type FakeServer };

/**
 * Named game states for a pack's tests (packs/<id>/tests/states.ts): each is every variable the overlay reads, map
 * and derived alike, as the server would send them on one tick.
 */
export type GameStates = Record<string, Vars>;

/**
 * Starts the UI, has the fake server match `pack` with `values` on its first tick, and waits for the pack's layout
 * to load. Further ticks can be sent with the returned server.
 */
export async function showState(pack: string, values: Vars) {
  const server = await startApp();
  server.startGame(pack, values);
  await waitFor(() => {
    if (!root().querySelector(".layout")) throw new Error(`The layout for ${pack} didn't load`);
  });
  return server;
}

/**
 * The gadget on screen whose label matches: the whole label text for a string, e.g. "LIVES", or a pattern, e.g.
 * /^BOSS/. Fails, listing the labels on screen, if there isn't one.
 */
export function gadget(label: string | RegExp): HTMLElement {
  const gadgets = [...root().querySelectorAll<HTMLElement>(".gadget")];
  const labelOf = (el: HTMLElement) => el.querySelector(".label")?.textContent?.trim() ?? "";
  const matches = (text: string) => (typeof label === "string" ? text === label : label.test(text));
  const found = gadgets.find((el) => matches(labelOf(el)));
  if (!found) {
    throw new Error(`No gadget labelled ${label}. On screen: ${gadgets.map((el) => JSON.stringify(labelOf(el))).join(", ")}`);
  }
  return found;
}

/** The text of a gadget's part: its value (".value") by default, or e.g. ".note". */
export function textOf(label: string | RegExp, part = ".value") {
  return gadget(label).querySelector(part)?.textContent ?? null;
}
