import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { PackLayout } from "./layouts/PackLayout";
import { client, source } from "./sdk/client";
import { useConnection, useGame, usePack } from "./sdk/react";
import "./styles.css";

// "?state=<pack>/<name>", in development only: show one of a pack's game states (packs/<id>/tests/states.ts) instead
// of connecting to the server.
const state = new URLSearchParams(location.search).get("state");
if (import.meta.env.DEV && state) {
  void import("./devStates").then(({ showGameState }) => showGameState(state));
} else {
  client.connect(`ws://${location.host}/ws`);
}

// "?overlay": running inside the transparent overlay window, so draw no page background.
if (source === "overlay") {
  document.documentElement.classList.add("overlay");
}

function App() {
  const { state, detail } = useConnection();
  const game = useGame();
  const pack = usePack();

  if (state !== "connected" || !game) {
    return <div className="status">{detail ?? state}</div>;
  }
  return <PackLayout pack={pack} />;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
