import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

export default defineConfig({
  base: "/",
  plugins: [react()],
  server: {
    proxy: {
      "/api": "http://localhost:5016",
    },
  },
  build: {
    outDir: "../wwwroot",
    emptyOutDir: false,
    rollupOptions: {
      input: {
        client: "client/index.html",
        staff: "staff/index.html",
      },
    },
  },
});
