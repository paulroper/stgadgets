import { useVar } from "@stgadgets/sdk";
import "./Lives.css";

const TEAM_MAX = 3; // pN.team.0-2

/** Player 1's pilots left, one pip each, and the score that brings one back. */
export function Lives() {
  const lives = useVar<number | null>("p1.lives");
  const lastIndex = useVar<number | null>("team.last_index");
  const nextAt = useVar<number | null>("p1.next_extend_at");
  if (lives === undefined) return null;

  const team = Math.min((lastIndex ?? TEAM_MAX - 1) + 1, TEAM_MAX);

  return (
    <div className="gadget lives">
      <div className="label">LIVES</div>
      <div className="value">
        {lives ?? "-"}
        <span className="pips lives-pips">
          {Array.from({ length: team }, (_, i) => (
            <span key={i} className={lives !== null && i < lives ? "pilot on" : "pilot"} />
          ))}
        </span>
      </div>
      <div className="note">
        {lives === null ? (
          ""
        ) : nextAt === null ? (
          "no more extends"
        ) : (
          <>
            next at <span className="lives-next-score">{nextAt?.toLocaleString("en")}</span>
          </>
        )}
      </div>
    </div>
  );
}
