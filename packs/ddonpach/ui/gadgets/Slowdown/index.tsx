import { useVar } from "@stgadgets/sdk";
import "./index.css";

function speedLevel(percent: number | null) {
  if (percent === null) return "";
  if (percent >= 95) return "full";
  if (percent >= 75) return "slow";
  return "very-slow";
}

function speedText(percent: number | null, paused: boolean | null | undefined) {
  if (paused) return "PAUSED";
  if (percent === null) return "-";
  return `${percent}%`;
}

/** How fast the game itself is running: below 100% when its main loop misses frames (slowdown). */
export function Slowdown() {
  const speed = useVar<number | null>("game.speed");
  const paused = useVar<boolean | null>("game.paused");
  if (speed === undefined) return null;

  const percent = speed === null ? null : Math.min(Math.round(speed * 100), 100);
  const level = speedLevel(percent);

  return (
    <div className={`gadget slowdown slowdown-${level}`}>
      <div className="label">GAME SPEED</div>
      <div className="value small">{speedText(percent, paused)}</div>
      <div className="bar">
        <div className="bar-fill" style={{ width: `${percent ?? 0}%` }} />
      </div>
    </div>
  );
}
