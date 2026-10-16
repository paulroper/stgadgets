// Read → paint latency (requirement L3, docs/requirements.md): from the server reading memory to the browser's next
// paint. Doesn't include the emulator side (up to ~1 frame for polling providers) or the OS compositor.

import type { StgadgetsClient } from "./client";

const WINDOW = 300; // ticks, ~5 s at 60 Hz
const REPORT_INTERVAL_MS = 5000;

export interface LatencyStats {
  p50: number;
  p95: number;
  max: number;
  /** Server read → message received by the page (p50). */
  arriveP50: number;
  /** Message received → next paint (p50). */
  paintP50: number;
  /** Ticks painted per second. */
  rate: number;
  samples: number;
}

/** High-resolution wall clock in Unix ms, comparable with the server's timestamps on the same machine. */
const now = () => performance.timeOrigin + performance.now();

export class LatencyTracker {
  private samples: { at: number; ms: number; arrive: number; paint: number }[] = [];
  private reported = 0;

  constructor(client: StgadgetsClient, source: string) {
    client.onFrame((_vars, _prev, info) => {
      // The next animation frame is when this tick's changes reach the screen.
      requestAnimationFrame(() => {
        const at = now();
        this.samples.push({ at, ms: at - info.readAt, arrive: info.receivedAt - info.readAt, paint: at - info.receivedAt });
        if (this.samples.length > WINDOW) this.samples.shift();
      });
    });

    // Diagnostics: how often the page actually paints and runs timers, to spot WebKit throttling.
    let rafCount = 0;
    let timerCount = 0;
    const countRaf = () => {
      rafCount++;
      requestAnimationFrame(countRaf);
    };
    requestAnimationFrame(countRaf);
    setInterval(() => timerCount++, 16);

    // Report to the server so latency shows up in its log.
    setInterval(() => {
      const stats = this.stats();
      const latest = this.samples.at(-1)?.at ?? 0;
      if (stats && latest > this.reported) {   // only new samples
        client.send({
          type: "latency",
          source,
          ...stats,
          rafHz: (rafCount * 1000) / REPORT_INTERVAL_MS,
          timerHz: (timerCount * 1000) / REPORT_INTERVAL_MS,
          visibility: document.visibilityState,
          focused: document.hasFocus(),
        });
      }
      this.reported = latest;
      rafCount = timerCount = 0;
    }, REPORT_INTERVAL_MS);
  }

  stats(): LatencyStats | null {
    const s = this.samples;
    if (s.length < 2) return null;
    const sorted = s.map((x) => x.ms).sort((a, b) => a - b);
    const median = (values: number[]) => values.sort((a, b) => a - b)[Math.floor(values.length / 2)];
    return {
      p50: sorted[Math.floor(sorted.length * 0.5)],
      p95: sorted[Math.floor(sorted.length * 0.95)],
      max: sorted[sorted.length - 1],
      arriveP50: median(s.map((x) => x.arrive)),
      paintP50: median(s.map((x) => x.paint)),
      rate: ((s.length - 1) * 1000) / (s[s.length - 1].at - s[0].at),
      samples: s.length,
    };
  }
}
