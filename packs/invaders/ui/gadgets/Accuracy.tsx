import { useVar } from "@stgadgets/sdk";

/** Player 1's hit rate this game, with the kills and shots behind it. */
export function Accuracy() {
  const accuracy = useVar<number | null>("p1.accuracy");
  const kills = useVar<number | null>("p1.kills");
  const shots = useVar<number | null>("p1.shots");
  if (accuracy === undefined) return null;

  return (
    <div className="gadget">
      <div className="label">ACCURACY</div>
      <div className="value">{accuracy === null ? "-" : `${Math.round(accuracy * 100)}%`}</div>
      {kills != null && shots != null && <div className="dim" style={{ fontSize: "var(--label-size)", marginTop: 4 }}>{kills} hits / {shots} shots</div>}
    </div>
  );
}
