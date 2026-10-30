
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { BagelleApi } from "./api.js";
import { createServer } from "./server.js";

const api = new BagelleApi({
  baseUrl: process.env.BAGELLE_API_URL || "http://localhost:5038/api",
  token: process.env.BAGELLE_API_TOKEN || "",
});
const readOnly = ["1", "true", "yes"].includes((process.env.BAGELLE_READONLY || "").toLowerCase());

const server = createServer(api, { readOnly });
await server.connect(new StdioServerTransport());

console.error(
  `[bagelle-mcp] запущено · API: ${api.baseUrl} · токен: ${api.hasToken ? "є" : "немає (лише читання публічних даних)"} · режим: ${readOnly ? "read-only" : "читання + запис"}`,
);
