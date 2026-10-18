import { useVar } from "@stgadgets/sdk";
import "./Boss.css";

/** How far the stage has scrolled towards its boss, and roughly how long until it arrives. */
export function Boss() {
  const progress = useVar<number | null>("boss.progress");
  const eta = useVar<number | null>("boss.eta");
  const stage = useVar<string | null>("split.stage");
  if (progress === undefined) return null;

  const reached = progress !== null && progress >= 1;

  return (
    <div className={reached ? "gadget boss boss-here" : "gadget boss"}>
      <div className="label">BOSS{stage ? ` · ${stage}` : ""}</div>
      <div className="value small">
        {progress === null ? "-" : reached ? "HERE!" : eta != null ? `in ~${Math.ceil(eta)} s` : `${Math.round(progress * 100)}%`}
      </div>
      <div className="boss-bar">
        <div className="boss-bar-fill" style={{ width: `${(progress ?? 0) * 100}%` }} />
        <span className="boss-marker" />
      </div>
    </div>
  );
}
