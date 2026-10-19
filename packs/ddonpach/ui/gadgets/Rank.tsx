import { useVar } from "@stgadgets/sdk";
import "./Rank.css";

const RANK_MAX = 63;
const CHANGE_TICKS = 180; // how long the latest change stays highlighted

// The parts of rank (derived.js), in stacking order.
const PARTS = [
  { name: "rank.from_stage", label: "stage" },
  { name: "rank.from_loop", label: "loop" },
  { name: "rank.from_ships", label: "ship & power" },
  { name: "rank.from_lives", label: "lives" },
  { name: "rank.from_bees", label: "bees" },
  { name: "rank.from_time", label: "survival" },
] as const;

function usePart(name: string) {
  return useVar<number | null>(name);
}

/** The game's rank (0-63), what it's made of, and what changed it last. */
export function Rank() {
  const rank = useVar<number | null>("rank");
  const model = useVar<number | null>("rank.model");
  const headingTo = useVar<number | null>("rank.heading_to");
  const running = useVar<boolean | null>("game.running");
  const change = useVar<number | null>("rank.change");
  const reason = useVar<string | null>("rank.change_reason");
  const age = useVar<number | null>("rank.change_age");
  const timePoint = useVar<number | null>("rank.time_point_in");
  const parts = PARTS.map((part) => ({ ...part, value: usePart(part.name) }));
  if (rank === undefined || model === undefined) return null;

  const shown = running ? rank : null;
  const recent = change != null && age != null && age < CHANGE_TICKS;
  const pending = shown !== null && headingTo != null;

  return (
    <div className="gadget rank">
      <div className="label">RANK</div>
      <div className="value">
        {shown ?? "-"}
        <span className="unit"> / {RANK_MAX}</span>
        {pending && <span className="rank-pending"> → {headingTo}</span>}
      </div>
      <div className="bar">
        {parts.map(({ name, value }) =>
          value ? <div key={name} className={`bar-fill ${name.replace(".", "-")}`} style={{ width: `${(value / RANK_MAX) * 100}%` }} /> : null,
        )}
      </div>
      <div className="note">
        {recent && (
          <span className={change > 0 ? "rank-change up" : "rank-change down"}>
            {change > 0 ? "+" : ""}
            {change} <span className="dim">{reason}</span>
          </span>
        )}
      </div>
      <div className="note">
        {timePoint != null && shown !== null && shown < RANK_MAX && (
          <>
            survival +1 in <span className="rank-timer-value">{Math.ceil(timePoint)} s</span>
          </>
        )}
      </div>
      <ul className="rank-parts">
        {parts.map(({ name, label, value }) => (
          <li key={name} className={value ? "" : "rank-part-none"}>
            <span className={`rank-key ${name.replace(".", "-")}`} />
            {label}
            <span className="rank-part-value">{model === null || value == null ? "-" : value}</span>
          </li>
        ))}
      </ul>
    </div>
  );
}
