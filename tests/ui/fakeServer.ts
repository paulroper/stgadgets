// Starts the real UI (src/ui/src/main.tsx) against a fake stgadgets server, so tests drive it over the wire protocol
// (docs/gadgets.md#wire-protocol-internal) the same way the server does.

import { act } from "@testing-library/react";
import { vi } from "vitest";

type Values = Record<string, number | boolean | string | null>;

/** Stands in for the browser's WebSocket; the UI opens one per connection attempt. */
class FakeSocket {
  static readonly OPEN = 1;
  readyState = FakeSocket.OPEN;
  onmessage: ((event: { data: string }) => void) | null = null;
  onclose: (() => void) | null = null;
  /** Messages the UI sent to the server. */
  sent: unknown[] = [];

  constructor(readonly url: string) {
    sockets.push(this);
  }

  send(data: string) {
    this.sent.push(JSON.parse(data));
  }
}

let sockets: FakeSocket[] = [];

export class FakeServer {
  private seq = 0;

  /** Every connection the UI has opened, oldest first. */
  get sockets(): readonly FakeSocket[] {
    return sockets;
  }

  /** The UI's current connection. */
  get socket() {
    return sockets[sockets.length - 1];
  }

  send(message: { type: string } & Record<string, unknown>) {
    act(() => this.socket.onmessage!({ data: JSON.stringify(message) }));
  }

  status(state: string, detail?: string) {
    this.send({ type: "status", state, detail: detail ?? null });
  }

  hello(pack: string) {
    const game = { title: `Test game (${pack})`, manufacturer: null, year: null, romset: pack, screen: null };
    this.send({ type: "hello", pack, game, variables: {}, values: {} });
  }

  tick(changed: Values) {
    this.send({ type: "tick", seq: ++this.seq, readAt: Date.now(), sentAt: Date.now(), changed });
  }

  /** What the server does when a game is matched: hello, then "connected", then the first tick's values. */
  startGame(pack: string, values: Values = {}) {
    this.hello(pack);
    this.status("connected");
    this.tick(values);
  }

  /** The server goes away. */
  disconnect() {
    this.socket.readyState = 3; // CLOSED
    act(() => this.socket.onclose!());
  }
}

/**
 * Loads a fresh copy of the UI, as a page load would.
 * @param url the page URL, e.g. "/?overlay"
 */
export async function startApp(url = "/") {
  sockets = [];
  vi.stubGlobal("WebSocket", FakeSocket);
  vi.resetModules(); // a fresh client for every test

  history.replaceState(null, "", url);
  document.documentElement.className = "";
  document.body.innerHTML = `<div id="root"></div>`;

  await act(() => import("../../src/ui/src/main"));
  return new FakeServer();
}

/** The page as the user sees it. */
export function root() {
  return document.getElementById("root")!;
}
