import { useVar } from "@stgadgets/sdk";
import "./index.css";

const points = (n: number) => n.toLocaleString("en");

/** What clearing the stage would add right now: the no-miss bonus plus the stars collected. */
export function StageClear() {
  const total = useVar<number | null>("p1.stage_clear_bonus");
  const noMiss = useVar<number | null>("p1.no_miss_bonus");
  const stars = useVar<number | null>("p1.star_bonus");
  if (total === undefined) return null;

  return (
    <div className="gadget stage-clear">
      <div className="label">STAGE CLEAR</div>
      <div className="value small">{total === null ? "-" : `+${points(total)}`}</div>
      <div className="note">
        {total !== null && noMiss != null && stars != null && (
          <>
            {noMiss ? `no miss ${points(noMiss)}` : <span className="stage-clear-lost">no miss lost</span>}
            {" · "}stars {points(stars)}
          </>
        )}
      </div>
    </div>
  );
}
