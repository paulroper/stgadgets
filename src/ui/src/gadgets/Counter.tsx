import { useVar } from "../sdk/react";

interface Props {
  name: string;
  label: string;
  /** Adjust the raw value before showing it, e.g. spare ships + 1. */
  format?: (value: number) => string | number;
}

/** A labelled number. Renders nothing if the game's map has no such variable. */
export function Counter({ name, label, format = String }: Props) {
  const value = useVar<number | null>(name);
  if (value === undefined) return null;

  return (
    <div className="gadget">
      <div className="label">{label}</div>
      <div className="value">{value === null ? "-" : format(value)}</div>
    </div>
  );
}
