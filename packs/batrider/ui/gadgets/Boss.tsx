import { useVar } from "@stgadgets/sdk";
import "./Boss.css";

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

  const present = hp !== null && name != null;
  const approaching = !present && progress != null;

  return (
    <div className={present ? "gadget boss boss-here" : approaching ? "gadget boss boss-approaching" : "gadget boss"}>
      <div className="label">BOSS{present ? ` · ${name}` : ""}</div>
      <div className="value">
        {present ? (
          <>
            {hp}
            <span className="unit"> HP</span>
          </>
        ) : approaching ? (
          eta != null ? (
            `in ~${Math.ceil(eta)} s`
          ) : (
            `${Math.round(progress * 100)}%`
          )
        ) : (
          "-"
        )}
      </div>
      <div className="bar">
        <div
          className="bar-fill"
          style={{ width: `${present && max ? (hp / max) * 100 : approaching ? progress * 100 : 0}%` }}
        />
        {approaching && <span className="boss-marker" />}
      </div>
    </div>
  );
}
