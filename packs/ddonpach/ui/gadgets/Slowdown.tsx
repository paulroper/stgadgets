import { useVar } from "@stgadgets/sdk";
import "./Slowdown.css";

/** How fast the game itself is running: below 100% when its main loop misses frames (slowdown). */
export function Slowdown() {
  const speed = useVar<number | null>("game.speed");
  const paused = useVar<boolean | null>("game.paused");
  if (speed === undefined) return null;

  const percent = speed === null ? null : Math.min(Math.round(speed * 100), 100);
  const level = percent === null ? "" : percent >= 95 ? "full" : percent >= 75 ? "slow" : "very-slow";

  return (
    <div className={`gadget slowdown slowdown-${level}`}>
      <div className="label">GAME SPEED</div>
      <div className="value small">{paused ? "PAUSED" : percent === null ? "-" : `${percent}%`}</div>
      <div className="bar">
        <div className="bar-fill" style={{ width: `${percent ?? 0}%` }} />
      </div>
    </div>
  );
}
