import { useVar } from "@stgadgets/sdk";
import "./index.css";

const STEPS = 19; // 100-900, then 1,000-10,000
const TOP = 10000;

function chainNote(next: number | null, step: number | null) {
  if (next === null) return "";
  if (next === TOP) return "MAX: keep the chain!";
  return `step ${step} of ${STEPS}`;
}

/** What the next medal released is worth, and how far up the chain that is. */
export function Medal() {
  const next = useVar<number | null>("medal.next");
  const value = useVar<number | null>("medal.value");
  if (next === undefined) return null;

  const step = next === null || value === null || value === undefined ? null : Math.min(value / 2 + 1, STEPS);

  return (
    <div className={next === TOP ? "gadget medal medal-max" : "gadget medal"}>
      <div className="label">
        <span aria-hidden="true">🏅</span> NEXT MEDAL
      </div>
      <div className="value">{next === null ? "-" : next.toLocaleString("en")}</div>
      <div className="bar">
        <div className="bar-fill" style={{ width: `${step === null ? 0 : (step / STEPS) * 100}%` }} />
      </div>
      <div className="note">{chainNote(next, step)}</div>
    </div>
  );
}
