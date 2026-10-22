/// <reference types="vitest/config" />
import path from "node:path";
import { defineConfig, searchForWorkspaceRoot, type Plugin } from "vite";
import react from "@vitejs/plugin-react";

const repoDir = path.resolve(import.meta.dirname, "../..");
const packsDir = path.join(repoDir, "packs");
const testsDir = path.join(repoDir, "tests/ui");

// What a pack's UI may import besides its own files (docs/packs.md). Keep in sync with tsconfig.json.
const packApi = {
  "@stgadgets/sdk": path.resolve(import.meta.dirname, "src/sdk/index.ts"),
  "@stgadgets/gadgets": path.resolve(import.meta.dirname, "src/gadgets/index.ts"),
};
const allowedInPackUi = ["react", "react/jsx-runtime", "react/jsx-dev-runtime", ...Object.keys(packApi)];

// What a pack's tests (packs/<id>/tests) may import besides that. @stgadgets/testing stands in for the test package
// pack authors will install once packs can live outside this repository.
const testingApi = { "@stgadgets/testing": path.join(testsDir, "testing.ts") };
const allowedInPackTests = [...allowedInPackUi, "vitest", "@testing-library/react", ...Object.keys(testingApi)];

/**
 * Fails the build when a pack's UI or tests import anything else, such as platform code by relative path, so packs
 * stay separable from the platform.
 */
function packImportRule(): Plugin {
  return {
    name: "stgadgets:pack-imports",
    enforce: "pre",
    resolveId(source, importer) {
      const importerPath = importer?.split("?")[0];
      if (!importerPath?.startsWith(packsDir + path.sep)) return null;

      // packs/<id>/...
      const [pack, folder] = path.relative(packsDir, importerPath).split(path.sep) as [string, ...string[]];
      const packRoot = path.join(packsDir, pack);
      const allowedHere = folder === "tests" ? allowedInPackTests : allowedInPackUi;
      const aliases = folder === "tests" ? { ...packApi, ...testingApi } : packApi;
      const target = path.resolve(path.dirname(importerPath), source);
      const allowed =
        allowedHere.includes(source) ||
        Object.values(aliases).includes(source) || // already resolved by the alias
        source.startsWith("\0") || // Vite and plugin internals
        source.startsWith("/@") ||
        target.startsWith(packRoot + path.sep); // its own files
      if (!allowed) {
        this.error(
          `${path.relative(packsDir, importerPath)} imports "${source}". It may import only ` +
            `${allowedHere.join(", ")} and its pack's own files.`,
        );
      }
      return null;
    },
  };
}

// In development, Vite serves the layouts and proxies the WebSocket to the stgadgets server.
export default defineConfig({
  plugins: [packImportRule(), react()],
  resolve: {
    alias: { ...packApi, ...testingApi },
    // Packs and tests live outside this folder; give them our copies.
    dedupe: ["react", "react-dom", "vitest", "@testing-library/react", "@testing-library/dom"],
  },
  server: {
    host: "127.0.0.1",
    proxy: { "/ws": { target: "ws://127.0.0.1:5199", ws: true } },
    fs: { allow: [searchForWorkspaceRoot(process.cwd()), packsDir, testsDir] },
  },
  // UI tests live in tests/ui, mirroring src/ (docs/conventions.md), and each pack's in packs/<id>/tests.
  test: {
    dir: repoDir,
    include: ["tests/ui/**/*.test.{ts,tsx}", "packs/*/tests/**/*.test.{ts,tsx}"],
    environment: "jsdom",
  },
});
