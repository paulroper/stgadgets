import { useVar } from "@stgadgets/sdk";
import "./Saucer.css";

const FRAMES_PER_SECOND = 60;
const TIMER_RELOAD = 0x600; // frames between saucers (code at 0x0921)

type Status = "flying" | "due" | "counting" | "paused" | "none";

const headline: Record<Status, string> = {
  flying: "FLYING!",
  due: "ANY MOMENT",
  counting: "",
  paused: "",
  none: "NO MORE",
};

const note: Record<Status, string> = {
  flying: "",
  due: "waiting for the squiggly shot",
  counting: "",
  paused: "paused until the rack drops",
  none: "under 8 invaders this wave",
};

/** When the mystery ship comes, and what hitting it is worth. Values come from derived.js. */
export function Saucer() {
  const status = useVar<Status | null>("saucer.status");
  const appearsIn = useVar<number | null>("saucer.appears_in");
  const nextScore = useVar<number | null>("saucer.next_score");
  const shotsTo300 = useVar<number | null>("saucer.shots_to_300");
  if (status === undefined || status === null) return null;

  const counting = (status === "counting" || status === "paused") && appearsIn != null;

  return (
    <>
      <div className={`gadget saucer saucer-${status}`}>
        <div className="label">MYSTERY SHIP</div>
        <div className="value saucer-headline">
          {counting ? (
            <>
              {(appearsIn / FRAMES_PER_SECOND).toFixed(1)}
              <span className="unit"> s</span>
            </>
          ) : (
            headline[status]
          )}
        </div>
        {counting && (
          <div className="bar">
            <div className="bar-fill" style={{ width: `${Math.min(appearsIn / TIMER_RELOAD, 1) * 100}%` }} />
          </div>
        )}
        {note[status] && <div className="note">{note[status]}</div>}
      </div>
      {nextScore != null && shotsTo300 != null && (
        <div className="gadget">
          <div className="label">NEXT HIT</div>
          <div className={nextScore === 300 ? "value saucer-300" : "value"}>{nextScore}</div>
          <div className="note">{shotsTo300 === 1 ? "300 on this shot!" : `300 on shot ${shotsTo300}`}</div>
          <div className="pips saucer-shots">
            {Array.from({ length: shotsTo300 }, (_, i) => (
              <span key={i} className={i === shotsTo300 - 1 ? "saucer-shot jackpot" : "saucer-shot"} />
            ))}
          </div>
        </div>
      )}
    </>
  );
}
