import { useEffect, useRef, useSyncExternalStore } from "react";
import { client, type FrameListener, type Value } from "./client";

/** A variable's current value, or undefined if the active map doesn't define it. */
export function useVar<T extends Value = Value>(name: string): T | undefined {
  return useSyncExternalStore(client.subscribe, () => client.vars[name] as T | undefined);
}

/** The ID of the matched game pack, e.g. "invaders", or null before the server has matched one. */
export function usePack() {
  return useSyncExternalStore(client.subscribe, () => client.pack);
}

export function useGame() {
  return useSyncExternalStore(client.subscribe, () => client.game);
}

export function useConnection() {
  const state = useSyncExternalStore(client.subscribe, () => client.state);
  const detail = useSyncExternalStore(client.subscribe, () => client.detail);
  return { state, detail };
}

/** Calls `listener` for every tick, in order. The building block for trackers (docs/gadgets.md#trackers). */
export function useOnFrame(listener: FrameListener) {
  const latest = useRef(listener);
  latest.current = listener;
  useEffect(() => client.onFrame((...args) => latest.current(...args)), []);
}
