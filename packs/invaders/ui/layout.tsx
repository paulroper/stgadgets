import { Counter, LatencyMeter, Score } from "@stgadgets/gadgets";
import { Accuracy } from "./gadgets/Accuracy";
import { Lives } from "./gadgets/Lives";
import { March } from "./gadgets/March";
import { Saucer } from "./gadgets/Saucer";
import { Ufo } from "./gadgets/Ufo";

/** Side panels for Space Invaders: the classic readouts, then the march and the mystery ship (derived values). */
export default function InvadersClassic() {
  return (
    <div className="layout">
      <div className="layout-left">
        <div className="panel">
          <Ufo />
          <Score name="p1.score" label="SCORE<1>" gain="p1.score_popup" />
          <Score name="hiscore" label="HI-SCORE" />
          <Counter name="fleet.aliens" label="INVADERS" />
          <Lives />
          <Counter name="p1.kills" label="KILLS" />
          <Accuracy />
          <Counter name="credits" label="CREDIT" format={v => String(v).padStart(2, "0")} />
          <LatencyMeter />
        </div>
        <div className="panel">
          <March />
        </div>
        <div className="panel">
          <Saucer />
        </div>
      </div>
    </div>
  );
}
