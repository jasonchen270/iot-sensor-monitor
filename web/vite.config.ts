import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

// The dashboard talks to the .NET service over HTTP + a WebSocket (/live).
// Override VITE_API / VITE_WS at build time; defaults point at the local service.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: "http://localhost:5080", changeOrigin: true },
      "/live": { target: "ws://localhost:5080", ws: true },
    },
  },
});
