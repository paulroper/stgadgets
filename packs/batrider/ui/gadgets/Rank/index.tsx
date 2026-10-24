import { useVar } from "@stgadgets/sdk";
import "./index.css";

const RANK_SCALE = 0x1000000; // rank units in the whole scale (derived.js)
const CHANGE_TICKS = 180; // rank.change_age stops counting here

// What has made rank harder (or easier) this game (derived.js), in the order they're listed.
const PARTS = [
  { name: "rank.from_time", label: "time" },
  { name: "rank.from_shots", label: "shots & graze" },
  { name: "rank.from_items", label: "items" },
  { name: "rank.from_extends", label: "extends" },
  { name: "rank.from_deaths", label: "deaths" },
] as const;

/** Rank units as a signed percentage of the scale; changes too small for two decimals still show which way they went. */
function percent(units: number) {
  const value = (units / RANK_SCALE) * 100;
  if (units !== 0 && Math.abs(value) < 0.005) return units > 0 ? "+<0.01%" : "-<0.01%";
  return `${value > 0 ? "+" : ""}${value.toFixed(2)}%`;
}

/** Which way a part has moved rank: up is harder. */
function partDirection(value: number | null | undefined) {
  if (value == null || value === 0) return "";
  return value > 0 ? "up" : "down";
}

function usePart(name: string) {
  return useVar<number | null>(name);
}

/** Rank as a percentage (0 easiest, 100 hardest), what has moved it this game, and the latest change with a cause. */
export function Rank() {
  const level = useVar<number | null>("rank.percent");
  const drift = useVar<number | null>("rank.drift");
  const change = useVar<number | null>("rank.change");
  const reason = useVar<string | null>("rank.change_reason");
  const age = useVar<number | null>("rank.change_age");
  // PARTS never changes, so the hooks run in the same order every render.
  // oxlint-disable-next-line react/hooks
  const parts = PARTS.map(part => ({ ...part, value: usePart(part.name) }));
  if (level === undefined) return null;

  const recent = change != null && age != null && age < CHANGE_TICKS;

  return (
    <div className="gadget rank">
      <div className="label">RANK</div>
      <div className="value">
        {level === null ? "-" : level.toFixed(2)}
        <span className="unit"> %</span>
      </div>
      <div className="bar">
        <div className="bar-fill" style={{ width: `${level ?? 0}%` }} />
      </div>
      <div className="note">
        {recent && (
          <span className={change > 0 ? "rank-change up" : "rank-change down"}>
            {percent(change)} <span className="dim">{reason}</span>
          </span>
        )}
      </div>
      <div className="note">{drift != null && `+${drift} a frame from time and credits`}</div>
      <ul className="rank-parts">
        {parts.map(({ name, label, value }) => (
          <li key={name} className={value ? "" : "rank-part-none"}>
            <span className={`rank-key ${name.replace(".", "-")}`} />
            {label}
            <span className={`rank-part-value ${partDirection(value)}`.trim()}>
              {value == null ? "-" : percent(value)}
            </span>
          </li>
        ))}
      </ul>
    </div>
  );
}
