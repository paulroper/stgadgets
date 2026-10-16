import { useVar } from "@stgadgets/sdk";
import { Sprite } from "./Sprite";
import "./Ufo.css";

const SAUCER = [
  ".....######.....",
  "...##########...",
  "..############..",
  ".##.##.##.##.##.",
  "################",
  "..###..##..###..",
  "...#........#...",
];

/** A pixel-art mystery ship that hovers while the real one is on screen. */
export function Ufo() {
  const flying = useVar("saucer.status") === "flying";
  return <Sprite rows={SAUCER} label="Mystery ship" className={flying ? "ufo flying" : "ufo"} />;
}
