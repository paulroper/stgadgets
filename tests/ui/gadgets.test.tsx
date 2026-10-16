import { render } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import { startApp } from "./fakeServer";

// Imported after startApp, so gadgets share the fresh app's connection.
const gadgets = () => import("../../src/ui/src/gadgets");

describe("Score", () => {
  it("shows points just scored beside the score while the gain variable has a value", async () => {
    const server = await startApp();
    server.startGame("no-such-pack", { "p1.score": 720, "p1.score_popup": null });
    const { Score } = await gadgets();
    const { container } = render(<Score name="p1.score" label="1UP" gain="p1.score_popup" />);
    const value = () => container.querySelector(".value")?.textContent;

    expect(value()).toBe("0720");

    server.tick({ "p1.score": 820, "p1.score_popup": 100 });
    expect(value()).toBe("0820+100");

    server.tick({ "p1.score_popup": null });
    expect(value()).toBe("0820");
  });

  it("ignores a gain variable the game doesn't define", async () => {
    const server = await startApp();
    server.startGame("no-such-pack", { "p1.score": 50 });

    const { Score } = await gadgets();
    const { container } = render(<Score name="p1.score" label="1UP" gain="p1.score_popup" />);

    expect(container.querySelector(".value")?.textContent).toBe("0050");
  });
});
