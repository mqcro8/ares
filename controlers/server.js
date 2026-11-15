import { createRelayServer } from "./relay.js";

const port = Number(process.env.PORT ?? 8000);
const host = process.env.HOST ?? "0.0.0.0";

const relay = createRelayServer({ port, host });

try {
  const address = await relay.listen();
  const shown = typeof address === "object" && address ? address.port : port;
  console.log(`Mission: Ares relay listening on http://localhost:${shown}`);
  console.log("The controller page and local relay share this URL.");
} catch (error) {
  console.error(`Could not start the relay: ${error.message}`);
  process.exit(1);
}

let closing = false;
async function shutdown(signal) {
  if (closing) return;
  closing = true;
  console.log(`\n${signal} received, shutting down.`);
  try {
    await relay.close();
  } finally {
    process.exit(0);
  }
}

process.on("SIGINT", () => shutdown("SIGINT"));
process.on("SIGTERM", () => shutdown("SIGTERM"));
