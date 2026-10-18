import { useMemo } from "react";
import { useVar } from "@stgadgets/sdk";
import "./Splits.css";

const FRAMES_PER_SECOND = 15625 / 271.5; // the board's refresh rate

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
    <tr className={current ? "splits-current" : ""}>
      <td>{split.stage}</td>
      <td>{time(split.frames)}</td>
      <td>{split.points === null ? "-" : split.points.toLocaleString("en")}</td>
      <td>{split.deaths ?? "-"}</td>
      <td>{split.bombs ?? "-"}</td>
      <td>{slowdown(split)}</td>
    </tr>
  );
}

/** Player 1's stage splits this credit: time, points, deaths, bombs and slowdown, with the current stage live. */
export function Splits() {
  const stage = useVar<string | null>("split.stage");
  const done = useVar<string | null>("split.done");
  const frames = useVar<number | null>("split.frames");
  const gameFrames = useVar<number | null>("split.game_frames");
  const points = useVar<number | null>("split.points");
  const deaths = useVar<number | null>("split.deaths");
  const bombs = useVar<number | null>("split.bombs");
  const finished = useMemo(() => parse(done), [done]);
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
          {finished.map((split) => (
            <Row key={split.stage} split={split} />
          ))}
          {current && <Row split={current} current />}
        </tbody>
      </table>
      {!current && finished.length === 0 && <div className="note">starts with stage 1</div>}
    </div>
  );
}
