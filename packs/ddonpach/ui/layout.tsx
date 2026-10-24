import { Counter, LatencyMeter, Score } from "@stgadgets/gadgets";
import { Bees } from "./gadgets/Bees";
import { Boss } from "./gadgets/Boss";
import { Chain } from "./gadgets/Chain";
import { Lives } from "./gadgets/Lives";
import { Power } from "./gadgets/Power";
import { Rank } from "./gadgets/Rank";
import { SecondLoop } from "./gadgets/SecondLoop";
import { Slowdown } from "./gadgets/Slowdown";
import { Splits } from "./gadgets/Splits";
import { StageClear } from "./gadgets/StageClear";
import "./layout.css";

/**
 * DoDonPachi, either side of the vertical game. Left: score and chain, the ship, rank and bees.
 * Right: distance to the boss and the second loop at the top; game speed beside the overlay's own latency, then stage
 * splits, at the bottom.
 */
export default function DoDonPachi() {
  return (
    <div className="layout">
      <div className="layout-left">
        <div className="panel">
          <Score name="p1.score" label="SCORE" digits={9} gain="p1.score_popup" />
          <StageClear />
          <Score name="hiscore" label="HI-SCORE" digits={9} />
          <Chain />
          <Lives />
          <Counter name="p1.bombs_held" label="BOMBS" />
          <Power />
          <Counter name="stage" label="STAGE" />
        </div>
        <div className="panel">
          <Rank />
          <Bees />
        </div>
      </div>
      <div className="layout-right">
        <div className="panel">
          <Boss />
        </div>
        <div className="panel">
          <SecondLoop />
        </div>
        <div className="layout-bottom">
          <div className="panel ddonpach-timing">
            <Slowdown />
            <LatencyMeter />
          </div>
          <div className="panel">
            <Splits />
          </div>
        </div>
      </div>
    </div>
  );
}
