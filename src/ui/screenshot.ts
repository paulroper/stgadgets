// Screenshots of the layout at several window sizes, to check how it scales and how it looks (docs/pack-development.md).
// Pictures go to screenshots/ (git-ignored), and each side's overflow is printed.
//
//   npm run screenshot -- 1024x600 2560x1440            the running game: needs the server running with a game
//   npm run screenshot -- --state ddonpach/bossFight    one of a pack's game states (packs/<id>/tests/states.ts)
//   npm run screenshot -- --state ddonpach              every game state of a pack
//
// With no sizes: a small, a laptop and a large window. --state needs neither the server nor an emulator: it starts
// the Vite dev server and shows each state as the server would send it.
import { mkdirSync, existsSync } from "node:fs";
import path from "node:path";
import { parseArgs } from "node:util";
import { firefox } from "playwright";
import { createServer, type ViteDevServer } from "vite";

const { values: options, positionals } = parseArgs({ options: { state: { type: "string" } }, allowPositionals: true });
const sizes = positionals.length > 0 ? positionals : ["1024x600", "1512x917", "2560x1440"];
const out = "screenshots";

/** What to screenshot: a URL and the name its pictures are saved under. */
interface Page {
  url: string;
  name: string;
}

async function gameStatePages(spec: string, vite: ViteDevServer): Promise<Page[]> {
  const [pack, state] = spec.split("/");
  const file = path.resolve(import.meta.dirname, "../../packs", pack, "tests/states.ts");
  if (!existsSync(file)) throw new Error(`No game states for '${pack}' (${file})`);
  const states = Object.keys(((await vite.ssrLoadModule(file)).default as { states: Record<string, unknown> }).states);
  if (state && !states.includes(state)) throw new Error(`No game state '${spec}'. Try ${states.map((s) => `${pack}/${s}`).join(", ")}`);

  const base = vite.resolvedUrls!.local[0];
  return (state ? [state] : states).map((s) => ({ url: `${base}?state=${pack}/${s}`, name: `${pack}-${s}-` }));
}

const vite = options.state ? await createServer({ configFile: path.join(import.meta.dirname, "vite.config.ts"), server: { port: 0 } }) : null;
mkdirSync(out, { recursive: true });
const browser = await firefox.launch();
try {
  let pages: Page[] = [{ url: process.env.STGADGETS_URL ?? "http://127.0.0.1:5199/", name: "" }];
  if (vite) {
    await vite.listen();
    pages = await gameStatePages(options.state!, vite);
  }

  for (const { url, name } of pages) {
    for (const size of sizes) {
      const [width, height] = size.split("x").map(Number);
      const page = await browser.newPage({ viewport: { width, height } });
      await page.goto(url);
      await page.waitForSelector(".layout .gadget", { timeout: 15_000 });
      await page.waitForTimeout(vite ? 200 : 1_000); // a few ticks, so values have arrived; a game state has them at once

      const rem = await page.evaluate(() => getComputedStyle(document.documentElement).fontSize);
      const sides = await page.$$eval(".layout-left, .layout-right", (els) =>
        els.map((el) => {
          const overflow = el.scrollHeight - el.clientHeight;
          return `${el.className} ${Math.round(el.getBoundingClientRect().width)}px wide, ${overflow > 0 ? `overflows by ${overflow}px` : "fits"}`;
        }),
      );
      console.log(`${name}${size}: 1rem = ${rem}; ${sides.join("; ")}`);

      await page.screenshot({ path: `${out}/${name}${size}.png` });
      await page.close();
    }
  }
} finally {
  await browser.close();
  await vite?.close();
}
