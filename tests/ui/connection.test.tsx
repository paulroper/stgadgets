import { act, screen } from "@testing-library/react";
import { afterEach, describe, expect, it, vi } from "vitest";
import { root, startApp } from "./fakeServer";

afterEach(() => {
  vi.useRealTimers();
});

describe("connecting to the server", () => {
  it("connects to the server's WebSocket on the page's host", async () => {
    const server = await startApp();

    expect(server.socket.url).toBe(`ws://${location.host}/ws`);
  });

  it("says it's connecting until the server replies", async () => {
    await startApp();

    expect(root().textContent).toBe("connecting");
  });

  it("shows why there's nothing to display", async () => {
    const server = await startApp();

    server.status("no-pack", "No pack for 'ddonpach'");

    expect(root().textContent).toBe("No pack for 'ddonpach'");
  });

  it("shows the state when the server gives no detail", async () => {
    const server = await startApp();

    server.status("starting");

    expect(root().textContent).toBe("starting");
  });

  it("shows the layout once a game is matched", async () => {
    const server = await startApp();

    server.startGame("no-such-pack", { "p1.score": 150 });

    expect(screen.getByText("1UP")).toBeDefined();
    expect(root().querySelector(".status")).toBeNull();
  });

  it("goes back to a status message when the game is unloaded", async () => {
    const server = await startApp();
    server.startGame("no-such-pack", { "p1.score": 150 });

    server.status("no-content", "No game loaded");

    expect(root().textContent).toBe("No game loaded");
  });
});

describe("losing the server", () => {
  it("says the server can't be reached", async () => {
    const server = await startApp();
    server.startGame("no-such-pack", { "p1.score": 150 });

    server.disconnect();

    expect(root().textContent).toBe("Can't reach the stgadgets server");
  });

  it("reconnects after a second and shows the layout again", async () => {
    const server = await startApp();
    server.startGame("no-such-pack", { "p1.score": 150 });
    vi.useFakeTimers({ toFake: ["setTimeout"] });

    server.disconnect();
    act(() => vi.advanceTimersByTime(999));
    expect(server.sockets).toHaveLength(1);
    act(() => vi.advanceTimersByTime(1));
    expect(server.sockets).toHaveLength(2);
    expect(server.socket.url).toBe(server.sockets[0].url);

    server.startGame("no-such-pack", { "p1.score": 300 });
    expect(screen.getByText("0300")).toBeDefined();
  });
});

describe("where the page runs", () => {
  it("draws no page background in the overlay window", async () => {
    await startApp("/?overlay");

    expect(document.documentElement.classList.contains("overlay")).toBe(true);
  });

  it("draws the page background in a browser tab", async () => {
    await startApp("/");

    expect(document.documentElement.classList.contains("overlay")).toBe(false);
  });
});
