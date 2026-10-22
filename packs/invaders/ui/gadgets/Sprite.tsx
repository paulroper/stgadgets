import type { SVGProps } from "react";

interface Props extends SVGProps<SVGSVGElement> {
  /** One string per pixel row; "#" is a lit pixel. */
  rows: readonly string[];
  label: string;
}

/** Pixel art as crisp SVG, coloured with CSS `fill`. */
export function Sprite({ rows, label, ...svg }: Props) {
  return (
    <svg
      viewBox={`0 0 ${rows[0]?.length ?? 0} ${rows.length}`}
      shapeRendering="crispEdges"
      role="img"
      aria-label={label}
      {...svg}
    >
      {rows.flatMap((row, y) =>
        // A pixel's position is its identity: the sprite never reorders.
        // oxlint-disable-next-line react/no-array-index-key
        [...row].flatMap((c, x) => (c === "#" ? [<rect key={`${x},${y}`} x={x} y={y} width={1} height={1} />] : [])),
      )}
    </svg>
  );
}
