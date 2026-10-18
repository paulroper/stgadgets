import { useVar } from "@stgadgets/sdk";
import "./Bees.css";

const STAGES = [1, 2, 3, 4, 5, 6, 7];

function useStageBees() {
  return {
    1: useVar<number | null>("bees.stage1"),
    2: useVar<number | null>("bees.stage2"),
    3: useVar<number | null>("bees.stage3"),
    4: useVar<number | null>("bees.stage4"),
    5: useVar<number | null>("bees.stage5"),
    6: useVar<number | null>("bees.stage6"),
    7: useVar<number | null>("bees.stage7"),
  } as Record<number, number | null | undefined>;
}

/** Bees this stage out of 13, what the next is worth, and each stage's count this loop. */
export function Bees() {
  const collected = useVar<number | null>("bees.collected");
  const total = useVar<number | null>("bees.total");
  const next = useVar<number | null>("bees.next_value");
  const possible = useVar<boolean | null>("bees.perfect_possible");
  const perfect = useVar<number | null>("bees.perfect");
  const stage = useVar<number | null>("stage");
  const byStage = useStageBees();
  if (collected === undefined || total === undefined) return null;

  const of = total ?? 13;

  return (
    <div className="gadget bees">
      <div className="label"><span className="bees-icon" aria-hidden="true">🐝</span> BEES</div>
      <div className={collected !== null && collected >= of ? "value bees-perfect" : "value"}>
        {collected ?? "-"}
        <span className="unit"> / {of}</span>
      </div>
      <div className="pips">
        {Array.from({ length: of }, (_, i) => (
          <span key={i} className={collected !== null && i < collected ? "bee on" : "bee"} />
        ))}
      </div>
      {collected !== null && (
        <div className="note">
          {collected >= of ? "PERFECT!" : possible === false ? "no perfect this stage" : next != null ? `next +${next.toLocaleString("en")}` : ""}
        </div>
      )}
      <div className="bees-stages">
        {STAGES.map((n) => {
          const count = byStage[n];
          if (count == null && n === 7) return null;
          return (
            <span
              key={n}
              className={["bees-stage", count != null && count >= of ? "perfect" : "", n === stage ? "current" : ""].join(" ").trim()}
              title={`Stage ${n}`}
            >
              <span className="bees-stage-n">{n}</span>
              <span className="bees-stage-count">{count ?? "·"}</span>
            </span>
          );
        })}
      </div>
      {perfect != null && <div className="note">perfect stages {perfect}</div>}
    </div>
  );
}
