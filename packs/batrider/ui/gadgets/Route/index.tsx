import { useVar } from "@stgadgets/sdk";
import "./index.css";

const ROWS = [1, 2, 3, 4, 5, 6, 7] as const; // the advanced course's 7 stages, the longest

type StageState = "none" | "done" | "current" | "next" | "ahead";

const MARKS: Record<StageState, string> = { none: "", done: "✓", current: "◀", next: "NEXT", ahead: "" };

/** Where stage `n` stands on the run. `none` is a row past the course's last stage. */
function stageState(
  n: number,
  name: string | null | undefined,
  at: number | null | undefined,
  done: number | null | undefined,
): StageState {
  if (name == null) return "none";
  if (n === at) return "current";
  if (done == null) return "ahead";
  if (n <= done) return "done";
  const next = done + 1 + (at != null ? 1 : 0);
  if (n === next) return "next";
  return "ahead";
}

/** The course's stages: done, the one being played, and the next. A hidden boss's stage shows as an extra line. */
export function Route() {
  const at = useVar<number | null>("route.at");
  const done = useVar<number | null>("route.done");
  const extra = useVar<string | null>("route.extra");
  const names = {
    1: useVar<string | null>("route.1"),
    2: useVar<string | null>("route.2"),
    3: useVar<string | null>("route.3"),
    4: useVar<string | null>("route.4"),
    5: useVar<string | null>("route.5"),
    6: useVar<string | null>("route.6"),
    7: useVar<string | null>("route.7"),
  };
  if (names[1] === undefined) return null;

  return (
    <div className="gadget route">
      <div className="label">ROUTE</div>
      <ol className="route-stages">
        {ROWS.map(n => {
          const name = names[n];
          const state = stageState(n, name, at, done);
          return (
            <li key={n} className={`route-${state}`}>
              <span className="route-n">{n}</span>
              <span className="route-name">{name ?? "-"}</span>
              <span className="route-state">{MARKS[state]}</span>
            </li>
          );
        })}
      </ol>
      <div className="note route-extra">{extra ? `◀ EXTRA STAGE: ${extra}` : ""}</div>
    </div>
  );
}
