import { Counter } from "../gadgets/Counter";
import { LatencyMeter } from "../gadgets/LatencyMeter";
import { Score } from "../gadgets/Score";

/** First layout: a side panel for Space Invaders. */
export function InvadersClassic() {
  return (
    <div className="panel">
      <Score name="p1.score" label="SCORE<1>" />
      <Score name="hiscore" label="HI-SCORE" />
      <Counter name="aliens_remaining" label="INVADERS" />
      <Counter name="p1.ships_reserve" label="SPARE SHIPS" />
      <Counter name="credits" label="CREDIT" format={(v) => String(v).padStart(2, "0")} />
      <LatencyMeter />
    </div>
  );
}
