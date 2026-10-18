import { Counter, LatencyMeter, Score } from "@stgadgets/gadgets";

/**
 * For games whose pack has no UI: standard gadgets bound to the standard variable names
 * (docs/memory-maps.md#standard-variable-names). Gadgets for variables the map doesn't define render nothing.
 */
export function GenericLayout() {
  return (
    <div className="layout">
      <div className="layout-left">
        <div className="panel">
          <Score name="p1.score" label="1UP" />
          <Score name="p2.score" label="2UP" />
          <Score name="hiscore" label="HI-SCORE" />
          <Counter name="stage" label="STAGE" />
          <Counter name="p1.lives" label="LIVES" />
          <Counter name="p1.ships_reserve" label="SPARE LIVES" />
          <Counter name="p1.bombs" label="BOMBS" />
          <Counter name="rank" label="RANK" />
          <Counter name="credits" label="CREDIT" />
          <LatencyMeter />
        </div>
      </div>
    </div>
  );
}
