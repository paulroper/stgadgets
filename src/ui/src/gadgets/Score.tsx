import { useVar } from "../sdk/react";

interface Props {
  /** Variable to show, e.g. "p1.score" or "hiscore". */
  name: string;
  label: string;
  digits?: number;
}

/** A zero-padded score readout. Renders nothing if the game's map has no such variable. */
export function Score({ name, label, digits = 4 }: Props) {
  const score = useVar<number | null>(name);
  if (score === undefined) return null;

  return (
    <div className="gadget">
      <div className="label">{label}</div>
      <div className="value">{score === null ? "-".repeat(digits) : String(score).padStart(digits, "0")}</div>
    </div>
  );
}
