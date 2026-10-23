import { useVar } from "@stgadgets/sdk";
import "./Boss.css";

/** The boss on screen: its name, and the HP left in the part that's being worn down. */
export function Boss() {
  const name = useVar<string | null>("boss.name");
  const hp = useVar<number | null>("boss.hp");
  const max = useVar<number | null>("boss.hp_max");
  if (hp === undefined) return null;

  const present = hp !== null && name != null;

  return (
    <div className={present ? "gadget boss boss-here" : "gadget boss"}>
      <div className="label">BOSS{present ? ` · ${name}` : ""}</div>
      <div className="value">
        {present ? hp : "-"}
        <span className="unit"> HP</span>
      </div>
      <div className="bar">
        <div className="bar-fill" style={{ width: `${present && max ? (hp / max) * 100 : 0}%` }} />
      </div>
    </div>
  );
}
