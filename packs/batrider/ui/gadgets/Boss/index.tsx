import { useVar } from "@stgadgets/sdk";
import "./index.css";

type BossState = "here" | "approaching" | "unknown";

function bossState(hp: number | null, name: string | null | undefined, progress: number | null | undefined): BossState {
  if (hp !== null && name != null) return "here";
  if (progress != null) return "approaching";
  return "unknown";
}

/** How long until the boss arrives, or how far there, when the scroll isn't moving. */
function approachText(progress: number, eta: number | null | undefined) {
  return eta != null ? `in ~${Math.ceil(eta)} s` : `${Math.round(progress * 100)}%`;
}

/** How full the bar is, in %: the boss part's HP once it's here, the stage's progress towards it before. */
function barWidth(
  state: BossState,
  hp: number | null,
  max: number | null | undefined,
  progress: number | null | undefined,
) {
  if (state === "here") return max ? ((hp ?? 0) / max) * 100 : 0;
  if (state === "approaching") return (progress ?? 0) * 100;
  return 0;
}

/**
 * The boss: before it arrives, how far the stage has scrolled towards it and roughly how long until it does; once it's
 * on screen, its name and the HP left in the part that's being worn down.
 */
export function Boss() {
  const name = useVar<string | null>("boss.name");
  const hp = useVar<number | null>("boss.hp");
  const max = useVar<number | null>("boss.hp_max");
  const progress = useVar<number | null>("boss.progress");
  const eta = useVar<number | null>("boss.eta");
  if (hp === undefined) return null;

  const state = bossState(hp, name, progress);

  return (
    <div className={`gadget boss boss-${state}`}>
      <div className="label">BOSS{state === "here" ? ` · ${name}` : ""}</div>
      <div className="value">
        {state === "here" && (
          <>
            {hp}
            <span className="unit"> HP</span>
          </>
        )}
        {state === "approaching" && approachText(progress ?? 0, eta)}
        {state === "unknown" && "-"}
      </div>
      <div className="bar">
        <div className="bar-fill" style={{ width: `${barWidth(state, hp, max, progress)}%` }} />
        {state === "approaching" && <span className="boss-marker" />}
      </div>
    </div>
  );
}
