import { createServer } from "http";
import { createReadStream } from "fs";
import { promises as fs } from "fs";
import { extname, join, resolve, sep } from "path";
import { fileURLToPath } from "url";
import { randomUUID } from "crypto";
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
  "minigame_action",
  "minigame_fine",
  "minigame_rotate_left",
  "minigame_rotate_right",
  "launch_hold",
]);

// Commands are the touch-native minigame controls. Index-based commands use a
// 0-based slot; freq_set adds an absolute value and hull_select caps at 3.
const CommandRules = new Map([
  ["wire_pick", { index: true }],
  ["wire_connect", { index: true }],
  ["fuse_press", { index: true }],
  ["freq_select", { index: true }],
  ["freq_set", { index: true, value: true }],
  ["hull_select", { index: true }],
  ["hull_restart", {}],
  ["minigame_start", {}],
  ["minigame_cancel", {}],
  ["launch_start", {}],
  ["launch_tap", {}],
  ["launch_retry", {}],
  ["ending_skip", {}],
  ["ending_continue", {}],
  ["ending_pause", {}],
]);

const ContextModes = new Set(["none", "wires", "fuses", "frequency", "hull", "launch", "ending"]);

const RoomPattern = /^[A-Z0-9]{4,8}$/;
const Roles = new Set(["controller", "game", "team_host", "player"]);

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

function isPlainObject(value) {
  return value !== null && typeof value === "object" && !Array.isArray(value);
}

function validIndex(index) {
  return Number.isInteger(index) && index >= 0 && index <= 7;
}

// Returns a normalized command payload, or null when the message is invalid.
function normalizeCommand(message) {
  if (!isPlainObject(message)) return null;
  const name = message.name;
  const rule = CommandRules.get(name);
  if (!rule) return null;

  const command = { type: "command", name };

  if (rule.index) {
    if (!validIndex(message.index)) return null;
    if (name === "hull_select" && message.index > 3) return null;
    command.index = message.index;
  } else if (message.index !== undefined) {
    return null;
  }

  if (rule.value) {
    if (typeof message.value !== "number" || !Number.isFinite(message.value)) return null;
    if (name === "freq_set" && (message.value < 0 || message.value > 1)) return null;
    command.value = message.value;
  } else if (message.value !== undefined) {
    return null;
  }

  return command;
}

