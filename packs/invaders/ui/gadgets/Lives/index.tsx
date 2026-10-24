import { useVar } from "@stgadgets/sdk";
import { Sprite } from "../Sprite";
import "./index.css";

const SHIP = [".....#.....", ".....#.....", "....###....", ".#########.", "###########", "###########", "#.#.#.#.#.#"];

/** Player 1's lives as a row of laser bases, in our own pixel art (docs/gadget-design.md#no-real-game-assets). */
export function Lives() {
  const lives = useVar<number | null>("p1.lives");
  if (lives === undefined) return null;

  return (
    <div className="gadget">
      <div className="label">LIVES</div>
      <div className="value lives">
        <span className="lives-count">{lives ?? "-"}</span>
        {Array.from({ length: lives ?? 0 }, (_, i) => (
          <Sprite key={i} rows={SHIP} label="Ship" className="lives-ship" />
        ))}
      </div>
    </div>
  );
}
