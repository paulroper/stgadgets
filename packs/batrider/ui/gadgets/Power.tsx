import { useVar } from "@stgadgets/sdk";
import "./Power.css";

const SHOT_MAX = 3; // 0x2FCA2

/** Player 1's shot level (0-3) with the small shot items still needed, and the option level. */
export function Power() {
  const shot = useVar<number | null>("p1.shot_level");
  const needed = useVar<number | null>("p1.shot_items_needed");
  const option = useVar<number | null>("p1.option_level");
  const running = useVar<boolean | null>("game.running");
  if (shot === undefined || option === undefined) return null;

  const shotShown = running ? shot : null;
  const optionShown = running ? option : null;

  return (
    <div className="gadget">
      <div className="label">POWER</div>
      <div className="power-row">
        <span className="power-name">SHOT</span>
        <span className="pips">
          {Array.from({ length: SHOT_MAX }, (_, i) => (
            <span key={i} className={shotShown !== null && i < shotShown ? "power-pip on" : "power-pip"} />
          ))}
        </span>
        <span className="power-level">
          {shotShown === null ? "-" : shotShown === SHOT_MAX ? "MAX" : `${needed ?? "-"} small to go`}
        </span>
      </div>
      <div className="power-row">
        <span className="power-name">OPTION</span>
        <span className="power-level">{optionShown === null ? "-" : `Lv ${optionShown}`}</span>
      </div>
    </div>
  );
}
