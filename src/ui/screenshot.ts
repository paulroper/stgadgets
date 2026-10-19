// Screenshots of the running layout at several window sizes, to check how it scales (docs/pack-development.md).
// Needs the server running with a game. `npm run screenshot -- 1024x600 2560x1440`; with no sizes, a small, a laptop and
// a large window. Pictures go to screenshots/ (git-ignored), and each side's overflow is printed.
import { mkdirSync } from "node:fs";
import { firefox } from "playwright";

const url = process.env.STGADGETS_URL ?? "http://127.0.0.1:5199/";
const sizes = process.argv.length > 2 ? process.argv.slice(2) : ["1024x600", "1512x917", "2560x1440"];
const out = "screenshots";

mkdirSync(out, { recursive: true });
const browser = await firefox.launch();
try {
  for (const size of sizes) {
    const [width, height] = size.split("x").map(Number);
    const page = await browser.newPage({ viewport: { width, height } });
    await page.goto(url);
    await page.waitForSelector(".layout .gadget", { timeout: 15_000 });
    await page.waitForTimeout(1_000); // a few ticks, so values have arrived

    const rem = await page.evaluate(() => getComputedStyle(document.documentElement).fontSize);
    const sides = await page.$$eval(".layout-left, .layout-right", (els) =>
      els.map((el) => {
        const overflow = el.scrollHeight - el.clientHeight;
        return `${el.className} ${Math.round(el.getBoundingClientRect().width)}px wide, ${overflow > 0 ? `overflows by ${overflow}px` : "fits"}`;
      }),
    );
    console.log(`${size}: 1rem = ${rem}; ${sides.join("; ")}`);

    await page.screenshot({ path: `${out}/${size}.png` });
    await page.close();
  }
} finally {
  await browser.close();
}
