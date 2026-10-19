import { useMemo } from "react";
import { useVar } from "@stgadgets/sdk";
import "./Splits.css";

const FRAMES_PER_SECOND = 15625 / 271.5; // the board's refresh rate

// Every stage a credit can reach, so the table has a row for each from the start (split.stage in derived.js).
const STAGES = ["1-1", "1-2", "1-3", "1-4", "1-5", "1-6", "2-1", "2-2", "2-3", "2-4", "2-5", "2-6", "2-7"];

interface Split {
  stage: string;
  frames: number | null;
  game_frames: number | null;
  points: number | null;
  deaths: number | null;
  bombs: number | null;
}

function time(frames: number | null) {
  if (frames === null) return "-";
  const seconds = Math.floor(frames / FRAMES_PER_SECOND);
  return `${Math.floor(seconds / 60)}:${String(seconds % 60).padStart(2, "0")}`;
}

function slowdown({ frames, game_frames }: Split) {
  if (!frames || game_frames === null) return "-";
  return `${Math.round((1 - game_frames / frames) * 100)}%`;
}

function parse(json: string | null | undefined): Split[] {
  try {
    return json ? (JSON.parse(json) as Split[]) : [];
  } catch {
    return [];
  }
}

function Row({ split, current }: { split: Split; current?: boolean }) {
  return (
    <tr className={current ? "splits-current" : split.frames === null ? "splits-ahead" : ""}>
      <td>{split.stage}</td>
      <td>{time(split.frames)}</td>
      <td>{split.points === null ? "-" : split.points.toLocaleString("en")}</td>
      <td>{split.deaths ?? "-"}</td>
      <td>{split.bombs ?? "-"}</td>
      <td>{slowdown(split)}</td>
    </tr>
  );
}

const ahead = (stage: string): Split => ({ stage, frames: null, game_frames: null, points: null, deaths: null, bombs: null });

/** Player 1's stage splits this credit: time, points, deaths, bombs and slowdown, with the current stage live. */
export function Splits() {
  const stage = useVar<string | null>("split.stage");
  const done = useVar<string | null>("split.done");
  const frames = useVar<number | null>("split.frames");
  const gameFrames = useVar<number | null>("split.game_frames");
  const points = useVar<number | null>("split.points");
  const deaths = useVar<number | null>("split.deaths");
  const bombs = useVar<number | null>("split.bombs");
  const finished = useMemo(() => new Map(parse(done).map((split) => [split.stage, split])), [done]);
  if (stage === undefined || done === undefined) return null;

  const current: Split | null =
    stage === null ? null : { stage, frames: frames ?? null, game_frames: gameFrames ?? null, points: points ?? null, deaths: deaths ?? null, bombs: bombs ?? null };

  return (
    <div className="gadget splits">
      <div className="label">SPLITS</div>
      <table className="splits-table">
        <thead>
          <tr className="dim">
            <th>STG</th>
            <th>TIME</th>
            <th>POINTS</th>
            <th>✕</th>
            <th>B</th>
            <th>SLOW</th>
          </tr>
        </thead>
        <tbody>
          {STAGES.map((name) =>
            current?.stage === name ? (
              <Row key={name} split={current} current />
            ) : (
              <Row key={name} split={finished.get(name) ?? ahead(name)} />
            ),
          )}
        </tbody>
      </table>
    </div>
  );
}
