import { createServer } from "http";
import { createReadStream } from "fs";
import { promises as fs } from "fs";
import { extname, join, resolve, sep } from "path";
import { fileURLToPath } from "url";
import { WebSocketServer, WebSocket } from "ws";

const ControlersRoot = fileURLToPath(new URL(".", import.meta.url));
const ProjectRoot = resolve(ControlersRoot, "..");
const ArtRoot = join(ProjectRoot, "art");

const AllowedActions = new Set([
  "move_left",
  "move_right",
  "move_up",
  "move_down",
  "interact",
  "training_jump",
  "hotbar_1",
  "hotbar_2",
  "hotbar_3",
  "hotbar_4",
  "hotbar_5",
]);

const RoomPattern = /^[A-Z0-9]{4,8}$/;
const Roles = new Set(["controller", "game"]);

const MimeTypes = new Map([
  [".html", "text/html; charset=utf-8"],
  [".css", "text/css; charset=utf-8"],
  [".js", "text/javascript; charset=utf-8"],
  [".json", "application/json; charset=utf-8"],
  [".png", "image/png"],
  [".jpg", "image/jpeg"],
  [".jpeg", "image/jpeg"],
  [".webp", "image/webp"],
  [".svg", "image/svg+xml"],
  [".ico", "image/x-icon"],
  [".ttf", "font/ttf"],
  [".otf", "font/otf"],
  [".woff", "font/woff"],
  [".woff2", "font/woff2"],
  [".mp3", "audio/mpeg"],
  [".wav", "audio/wav"],
]);

function normalizeRoom(raw) {
  const room = String(raw ?? "").trim().toUpperCase();
  return RoomPattern.test(room) ? room : null;
}

function sendJson(socket, payload) {
  if (socket?.readyState === WebSocket.OPEN) {
    socket.send(JSON.stringify(payload));
  }
}

function resolveStaticFile(pathname) {
  if (pathname === "/" || pathname === "/controlers" || pathname === "/controlers/" ||
      pathname === "/controlers/index.html") {
    return join(ControlersRoot, "index.html");
  }

  if (pathname.startsWith("/art/")) {
    return safeJoin(ArtRoot, pathname.slice("/art/".length));
  }

  if (pathname.startsWith("/controlers/")) {
    return safeJoin(ControlersRoot, pathname.slice("/controlers/".length));
  }

  return null;
}

function safeJoin(root, relative) {
  const target = resolve(root, relative);
  return target === root || target.startsWith(root + sep) ? target : null;
}

async function handleHttpRequest(request, response) {
  if (request.method !== "GET" && request.method !== "HEAD") {
    response.writeHead(405, { Allow: "GET, HEAD", "Content-Type": "text/plain; charset=utf-8" });
    response.end("Method not allowed");
    return;
  }

  let pathname;
  try {
    pathname = decodeURIComponent(new URL(request.url, "http://localhost").pathname);
  } catch {
    response.writeHead(400, { "Content-Type": "text/plain; charset=utf-8" });
    response.end("Bad request");
    return;
  }

  const filePath = resolveStaticFile(pathname);
  if (!filePath) {
    response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
    response.end("Not found");
    return;
  }

  try {
    const stats = await fs.stat(filePath);
    if (!stats.isFile()) throw new Error("Not a file");

    const isPage = filePath.endsWith("index.html");
    response.writeHead(200, {
      "Content-Type": MimeTypes.get(extname(filePath).toLowerCase()) ?? "application/octet-stream",
      "Content-Length": stats.size,
      "Cache-Control": isPage ? "no-cache" : "public, max-age=3600",
    });

    if (request.method === "HEAD") {
      response.end();
      return;
    }

    createReadStream(filePath).pipe(response);
  } catch {
    response.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" });
    response.end("Not found");
  }
}

/**
 * Creates the Mission: Ares relay and controller-page server.
 * Tests pass port 0 and heartbeatMs 0 to avoid real timers/ports.
 */
