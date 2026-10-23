import { useVar } from "@stgadgets/sdk";
import "./Route.css";

const ROWS = [1, 2, 3, 4, 5, 6, 7] as const; // the advanced course's 7 stages, the longest

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

  const next = done == null ? null : done + 1 + (at != null ? 1 : 0);

  return (
    <div className="gadget route">
      <div className="label">ROUTE</div>
      <ol className="route-stages">
        {ROWS.map(n => {
          const name = names[n];
          const state =
            name == null
              ? "route-none"
              : n === at
                ? "current"
                : done != null && n <= done
                  ? "done"
                  : n === next
                    ? "next"
                    : "";
          return (
            <li key={n} className={state}>
              <span className="route-n">{n}</span>
              <span className="route-name">{name ?? "-"}</span>
              <span className="route-state">
                {state === "done" ? "✓" : state === "current" ? "◀" : state === "next" ? "NEXT" : ""}
              </span>
            </li>
          );
        })}
      </ol>
      <div className="note route-extra">{extra ? `◀ EXTRA STAGE: ${extra}` : ""}</div>
    </div>
  );
}
