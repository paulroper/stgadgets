import { useVar } from "@stgadgets/sdk";
import "./index.css";

const SPECIAL_AT = 3; // three let fall, then the next one taken is special (0x2FCAC, 0x2FD9E, 0x2FEC8, 0x30194)

const ITEMS = [
  { name: "dropped.small_shot", label: "SHOT", special: "max shot" },
  { name: "dropped.large_shot", label: "BIG SHOT", special: "max shot" },
  { name: "dropped.option", label: "OPTION", special: "special option" },
  { name: "dropped.extend", label: "1UP", special: "secret extend" },
] as const;

function useDropped(name: string) {
  return useVar<number | null>(name);
}

/** Items let fall in a row: three of a kind, and the next one taken is special. */
export function Drops() {
  const running = useVar<boolean | null>("game.running");
  // ITEMS never changes, so the hooks run in the same order every render.
  // oxlint-disable-next-line react/hooks
  const items = ITEMS.map(item => ({ ...item, dropped: useDropped(item.name) }));
  if (items[0]!.dropped === undefined) return null;

  return (
    <div className="gadget drops">
      <div className="label">DROPPED</div>
      <ul className="drops-items">
        {items.map(({ name, label, special, dropped }) => {
          const count = running ? Math.min(dropped ?? 0, SPECIAL_AT) : null;
          const ready = count === SPECIAL_AT;
          return (
            <li key={name} className={ready ? "drops-ready" : ""}>
              <span className="drops-name">{label}</span>
              <span className="pips">
                {Array.from({ length: SPECIAL_AT }, (_, i) => (
                  <span key={i} className={count !== null && i < count ? "drop on" : "drop"} />
                ))}
              </span>
              <span className="drops-special">{ready ? special.toUpperCase() : ""}</span>
            </li>
          );
        })}
      </ul>
      <div className="note">let 3 fall, take the next</div>
    </div>
  );
}
