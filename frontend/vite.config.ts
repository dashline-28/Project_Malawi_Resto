import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/client/",
  plugins: [react()],
  server: {
    proxy: {
      "/api": "http://localhost:5016",
    },
  },
  build: {
    outDir: "../wwwroot/client",
    emptyOutDir: true,
  },
});
