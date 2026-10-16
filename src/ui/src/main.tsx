import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import { InvadersClassic } from "./layouts/InvadersClassic";
import { client, source } from "./sdk/client";
import { useConnection, useGame } from "./sdk/react";
import "./styles.css";

client.connect(`ws://${location.host}/ws`);

// "?overlay": running inside the transparent overlay window, so draw no page background.
if (source === "overlay") {
  document.documentElement.classList.add("overlay");
}

function App() {
  const { state, detail } = useConnection();
  const game = useGame();

  if (state !== "connected" || !game) {
    return <div className="status">{detail ?? state}</div>;
  }
  return <InvadersClassic />;
}

createRoot(document.getElementById("root")!).render(
  <StrictMode>
    <App />
  </StrictMode>,
);
