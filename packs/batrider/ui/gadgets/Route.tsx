import { useVar } from "@stgadgets/sdk";
import "./Route.css";

const STAGES = [1, 2, 3, 4, 5] as const;

/** The stages ahead: Manhattan, the three chosen at the start of the run, then the Highway. */
export function Route() {
  const stage = useVar<number | null>("stage");
  const names = {
    1: useVar<string | null>("route.1"),
    2: useVar<string | null>("route.2"),
    3: useVar<string | null>("route.3"),
    4: useVar<string | null>("route.4"),
    5: useVar<string | null>("route.5"),
  };
  if (names[1] === undefined) return null;

  return (
    <div className="gadget route">
      <div className="label">ROUTE</div>
      <ol className="route-stages">
        {STAGES.map(n => {
          const state =
            stage == null ? "" : n < stage ? "done" : n === stage ? "current" : n === stage + 1 ? "next" : "";
          return (
            <li key={n} className={state}>
              <span className="route-n">{n}</span>
              <span className="route-name">{names[n] ?? "-"}</span>
              <span className="route-state">
                {state === "done" ? "✓" : state === "current" ? "◀" : state === "next" ? "NEXT" : ""}
              </span>
            </li>
          );
        })}
      </ol>
    </div>
  );
}