export function createRelayServer({
  port = 8000,
  host = "0.0.0.0",
  heartbeatMs = 20000,
  log = console.log,
} = {}) {
  const rooms = new Map();
  const httpServer = createServer((request, response) => {
    handleHttpRequest(request, response).catch((error) => {
      log(`[http] ${error.message}`);
      if (!response.headersSent) {
        response.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" });
      }
      response.end("Internal server error");
    });
  });

  const wss = new WebSocketServer({ server: httpServer, maxPayload: 4096 });

  const notify = (room, role, message) => sendJson(room[role], message);

  wss.on("connection", (socket, request) => {
    const url = new URL(request.url, `http://${request.headers.host ?? "localhost"}`);
    const roomId = normalizeRoom(url.searchParams.get("room"));
    const role = url.searchParams.get("role");

    if (!roomId || !Roles.has(role)) {
      socket.close(1008, "Invalid room or role");
      return;
    }

    let room = rooms.get(roomId);
    if (!room) {
      room = { controller: null, game: null };
      rooms.set(roomId, room);
    }

    const previous = room[role];
    if (previous && previous !== socket) {
      // Replace stale or duplicate connections instead of rejecting them so a
      // phone/game reload can reconnect without waiting for the old socket.
      notify(room, role === "controller" ? "game" : "controller",
        { type: role === "controller" ? "controller_disconnected" : "game_disconnected" });
      previous.close(1000, "Replaced by a new connection");
    }

    room[role] = socket;
    socket.isAlive = true;
    socket.on("pong", () => { socket.isAlive = true; });

    log(`[${roomId}] ${role} connected`);

    if (role === "game") {
      sendJson(socket, room.controller ? { type: "controller_connected" } : { type: "room_ready" });
      notify(room, "controller", { type: "game_connected" });
    } else {
      sendJson(socket, room.game ? { type: "game_connected" } : { type: "room_ready" });
      notify(room, "game", { type: "controller_connected" });
    }

    socket.on("message", (data) => {
      if (role !== "controller") return;

      let message;
      try {
        message = JSON.parse(data.toString());
      } catch {
        log(`[${roomId}] Invalid JSON received`);
        return;
      }

      const { action, strength } = message ?? {};
      if (
        !AllowedActions.has(action) ||
        typeof strength !== "number" ||
        !Number.isFinite(strength) ||
        strength < 0 ||
        strength > 1
      ) {
        log(`[${roomId}] Invalid control message`);
        return;
      }

      notify(room, "game", { type: "input", action, strength });
    });

    socket.on("close", () => {
      log(`[${roomId}] ${role} disconnected`);

      if (room[role] !== socket) return;
      room[role] = null;

      if (role === "controller") {
        notify(room, "game", { type: "controller_disconnected" });
      } else {
        notify(room, "controller", { type: "game_disconnected" });
      }

      if (!room.controller && !room.game) {
        rooms.delete(roomId);
      }
    });

    socket.on("error", (error) => {
      log(`[${roomId}] ${role} error: ${error.message}`);
    });
  });

  let heartbeat = null;
  if (heartbeatMs > 0) {
    heartbeat = setInterval(() => {
      for (const socket of wss.clients) {
        if (socket.isAlive === false) {
          socket.terminate();
          continue;
        }

        socket.isAlive = false;
        socket.ping();
      }
    }, heartbeatMs);
    heartbeat.unref?.();
  }

  return {
    httpServer,
    wss,
    rooms,
    listen() {
      return new Promise((resolveAddress, reject) => {
        httpServer.once("error", reject);
        httpServer.listen(port, host, () => resolveAddress(httpServer.address()));
      });
    },
    close() {
      if (heartbeat) clearInterval(heartbeat);
      for (const socket of wss.clients) socket.terminate();
      return new Promise((resolveClose) => {
        wss.close(() => httpServer.close(() => resolveClose()));
      });
    },
  };
}
