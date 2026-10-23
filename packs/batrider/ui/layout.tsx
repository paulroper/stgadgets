import { Counter, LatencyMeter, Score } from "@stgadgets/gadgets";
import { Bombs } from "./gadgets/Bombs/Bombs";
import { Boss } from "./gadgets/Boss/Boss";
import { Drops } from "./gadgets/Drops/Drops";
import { Lives } from "./gadgets/Lives/Lives";
import { Medal } from "./gadgets/Medal/Medal";
import { Power } from "./gadgets/Power/Power";
import { Rank } from "./gadgets/Rank/Rank";
import { Route } from "./gadgets/Route/Route";
import "./layout.css";

/**
 * Armed Police Batrider, either side of the vertical game. Left: score, the team, bombs and power, then rank and the
 * medal chain. Right: the boss's HP and the route at the top, items close to a special level-up, and the overlay's own
 * latency at the bottom.
 */
export default function Batrider() {
  return (
    <div className="layout">
      <div className="layout-left">
        <div className="panel">
          <Score name="p1.score" label="SCORE" digits={8} gain="p1.score_gain" />
          <Score name="hiscore" label="HI-SCORE" digits={8} />
          <Lives />
          <Bombs />
          <Power />
          <Counter name="stage" label="STAGE" />
        </div>
        <div className="panel">
          <Rank />
          <Medal />
        </div>
      </div>
      <div className="layout-right">
        <div className="panel">
          <Boss />
          <Route />
        </div>
        <div className="panel">
          <Drops />
        </div>
        <div className="layout-bottom">
          <div className="panel batrider-timing">
            <LatencyMeter />
          </div>
        </div>
      </div>
    </div>
  );
}
