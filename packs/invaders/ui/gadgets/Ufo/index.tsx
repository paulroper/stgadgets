import { useVar } from "@stgadgets/sdk";
import { Sprite } from "../Sprite";
import "./index.css";

const SAUCER = [
  ".....#####.....",
  "....##...##....",
  "..###########..",
  ".##.#.#.#.#.##.",
  "###############",
  "..##.......##..",
];

/** Our own pixel-art mystery ship (docs/gadget-design.md#no-real-game-assets) that hovers while the real one is on screen. */
export function Ufo() {
  const flying = useVar("saucer.status") === "flying";
  return <Sprite rows={SAUCER} label="Mystery ship" className={flying ? "ufo flying" : "ufo"} />;
}
