import { useVar } from "@stgadgets/sdk";
import "./index.css";

// The march's slowest and fastest tempo, in frames per note (ROM table at 0x1A21).
const SLOWEST = 52;
const FASTEST = 5;

function speedupText(speedupIn: number | null, tempo: number | null) {
  if (speedupIn !== null) return `${speedupIn} ${speedupIn === 1 ? "KILL" : "KILLS"}`;
  return tempo !== null ? "MAX" : "-";
}

/** The fleet's march: how fast it is now, and how many kills until it speeds up. Values come from derived.js. */
export function March() {
  const tempo = useVar<number | null>("fleet.tempo");
  const speedupIn = useVar<number | null>("fleet.speedup_in");
  const gameDelay = useVar<number | null>("fleet.sound_delay");
  if (tempo === undefined || speedupIn === undefined) return null;

  const speed = tempo === null ? 0 : (SLOWEST - tempo) / (SLOWEST - FASTEST);

  return (
    <>
      <div className="gadget">
        <div className="label">MARCH TEMPO</div>
        <div className="value">
          {tempo ?? "-"}
          <span className="unit"> f/note</span>
        </div>
        <div className="bar">
          <div className="bar-fill march-fill" style={{ width: `${Math.max(speed, 0.03) * 100}%` }} />
        </div>
        <div className="note">{gameDelay != null && gameDelay !== tempo && `game catching up: ${gameDelay}`}</div>
      </div>
      <div className="gadget">
        <div className="label">SPEED-UP IN</div>
        <div className="value">{speedupText(speedupIn, tempo)}</div>
        <div className="pips march-pips">
          {Array.from({ length: speedupIn ?? 0 }, (_, i) => (
            <span key={i} className={i === 0 ? "march-pip next" : "march-pip"} />
          ))}
        </div>
      </div>
    </>
  );
}
