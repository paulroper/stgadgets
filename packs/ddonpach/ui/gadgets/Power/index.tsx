import { useVar } from "@stgadgets/sdk";
import "./index.css";

const MAX_LEVEL = 4;

function levelText(level: number | null) {
  if (level === null) return "-";
  return level === MAX_LEVEL ? "MAX" : level;
}

function Level({ label, level, strong }: { label: string; level: number | null; strong: boolean }) {
  return (
    <div className={strong ? "power-row power-strong" : "power-row"}>
      <span className="power-name">{label}</span>
      <span className="pips">
        {Array.from({ length: MAX_LEVEL }, (_, i) => (
          <span key={i} className={level !== null && i < level ? "power-pip on" : "power-pip"} />
        ))}
      </span>
      <span className="power-level">{levelText(level)}</span>
    </div>
  );
}

/** Player 1's shot and laser power, 0-4 each, with the ship's style (S or L) marked. */
export function Power() {
  const shot = useVar<number | null>("p1.shot_level");
  const laser = useVar<number | null>("p1.laser_level");
  const style = useVar<number | null>("p1.style");
  if (shot === undefined || laser === undefined) return null;

  return (
    <div className="gadget">
      <div className="label">POWER</div>
      <Level label="SHOT" level={shot} strong={shot !== null && style === 0} />
      <Level label="LASER" level={laser} strong={laser !== null && style === 1} />
    </div>
  );
}
