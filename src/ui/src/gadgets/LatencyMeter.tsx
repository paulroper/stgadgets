import { useEffect, useState } from "react";
import { latency, type LatencyStats } from "../sdk";

const REFRESH_MS = 250;

/** Debug gadget showing read → paint latency (see sdk/latency.ts). */
export function LatencyMeter() {
  const [stats, setStats] = useState<LatencyStats | null>(null);

  useEffect(() => {
    const timer = setInterval(() => setStats(latency.stats()), REFRESH_MS);
    return () => clearInterval(timer);
  }, []);

  return (
    <div className="gadget debug">
      <div className="label">READ → PAINT</div>
      <div className="value small">
        {stats ? `${stats.p50.toFixed(1)} / ${stats.p95.toFixed(1)} ms` : "–"}
        <span className="dim">{stats ? ` p50/p95 · ${stats.rate.toFixed(0)} Hz` : ""}</span>
      </div>
    </div>
  );
}
