import { PackLayout } from "./layouts/PackLayout";
import { useConnection, useGame, usePack } from "./sdk/react";

/** The matched pack's layout once connected, or what the connection is doing until then. */
export function App() {
  const { state, detail } = useConnection();
  const game = useGame();
  const pack = usePack();

  if (state !== "connected" || !game) {
    return <div className="status">{detail ?? state}</div>;
  }
  return <PackLayout pack={pack} />;
}
