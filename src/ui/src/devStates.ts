// Shows a pack's game state (packs/<id>/tests/states.ts) without a server or an emulator, so a layout can be looked
// at in any moment of the game: `?state=ddonpach/bossFight` in the dev server. Development only (main.tsx).

import { gameStates } from "./gameStates";
import { client } from "./sdk/client";

/** Does what the server does when it matches a game, with `spec` ("<pack>/<state>") as the first tick's values. */
export async function showGameState(spec: string) {
  const [pack, name] = spec.split("/");
  let states;
  try {
    states = await gameStates(pack);
  } catch (error) {
    client.receive({ type: "status", state: "no-pack", detail: (error as Error).message });
    return;
  }

  const values = name ? states[name] : undefined;
  if (!values) {
    const detail = `No game state '${spec}'. Try ${Object.keys(states)
      .map(state => `${pack}/${state}`)
      .join(", ")}`;
    client.receive({ type: "status", state: "no-pack", detail });
    return;
  }

  const game = { title: spec, manufacturer: null, year: null, romset: pack, screen: null };
  client.receive({ type: "hello", pack, game, values: {} });
  client.receive({ type: "status", state: "connected" });
  client.receive({ type: "tick", seq: 1, readAt: Date.now(), changed: values });
}
