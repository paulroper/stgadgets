import { useVar } from "@stgadgets/sdk";
import "./SecondLoop.css";

const SCORE_NEEDED = 50_000_000;
const PERFECT_NEEDED = 4;
const DEATHS_ALLOWED = 2;

function Condition({ met, label, progress }: { met: boolean | null | undefined; label: string; progress: string }) {
  return (
    <li className={met ? "loop2-met" : ""}>
      <span className="loop2-check">{met ? "✓" : "·"}</span>
      {label}
      <span className="loop2-progress">{progress}</span>
    </li>
  );
}

/** Whether clearing stage 6 leads to the second loop, and how each condition stands. */
export function SecondLoop() {
  const possible = useVar<boolean | null>("loop2.possible");
  const eligible = useVar<boolean | null>("loop2.eligible");
  const byDeaths = useVar<boolean | null>("loop2.by_deaths");
  const byHits = useVar<boolean | null>("loop2.by_hits");
  const byScore = useVar<boolean | null>("loop2.by_score");
  const byBees = useVar<boolean | null>("loop2.by_bees");
  const hitsNeeded = useVar<number | null>("loop2.hits_needed");
  const deaths = useVar<number | null>("p1.deaths_first_credit");
  const maxHits = useVar<number | null>("p1.max_hits");
  const score = useVar<number | null>("p1.score");
  const perfect = useVar<number | null>("bees.perfect");
  const loop = useVar<number | null>("loop");
  const running = useVar<boolean | null>("game.running");
  if (possible === undefined) return null;

  const headline =
    possible === null ? (running && loop ? "LOOP 2" : "-") : !eligible ? "NOT THIS CREDIT" : possible ? "ON TRACK" : "NEEDS ONE";

  return (
    <div className={`gadget loop2 ${possible ? "loop2-on" : eligible === false ? "loop2-off" : ""}`}>
      <div className="label">2ND LOOP</div>
      <div className="value loop2-headline">{headline}</div>
      <div className="note">{eligible === false && "needs one credit, solo"}</div>
      <ul className="loop2-conditions">
        <Condition met={eligible && byDeaths} label="deaths" progress={`${deaths ?? "-"} / ${DEATHS_ALLOWED}`} />
        <Condition met={eligible && byHits} label="max chain" progress={`${maxHits ?? "-"} / ${hitsNeeded ?? "-"}`} />
        <Condition met={eligible && byScore} label="score" progress={`${score == null ? "-" : (score / 1e6).toFixed(1)}M / ${SCORE_NEEDED / 1e6}M`} />
        <Condition met={eligible && byBees} label="perfect bees" progress={`${perfect ?? "-"} / ${PERFECT_NEEDED}`} />
      </ul>
    </div>
  );
}
