/// <reference types="vitest/config" />
import path from "node:path";
import { defineConfig, searchForWorkspaceRoot, type Plugin } from "vite";
import react from "@vitejs/plugin-react";

const packsDir = path.resolve(import.meta.dirname, "../../packs");
const testsDir = path.resolve(import.meta.dirname, "../../tests/ui");

// What a pack's UI may import besides its own files (docs/packs.md). Keep in sync with tsconfig.json.
const packApi = {
  "@stgadgets/sdk": path.resolve(import.meta.dirname, "src/sdk/index.ts"),
  "@stgadgets/gadgets": path.resolve(import.meta.dirname, "src/gadgets/index.ts"),
};
const allowedInPacks = new Set(["react", "react/jsx-runtime", "react/jsx-dev-runtime", ...Object.keys(packApi)]);

/**
 * Fails the build when a pack's UI imports anything else, such as platform code by relative path, so packs stay
 * separable from the platform.
 */
function packImportRule(): Plugin {
  return {
    name: "stgadgets:pack-imports",
    enforce: "pre",
    resolveId(source, importer) {
      const importerPath = importer?.split("?")[0];
      if (!importerPath?.startsWith(packsDir + path.sep)) return null;

      // packs/<id>/...
      const packRoot = path.join(packsDir, path.relative(packsDir, importerPath).split(path.sep)[0]);
      const target = path.resolve(path.dirname(importerPath), source);
      const allowed =
        allowedInPacks.has(source) ||
        Object.values(packApi).includes(source) ||   // already resolved by the alias
        source.startsWith("\0") ||                    // Vite and plugin internals
        source.startsWith("/@") ||
        target.startsWith(packRoot + path.sep);       // its own files
      if (!allowed) {
        this.error(
          `${path.relative(packsDir, importerPath)} imports "${source}". Pack UIs may import only ` +
            `${[...allowedInPacks].join(", ")} and their own files.`,
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
    alias: packApi,
    // Packs and tests live outside this folder; give them our copies.
    dedupe: ["react", "react-dom", "vitest", "@testing-library/react", "@testing-library/dom"],
  },
  server: {
    host: "127.0.0.1",
    proxy: { "/ws": { target: "ws://127.0.0.1:5199", ws: true } },
    fs: { allow: [searchForWorkspaceRoot(process.cwd()), packsDir, testsDir] },
  },
  // UI tests live in tests/ui, mirroring src/ (docs/conventions.md).
  test: {
    dir: testsDir,
    environment: "jsdom",
  },
});
