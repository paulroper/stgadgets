import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { App } from "./App";
import { client, source } from "./sdk/client";
import "./styles.css";

// "?state=<pack>/<name>", in development only: show one of a pack's game states (packs/<id>/tests/states.ts) instead
// of connecting to the server.
const devState = new URLSearchParams(location.search).get("state");
if (import.meta.env.DEV && devState) {
  void import("./devStates").then(({ showGameState }) => showGameState(devState));
} else {
  client.connect(`ws://${location.host}/ws`);
}

// "?overlay": running inside the transparent overlay window, so draw no page background.
if (source === "overlay") {
  document.documentElement.classList.add("overlay");
}

/** Exported so tests, which load a fresh copy of the app for each test, can unmount the last one. */
export const appRoot = createRoot(document.getElementById("root")!);

appRoot.render(
  <StrictMode>
    <App />
  </StrictMode>,
);
