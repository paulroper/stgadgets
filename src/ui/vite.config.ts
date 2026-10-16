import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// In development, Vite serves the layouts and proxies the WebSocket to the stgadgets server.
export default defineConfig({
  plugins: [react()],
  server: {
    host: "127.0.0.1",
    proxy: { "/ws": { target: "ws://127.0.0.1:5199", ws: true } },
  },
});
