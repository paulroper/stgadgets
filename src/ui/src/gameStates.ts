// A pack's game states (packs/<id>/tests/states.ts), computed the way the server would: each state is a run of ticks
// of map values, and its values are every variable after the last tick, with the pack's derived.js run over each
// tick. Used by tests and, in development, by `?state=` (devStates.ts); never part of a production build.

import type { Value, Vars } from "./sdk/client";

type MapValues = Record<string, Value>;

/** One step of a state: set some map variables, then play <c>repeat</c> ticks (1 by default). */
export interface GameStateStep {
  /** Map variables to change on the step's first tick; the others keep their values. */
  set?: MapValues;
  /** Added to map variables on each of the step's ticks, on top of the file's <c>add</c>. */
  add?: Record<string, number>;
  repeat?: number;
}

/** What a pack's tests/states.ts default-exports. */
export interface GameStates {
  /** Memory on every state's first tick: every map variable the pack's map defines, as decoded. */
  start: MapValues;
  /** Added to map variables on every tick after a state's first, e.g. a frame counter's 1. */
  add?: Record<string, number>;
  /** Each state's steps, by name. */
  states: Record<string, GameStateStep[]>;
}

type DerivedFunction = (v: MapValues, prev: MapValues | null) => unknown;
type DerivedScript = Record<string, DerivedFunction | { value: DerivedFunction; description?: string }>;

const statesModules = import.meta.glob<GameStates>("../../../packs/*/tests/states.ts", { import: "default" });
const derivedScripts = import.meta.glob<DerivedScript>("../../../packs/*/derived.js", { import: "default" });
const packOf = (path: string) => path.split("/")[4]; // ../../../packs/<id>/...

/** The packs that have game states. */
export const packsWithStates = Object.keys(statesModules)
  .map(packOf)
  .filter(pack => pack !== undefined);

/** Every game state of a pack, by name. Throws if the pack has none, or a state can't be computed. */
export async function gameStates(pack: string): Promise<Record<string, Vars>> {
  const load = statesModules[`../../../packs/${pack}/tests/states.ts`];
  if (!load) throw new Error(`No game states for '${pack}' (packs/${pack}/tests/states.ts)`);
  const file = await load();
  const loadDerived = derivedScripts[`../../../packs/${pack}/derived.js`];
  const derived = loadDerived ? await loadDerived() : {};

  return Object.fromEntries(
    Object.entries(file.states).map(([name, steps]) => [name, play(name, steps, file, derived)]),
  );
}

// Plays one state's ticks, as derived.js scenarios do (src/lib/Stgadgets.Core/Derived/DerivedScenarios.cs): adds
// apply from the second tick on, and a step's first tick has exactly the values it sets.
function play(name: string, steps: GameStateStep[], file: GameStates, derived: DerivedScript): Vars {
  const memory: MapValues = { ...file.start };
  let prev: MapValues | null = null;
  let tick = 0;
  for (const step of steps) {
    for (let repeat = 0; repeat < (step.repeat ?? 1); repeat++, tick++) {
      if (tick > 0) {
        for (const [variable, amount] of [...Object.entries(file.add ?? {}), ...Object.entries(step.add ?? {})]) {
          memory[variable] = (memory[variable] as number) + amount;
        }
      }
      if (repeat === 0) {
        for (const [variable, value] of Object.entries(step.set ?? {})) {
          if (!(variable in file.start))
            throw new Error(`State '${name}' sets '${variable}', which isn't in start: only map variables can be set`);
          memory[variable] = value;
        }
      }
      prev = evaluate(name, tick, derived, { ...memory }, prev);
    }
  }
  if (prev === null) throw new Error(`State '${name}' has no ticks`);
  return prev;
}

// One tick of derived.js, as the server's DerivedEvaluator does (src/lib/Stgadgets.Core/Derived/DerivedScript.cs):
// values in order, each seeing the ones before it and the previous tick's. A value that throws or returns something
// other than a finite number, boolean, string or null fails the state. So does reading a name the tick doesn't have,
// which usually means a map variable missing from start.
function evaluate(
  name: string,
  tick: number,
  derived: DerivedScript,
  values: MapValues,
  prev: MapValues | null,
): MapValues {
  const where = (variable: string) => `State '${name}' (tick ${tick}): derived '${variable}'`;
  let current = "";
  const v = new Proxy(values, {
    get: (target, key) => {
      if (typeof key === "string" && !(key in target))
        throw new Error(`${where(current)} read '${key}', which this tick doesn't have`);
      return Reflect.get(target, key);
    },
  });

  for (const [variable, entry] of Object.entries(derived)) {
    current = variable;
    const value = typeof entry === "function" ? entry : entry.value;
    let result: unknown;
    try {
      result = value(v, prev);
    } catch (error) {
      throw new Error(`${where(variable)} failed: ${(error as Error).message}`, { cause: error });
    }
    const valid =
      result === null ||
      typeof result === "boolean" ||
      typeof result === "string" ||
      (typeof result === "number" && Number.isFinite(result));
    if (!valid) throw new Error(`${where(variable)} returned ${String(result)}`);
    values[variable] = result as Value;
  }
  return values;
}
