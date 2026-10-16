import { Counter, LatencyMeter, Score } from "@stgadgets/gadgets";
import { March } from "./gadgets/March";
import { Saucer } from "./gadgets/Saucer";
import { Ufo } from "./gadgets/Ufo";

/** Side panels for Space Invaders: the classic readouts, then the march and the mystery ship (derived values). */
export default function InvadersClassic() {
  return (
    <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-start" }}>
      <div className="panel">
        <Ufo />
        <Score name="p1.score" label="SCORE<1>" />
        <Score name="hiscore" label="HI-SCORE" />
        <Counter name="aliens_remaining" label="INVADERS" />
        <Counter name="p1.ships_reserve" label="SPARE SHIPS" />
        <Counter name="credits" label="CREDIT" format={(v) => String(v).padStart(2, "0")} />
        <LatencyMeter />
      </div>
      <div className="panel" style={{ marginTop: 0 }}>
        <March />
      </div>
      <div className="panel" style={{ marginTop: 0 }}>
        <Saucer />
      </div>
    </div>
  );
}
