import { upgradeWebSocket } from "@vercel/functions";
import Redis from "ioredis";

const RoomPattern = /^[A-Z0-9]{4,8}$/;
const Roles = new Set(["controller", "game"]);
const AllowedActions = new Set([
  "move_left", "move_right", "move_up", "move_down", "interact",
  "training_jump", "hotbar_1", "hotbar_2", "hotbar_3", "hotbar_4",
  "hotbar_5", "minigame_action", "minigame_fine", "minigame_rotate_left",
  "minigame_rotate_right", "launch_hold",
]);
const CommandRules = new Map([
  ["wire_pick", { index: true }], ["wire_connect", { index: true }],
  ["fuse_press", { index: true }], ["freq_select", { index: true }],
  ["freq_set", { index: true, value: true }], ["hull_select", { index: true }],
  ["hull_restart", {}], ["minigame_start", {}], ["minigame_cancel", {}],
  ["launch_start", {}], ["launch_tap", {}], ["launch_retry", {}],
  ["ending_skip", {}], ["ending_continue", {}], ["ending_pause", {}],
]);
const ContextModes = new Set(["none", "wires", "fuses", "frequency", "hull", "launch", "ending"]);
const PresenceTtl = 360;

let redis;
function getRedis() {
  if (!process.env.REDIS_URL) throw new Error("REDIS_URL is not configured");
  return redis ??= new Redis(process.env.REDIS_URL, { maxRetriesPerRequest: 2 })
    .on("error", (error) => console.error(`[relay] Redis connection error: ${error.message}`));
}

function validCommand(message) {
  if (!message || typeof message !== "object" || Array.isArray(message)) return null;
  const rule = CommandRules.get(message.name);
  if (!rule) return null;
  const command = { type: "command", name: message.name };
  if (rule.index) {
    if (!Number.isInteger(message.index) || message.index < 0 || message.index > 7) return null;
    if (message.name === "hull_select" && message.index > 3) return null;
    command.index = message.index;
  } else if (message.index !== undefined) return null;
  if (rule.value) {
    if (typeof message.value !== "number" || !Number.isFinite(message.value) ||
        message.value < 0 || message.value > 1) return null;
    command.value = message.value;
  } else if (message.value !== undefined) return null;
  return command;
}

function validContext(message) {
  if (!message || !ContextModes.has(message.mode) || typeof message.active !== "boolean" ||
      !message.state || typeof message.state !== "object" || Array.isArray(message.state)) return null;
  return { type: "context", mode: message.mode, active: message.active, state: message.state };
}

export default async function handler(request) {
  const url = new URL(request.url);
  const room = (url.searchParams.get("room") ?? "").trim().toUpperCase();
  const role = url.searchParams.get("role");
  if (!RoomPattern.test(room) || !Roles.has(role)) {
    return new Response("Invalid room or role", { status: 400 });
  }

  const clientId = crypto.randomUUID();
  const roomKey = `ares:room:${room}`;
  const channel = `ares:events:${room}`;

  return upgradeWebSocket((socket) => {
    const store = getRedis();
    const subscriber = store.duplicate();
    let closed = false;
    let ready = false;
    subscriber.on("error", (error) => {
      console.error(`[relay] Redis subscriber error: ${error.message}`);
      if (!closed) socket.close(1011, "Relay unavailable");
    });
    const send = (payload) => {
      if (!closed && socket.readyState === 1) socket.send(JSON.stringify(payload));
    };
    const publish = (event) => store.publish(channel, JSON.stringify(event));

    const onEvent = (message) => {
      let event;
      try { event = JSON.parse(message); } catch { return; }
      if (event.sender === clientId) return;
      if (event.kind === "message" && event.to === role) send(event.payload);
      if (event.kind === "replace" && event.role === role && event.sender !== clientId)
        socket.close(1000, "Replaced by a new connection");
    };

    const markDisconnected = async () => {
      const removed = await store.eval(
        "if redis.call('HGET', KEYS[1], ARGV[1]) == ARGV[2] then return redis.call('HDEL', KEYS[1], ARGV[1]) else return 0 end",
        1, roomKey, role, clientId,
      );
      if (!removed) return;
      if (role === "controller") await publish({
        kind: "message", to: "game", payload: { type: "controller_disconnected" },
      });
      else {
        await store.hdel(roomKey, "context");
        await publish({ kind: "message", to: "controller", payload: { type: "game_disconnected" } });
      }
    };

    subscriber.on("message", (_name, message) => onEvent(message));
    subscriber.subscribe(channel).then(async () => {
      if (closed) return;
      const previous = await store.eval(
        "local previous = redis.call('HGET', KEYS[1], ARGV[1]); redis.call('HSET', KEYS[1], ARGV[1], ARGV[2]); redis.call('EXPIRE', KEYS[1], ARGV[3]); return previous or ''",
        1, roomKey, role, clientId, PresenceTtl,
      );
      ready = true;

      if (previous && previous !== clientId) {
        await publish({ kind: "replace", role, sender: clientId });
        await publish({
          kind: "message",
          to: role === "controller" ? "game" : "controller",
          payload: { type: role === "controller" ? "controller_disconnected" : "game_disconnected" },
        });
      }

      const otherRole = role === "game" ? "controller" : "game";
      const otherConnected = Boolean(await store.hget(roomKey, otherRole));
      send(otherConnected
        ? { type: role === "game" ? "controller_connected" : "game_connected" }
        : { type: "room_ready" });
      if (role === "game") {
        await publish({ kind: "message", to: "controller", payload: { type: "game_connected" } });
      } else {
        const context = await store.hget(roomKey, "context");
        if (context) send(JSON.parse(context));
        await publish({ kind: "message", to: "game", payload: { type: "controller_connected" } });
      }
    }).catch(() => socket.close(1011, "Relay unavailable"));

    socket.on("message", async (data) => {
      try {
        if (!ready) return;
        let message;
        try { message = JSON.parse(data.toString()); } catch { return; }
        let payload;
        let to;

        if (role === "game") {
          payload = validContext(message);
          if (!payload) return;
          await store.hset(roomKey, "context", JSON.stringify(payload));
          to = "controller";
        } else if (message?.type === "command") {
          payload = validCommand(message);
          if (!payload) return;
          to = "game";
        } else {
          if (!AllowedActions.has(message?.action) || typeof message.strength !== "number" ||
              !Number.isFinite(message.strength) || message.strength < 0 || message.strength > 1) return;
          payload = { type: "input", action: message.action, strength: message.strength };
          to = "game";
        }

        const target = await store.hget(roomKey, to);
        if (target) await publish({ kind: "message", to, payload, sender: clientId });
      } catch (error) {
        console.error(`[relay] Message handling failed: ${error.message}`);
        if (!closed) socket.close(1011, "Relay unavailable");
      }
    });

    socket.on("close", async () => {
      if (closed) return;
      closed = true;
      subscriber.removeAllListeners("message");
      await subscriber.unsubscribe(channel).catch(() => {});
      subscriber.disconnect();
      if (ready) await markDisconnected().catch(() => {});
    });

    socket.on("error", () => {});
  }, { maxPayload: 4096 });
}
