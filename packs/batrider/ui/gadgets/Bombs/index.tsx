import { useVar } from "@stgadgets/sdk";
import "./index.css";

const BOMBS_MAX = 4; // 0x2FD48
const FRAGMENTS_PER_BOMB = 40;

function fragmentsNote(shown: number | null, fragments: number | null, full: boolean) {
  if (shown === null || fragments === null) return "";
  if (full) return "FULL: fragments score 1,000";
  return `${fragments} / ${FRAGMENTS_PER_BOMB} fragments`;
}

/** Player 1's full bombs, and the fragments towards the next one. */
export function Bombs() {
  const bombs = useVar<number | null>("p1.bombs");
  const fragments = useVar<number | null>("p1.bomb_fragments");
  const running = useVar<boolean | null>("game.running");
  if (bombs === undefined || fragments === undefined) return null;

  const shown = running ? bombs : null;
  const full = shown === BOMBS_MAX && fragments === FRAGMENTS_PER_BOMB;

  return (
    <div className={full ? "gadget bombs bombs-full" : "gadget bombs"}>
      <div className="label">
        <span aria-hidden="true">💣</span> BOMBS
      </div>
      <div className="value">
        {shown ?? "-"}
        <span className="pips bombs-pips">
          {Array.from({ length: BOMBS_MAX }, (_, i) => (
            <span key={i} className={shown !== null && i < shown ? "bomb on" : "bomb"} />
          ))}
        </span>
      </div>
      <div className="bar">
        <div
          className="bar-fill"
          style={{ width: `${shown === null || fragments === null ? 0 : (fragments / FRAGMENTS_PER_BOMB) * 100}%` }}
        />
      </div>
      <div className="note">{fragmentsNote(shown, fragments, full)}</div>
    </div>
  );
}
