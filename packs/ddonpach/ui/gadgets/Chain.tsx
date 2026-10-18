import { useVar } from "@stgadgets/sdk";
import "./Chain.css";

/** Player 1's chain: hits so far and the gauge that ends it, plus the best chain this credit. */
export function Chain() {
  const chain = useVar<number | null>("p1.chain");
  const gauge = useVar<number | null>("p1.chain_gauge");
  const best = useVar<number | null>("p1.max_hits");
  if (chain === undefined || gauge === undefined) return null;

  return (
    <div className={chain ? "gadget chain chain-live" : "gadget chain"}>
      <div className="label">CHAIN</div>
      <div className="value">
        {chain ?? "-"}
        <span className="chain-unit"> HIT</span>
      </div>
      <div className="chain-bar">
        <div className="chain-bar-fill" style={{ width: `${(gauge ?? 0) * 100}%` }} />
      </div>
      {chain !== null && best != null && <div className="chain-note dim">best {best}</div>}
    </div>
  );
}
