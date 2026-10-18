import { useVar } from "@stgadgets/sdk";
import "./Lives.css";

/** Player 1's lives, and the score that gives the next one. */
export function Lives() {
  const lives = useVar<number | null>("p1.lives");
  const nextAt = useVar<number | null>("p1.next_life_at");
  const more = useVar<boolean | null>("p1.more_lives");
  if (lives === undefined) return null;

  return (
    <div className="gadget">
      <div className="label">LIVES</div>
      <div className="value">{lives ?? "-"}</div>
      {more != null && (
        <div className="lives-next dim">
          {more && nextAt != null ? (
            <>
              next at <span className="lives-next-score">{nextAt.toLocaleString("en")}</span>
            </>
          ) : (
            "no more extends"
          )}
        </div>
      )}
    </div>
  );
}
