import { useVar } from "@stgadgets/sdk";
import "./Ufo.css";

// The mystery ship's sprite, one string per pixel row.
const SPRITE = [
  ".....######.....",
  "...##########...",
  "..############..",
  ".##.##.##.##.##.",
  "################",
  "..###..##..###..",
  "...#........#...",
];

const pixels = SPRITE.flatMap((row, y) => [...row].flatMap((c, x) => (c === "#" ? [{ x, y }] : [])));

/** A pixel-art mystery ship that hovers while the real one is on screen. */
export function Ufo() {
  const flying = useVar("saucer.status") === "flying";

  return (
    <svg
      className={flying ? "ufo flying" : "ufo"}
      viewBox={`0 0 ${SPRITE[0].length} ${SPRITE.length}`}
      shapeRendering="crispEdges"
      role="img"
      aria-label="Mystery ship"
    >
      {pixels.map(({ x, y }) => (
        <rect key={`${x},${y}`} x={x} y={y} width={1} height={1} />
      ))}
    </svg>
  );
}
