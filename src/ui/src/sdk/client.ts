// Connection to the stgadgets server. See docs/gadgets.md#wire-protocol-internal.

import { LatencyTracker } from "./latency";

export type Scalar = number | boolean | string | null; // strings come from derived values
/** An array variable: one entry per element of a table in memory, each its fields by name (docs/memory-maps.md#arrays). */
export type ArrayEntries = readonly Readonly<Record<string, number | boolean | null>>[];
export type Value = Scalar | ArrayEntries;
export type Vars = Readonly<Record<string, Value>>;

export type ConnectionState =
  | "connecting"
  | "starting"
  | "connected"
  | "no-emulator"
  | "no-content"
  | "no-pack"
  | "disconnected";

export interface GameInfo {
  title: string;
  manufacturer: string | null;
  year: number | null;
  romset: string | null;
  screen: { width: number; height: number; rotation: number } | null;
}

/** Timing for one tick, in Unix ms. */
export interface TickInfo {
  seq: number;
  /** When the server received the memory from the emulator. */
  readAt: number;
  /** When the browser received the tick. */
  receivedAt: number;
}

export type FrameListener = (vars: Vars, prev: Vars, info: TickInfo) => void;

/** What the server sends. */
export type ServerMessage =
  | { type: "status"; state: ConnectionState; detail?: string | null }
  | { type: "hello"; pack: string; game: GameInfo; values: Record<string, Value> }
  | { type: "tick"; seq: number; readAt: number; changed: Record<string, Value> };

const RECONNECT_DELAY_MS = 1000;

export class StgadgetsClient {
  vars: Vars = {};
  /** The ID of the game pack the server matched, e.g. "invaders". */
  pack: string | null = null;
  game: GameInfo | null = null;
  state: ConnectionState = "connecting";
  detail: string | null = null;

  private listeners = new Set<() => void>();
  private frameListeners = new Set<FrameListener>();
  private socket: WebSocket | null = null;

  connect(url: string) {
    const socket = new WebSocket(url);
    this.socket = socket;
    socket.onmessage = event => this.receive(JSON.parse(event.data) as ServerMessage);
    socket.onclose = () => {
      this.update({ state: "disconnected", detail: "Can't reach the stgadgets server" });
      setTimeout(() => this.connect(url), RECONNECT_DELAY_MS);
    };
  }

  /** Called after any change to vars, pack, game or state. */
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => void this.listeners.delete(listener);
  };

  /** Sends a message to the server (diagnostics only; the protocol is otherwise server → client). */
  send(message: { type: string } & Record<string, unknown>) {
    if (this.socket?.readyState === WebSocket.OPEN) this.socket.send(JSON.stringify(message));
  }

  /** Called for every tick, in order, before re-rendering. */
  onFrame(listener: FrameListener) {
    this.frameListeners.add(listener);
    return () => void this.frameListeners.delete(listener);
  }

  /** Handles a message from the server. Also called directly to show a game state without one (src/devStates.ts). */
  receive(message: ServerMessage) {
    switch (message.type) {
      case "status":
        this.update({ state: message.state, detail: message.detail ?? null });
        break;
      case "hello":
        this.update({ pack: message.pack, game: message.game, vars: message.values });
        break;
      case "tick": {
        const prev = this.vars;
        const vars = { ...prev, ...message.changed };
        const info = {
          seq: message.seq,
          readAt: message.readAt,
          receivedAt: performance.timeOrigin + performance.now(),
        };
        this.vars = vars;
        this.frameListeners.forEach(listener => listener(vars, prev, info));
        if (Object.keys(message.changed).length > 0) this.notify();
        break;
      }
    }
  }

  private update(changes: Partial<Pick<StgadgetsClient, "vars" | "pack" | "game" | "state" | "detail">>) {
    Object.assign(this, changes);
    this.notify();
  }

  private notify() {
    this.listeners.forEach(listener => listener());
  }
}

export const client = new StgadgetsClient();

/** Where this page is running, for diagnostics: the overlay window or a normal browser tab. */
export const source = new URLSearchParams(location.search).has("overlay") ? "overlay" : "browser";

export const latency = new LatencyTracker(client, source);

// Plain global for code that isn't React (docs/gadgets.md#globals-for-code-that-isnt-react).
declare global {
  interface Window {
    stgadgets: { readonly vars: Vars; subscribe: (listener: () => void) => () => void };
  }
}
window.stgadgets = {
  get vars() {
    return client.vars;
  },
  subscribe: client.subscribe,
};
