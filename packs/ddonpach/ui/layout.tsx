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
 * DoDonPachi, either side of the vertical game. Left: score and chain, the ship, rank, bees and the second loop.
 * Right: distance to the boss at the top; game speed beside the overlay's own latency, then stage splits, at the bottom.
 */
export default function DoDonPachi() {
  return (
    <div className="ddonpach-layout">
      <div className="ddonpach-side">
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
          <SecondLoop />
        </div>
      </div>
      <div className="ddonpach-side ddonpach-right">
        <div className="panel">
          <Boss />
        </div>
        <div className="ddonpach-bottom">
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