// Returns a normalized context payload, or null when the message is invalid.
function normalizeContext(message) {
  if (!isPlainObject(message)) return null;
  if (!ContextModes.has(message.mode)) return null;
  if (typeof message.active !== "boolean") return null;
  if (!isPlainObject(message.state)) return null;
  return {
    type: "context",
    mode: message.mode,
    active: message.active,
    state: message.state,
  };
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
      room = {
        controller: null, game: null, context: null, players: new Map(), nextSlot: 1,
        oxygenClaims: new Map(), positions: new Map(),
      };
      rooms.set(roomId, room);
    }

    const previous = role === "player" ? null : room[role];
    if (previous && previous !== socket) {
      // Replace stale or duplicate connections instead of rejecting them so a
      // phone/game reload can reconnect without waiting for the old socket.
      if (role === "controller" || role === "game")
        notify(room, role === "controller" ? "game" : "controller",
          { type: role === "controller" ? "controller_disconnected" : "game_disconnected" });
      previous.close(1000, "Replaced by a new connection");
    }

    if (role !== "player") room[role] = socket;
    if (role === "team_host") room.teamHost = socket;
    if (role === "player") {
      const token = String(url.searchParams.get("token") ?? "");
      let player = token ? [...room.players.values()].find((entry) => entry.token === token) : null;
      if (!player) {
        if (room.players.size >= 3) {
          socket.close(1013, "Room is full");
          return;
        }
        player = { slot: room.nextSlot++, token: randomUUID(), socket: null, ready: false };
        room.players.set(player.slot, player);
      }
      player.socket = socket;
      player.ready = false;
      socket.playerSlot = player.slot;
      socket.playerToken = player.token;
      sendJson(socket, {
        type: "team_joined",
        slot: player.slot,
        token: player.token,
        players: teamRoster(room),
      });
      if (room.teamState) sendJson(socket, { type: "team_state", state: room.teamState });
      for (const position of room.positions.values())
        sendJson(socket, { type: "team_position", ...position });
      broadcastTeam(room, { type: "team_lobby", players: teamRoster(room) });
    }
    socket.isAlive = true;
    socket.on("pong", () => { socket.isAlive = true; });

    log(`[${roomId}] ${role} connected`);

    if (role === "team_host") {
      sendJson(socket, { type: "team_lobby", players: teamRoster(room) });
      if (room.teamState) sendJson(socket, { type: "team_state", state: room.teamState });
      for (const position of room.positions.values())
        sendJson(socket, { type: "team_position", ...position });
    } else if (role === "game") {
      sendJson(socket, room.controller ? { type: "controller_connected" } : { type: "room_ready" });
      notify(room, "controller", { type: "game_connected" });
      sendJson(socket, { type: "team_lobby", players: teamRoster(room) });
      if (room.teamState) sendJson(socket, { type: "team_state", state: room.teamState });
    } else if (role === "controller") {
      sendJson(socket, room.game ? { type: "game_connected" } : { type: "room_ready" });
      if (room.context) sendJson(socket, room.context);
      notify(room, "game", { type: "controller_connected" });
    } else {
      sendJson(socket, { type: "team_lobby", players: teamRoster(room) });
    }

    socket.on("message", (data) => {
      let message;
      try {
        message = JSON.parse(data.toString());
      } catch {
        log(`[${roomId}] Invalid JSON received`);
        return;
      }

      if (role === "team_host") {
        if (room.teamStarted && message?.type === "team_position" &&
            message.slot === 0 && validTeamPosition(message)) {
          const position = { slot: 0, scene: message.scene, x: message.x, y: message.y,
            facing: message.facing, flip: message.flip };
          room.positions.set(0, position);
          for (const player of room.players.values())
            sendJson(player.socket, { type: "team_position", ...position });
          return;
        }
        if (room.teamStarted && message?.type === "team_position_broadcast" &&
            Number.isInteger(message.slot) && message.slot >= 1 && message.slot <= 3 &&
            validTeamPosition(message)) {
          const position = { slot: message.slot, scene: message.scene, x: message.x, y: message.y,
            facing: message.facing, flip: message.flip };
          room.positions.set(message.slot, position);
          for (const player of room.players.values())
            sendJson(player.socket, { type: "team_position", ...position });
          return;
        }
        if (message?.type === "team_state" && isPlainObject(message.state)) {
          room.teamState = message.state;
          for (const player of room.players.values())
            sendJson(player.socket, { type: "team_state", state: message.state });
          return;
        }
        if (message?.type === "team_start") {
          const roster = teamRoster(room);
          if (roster.length !== 4 || roster.some((player) => !player.connected || !player.ready)) {
            sendJson(socket, { type: "team_error", message: "All four players must be connected and ready." });
            return;
          }
          room.teamStarted = true;
          broadcastTeam(room, { type: "team_start" });
          return;
        }
        if (message?.type === "team_ready" && message.slot === 0) {
          room.hostReady = message.ready === true;
          broadcastTeam(room, { type: "team_lobby", players: teamRoster(room) });
          return;
        }
        if (room.teamStarted && message?.type === "team_event" && message.name === "oxygen_pickup_claim" &&
            message.slot === 0 && validPickupId(message.id)) {
          claimOxygenPickup(room, 0, message.id);
          return;
        }
        return;
      }

      if (role === "game") {
        const context = normalizeContext(message);
        if (!context) {
          log(`[${roomId}] Invalid context message`);
          return;
        }
        room.context = context;
        notify(room, "controller", context);
        return;
      }

      if (role === "player") {
        const player = room.players.get(socket.playerSlot);
        if (!player || player.socket !== socket) return;
        if (message?.type === "team_ready") {
          player.ready = message.ready === true;
          broadcastTeam(room, { type: "team_lobby", players: teamRoster(room) });
        } else if (room.teamStarted && message?.type === "team_position" && validTeamPosition(message)) {
          const position = { slot: player.slot, scene: message.scene, x: message.x, y: message.y,
            facing: message.facing, flip: message.flip };
          room.positions.set(player.slot, position);
          sendJson(room.teamHost, { type: "team_position", ...position });
          for (const teammate of room.players.values())
            if (teammate.slot !== player.slot)
              sendJson(teammate.socket, { type: "team_position", ...position });
        } else if (room.teamStarted && message?.type === "team_event" &&
                   message.name === "gateway_task_complete" &&
                   Number.isInteger(message.index) && message.index === player.slot) {
          sendJson(room.teamHost, {
            type: "team_event",
            name: message.name,
            slot: player.slot,
            index: message.index,
          });
        } else if (room.teamStarted && message?.type === "team_event" &&
                   message.name === "mars_task_complete" &&
                   Number.isInteger(message.index) && message.index >= 0 && message.index <= 6) {
          sendJson(room.teamHost, {
            type: "team_event",
            name: message.name,
            slot: player.slot,
            index: message.index,
          });
        } else if (room.teamStarted && message?.type === "team_event" &&
                   message.name === "rover_complete" &&
                   Number.isInteger(message.index) && message.index === player.slot) {
          sendJson(room.teamHost, {
            type: "team_event",
            name: message.name,
            slot: player.slot,
            index: message.index,
          });
        } else if (room.teamStarted && message?.type === "team_event" &&
                   message.name === "oxygen_pickup_claim" && validPickupId(message.id)) {
          claimOxygenPickup(room, player.slot, message.id);
        } else if (room.teamStarted && message?.type === "team_event" &&
                   message.name === "score_submit" && validTeamScore(message.score, message.passed)) {
          sendJson(room.teamHost, {
            type: "team_event",
            name: "score_submit",
            slot: player.slot,
            index: player.slot,
            score: message.score,
            passed: message.passed,
          });
        }
        return;
      }

      if (message?.type === "command") {
        const command = normalizeCommand(message);
        if (!command) {
          log(`[${roomId}] Invalid command message`);
          return;
        }
        notify(room, "game", command);
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

      if (role !== "player") {
        if (room[role] !== socket) return;
        room[role] = null;
      }

      if (role === "player") {
        const player = room.players.get(socket.playerSlot);
        if (player?.socket === socket) {
          player.socket = null;
          player.ready = false;
        }
        if (room.teamHost) sendJson(room.teamHost, { type: "team_player_disconnected", slot: socket.playerSlot });
        broadcastTeam(room, { type: "team_lobby", players: teamRoster(room) });
      } else if (role === "controller") {
        notify(room, "game", { type: "controller_disconnected" });
      } else if (role === "game") {
        room.context = null;
        notify(room, "controller", { type: "game_disconnected" });
      } else {
        if (room.teamHost !== socket) return;
        room.teamHost = null;
        room.hostReady = false;
        for (const player of room.players.values()) {
          player.socket?.close(1001, "Host disconnected");
          player.socket = null;
          player.ready = false;
        }
      }

      if (!room.controller && !room.game && room.players.size === 0) {
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

function teamRoster(room) {
  return [
    { slot: 0, connected: Boolean(room.teamHost), ready: Boolean(room.hostReady), host: true },
    ...[...room.players.values()].map(({ slot, socket, ready }) => ({
      slot, connected: Boolean(socket), ready: Boolean(ready), host: false,
    })),
  ].sort((a, b) => a.slot - b.slot);
}

function broadcastTeam(room, message) {
  sendJson(room.teamHost, message);
  for (const player of room.players.values()) sendJson(player.socket, message);
}

function validPickupId(id) {
  return typeof id === "string" && id.length > 0 && id.length <= 160 &&
    /^[A-Za-z0-9_.:/-]+$/.test(id);
}

function validTeamScore(score, passed) {
  return typeof score === "number" && Number.isFinite(score) &&
    score >= 0 && score <= 100 && typeof passed === "boolean";
}

function validTeamPosition(message) {
  return typeof message.scene === "string" && message.scene.length > 0 &&
    message.scene.length <= 160 && Number.isFinite(message.x) &&
    Number.isFinite(message.y) && Math.abs(message.x) <= 100000 &&
    Math.abs(message.y) <= 100000 &&
    ["up", "down", "side", "left", "right"].includes(message.facing) &&
    typeof message.flip === "boolean";
}

function claimOxygenPickup(room, slot, id) {
  const owner = room.oxygenClaims.get(id);
  const accepted = owner === undefined || owner === slot;
  if (accepted && owner === undefined) {
    room.oxygenClaims.set(id, slot);
    sendJson(room.teamHost, { type: "team_event", name: "oxygen_pickup_claimed", slot, id });
  }
  const result = { type: "team_pickup_result", id, accepted };
  if (slot === 0) sendJson(room.teamHost, result);
  else sendJson(room.players.get(slot)?.socket, result);
}
