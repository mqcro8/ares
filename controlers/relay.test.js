import test from "node:test";
import assert from "node:assert/strict";
import { WebSocket } from "ws";
import { createRelayServer } from "./relay.js";

function startRelay(options = {}) {
  const relay = createRelayServer({ port: 0, host: "127.0.0.1", heartbeatMs: 0, log: () => {}, ...options });
  return relay.listen().then((address) => ({ relay, port: address.port }));
}

function connect(port, room, role) {
  const socket = new WebSocket(`ws://127.0.0.1:${port}/?room=${room}&role=${role}`);
  // Buffer every message so tests can register listeners after the socket opens.
  socket.received = [];
  socket.on("message", (data) => {
    try {
      socket.received.push(JSON.parse(data.toString()));
    } catch {
      // Ignore non-JSON frames; the relay never sends them.
    }
  });
  return new Promise((resolve, reject) => {
    socket.once("open", () => resolve(socket));
    socket.once("error", reject);
  });
}

async function nextMessage(socket, predicate = () => true, timeoutMs = 1000) {
  const deadline = Date.now() + timeoutMs;
  while (Date.now() < deadline) {
    const index = socket.received.findIndex(predicate);
    if (index >= 0) return socket.received.splice(index, 1)[0];
    await new Promise((resolve) => setTimeout(resolve, 10));
  }
  throw new Error("Timed out waiting for a relay message");
}

function closeAll(...sockets) {
  for (const socket of sockets) {
    if (socket.readyState === WebSocket.OPEN || socket.readyState === WebSocket.CONNECTING) {
      socket.close();
    }
  }
}

test("forwards controller input to the game", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "ABCD", "game");
  const controller = await connect(port, "abcd", "controller");
  t.after(() => closeAll(game, controller));

  const received = nextMessage(game, (message) => message.type === "input");
  controller.send(JSON.stringify({ action: "move_left", strength: 0.75 }));
  assert.deepEqual(await received, { type: "input", action: "move_left", strength: 0.75 });
});

test("drops invalid controls instead of forwarding them", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "ABCD", "game");
  const controller = await connect(port, "ABCD", "controller");
  t.after(() => closeAll(game, controller));

  controller.send(JSON.stringify({ action: "pause", strength: 1 }));
  controller.send(JSON.stringify({ action: "move_left", strength: 2 }));
  controller.send(JSON.stringify({ action: "move_left", strength: "1" }));
  controller.send("not json");
  controller.send(JSON.stringify({ action: "move_left", strength: -0.1 }));

  await new Promise((resolve) => setTimeout(resolve, 100));
  assert.deepEqual(game.received.filter((message) => message.type === "input"), []);
});

test("notifies the game when a controller connects and disconnects", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "ROOM1", "game");
  t.after(() => closeAll(game));

  const connected = nextMessage(game, (message) => message.type === "controller_connected");
  const controller = await connect(port, "ROOM1", "controller");
  t.after(() => closeAll(controller));
  assert.equal((await connected).type, "controller_connected");

  const disconnected = nextMessage(game, (message) => message.type === "controller_disconnected");
  controller.close();
  assert.equal((await disconnected).type, "controller_disconnected");
});

test("tells a joining game that a controller is already waiting", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const controller = await connect(port, "EARLY", "controller");
  t.after(() => closeAll(controller));

  const game = await connect(port, "EARLY", "game");
  t.after(() => closeAll(game));

  assert.equal((await nextMessage(game, (message) => message.type === "controller_connected")).type, "controller_connected");
});

test("tells the phone when the game connects and disconnects", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const controller = await connect(port, "PHONE", "controller");
  t.after(() => closeAll(controller));

  const connected = nextMessage(controller, (message) => message.type === "game_connected");
  const game = await connect(port, "PHONE", "game");
  t.after(() => closeAll(game));
  assert.equal((await connected).type, "game_connected");

  const disconnected = nextMessage(controller, (message) => message.type === "game_disconnected");
  game.close();
  assert.equal((await disconnected).type, "game_disconnected");
});

test("a reconnecting controller replaces the previous one", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "RETRY", "game");
  const first = await connect(port, "RETRY", "controller");
  t.after(() => closeAll(game, first));

  const closed = new Promise((resolve) => first.once("close", resolve));
  const second = await connect(port, "RETRY", "controller");
  t.after(() => closeAll(second));
  await closed;

  const received = nextMessage(game, (message) => message.type === "input");
  second.send(JSON.stringify({ action: "interact", strength: 1 }));
  assert.deepEqual(await received, { type: "input", action: "interact", strength: 1 });
});

test("rejects invalid room codes and roles", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  for (const url of ["/", "/?room=ABC&role=game", "/?room=ABCD&role=observer", "/?role=game"]) {
    const socket = new WebSocket(`ws://127.0.0.1:${port}${url}`);
    const code = await new Promise((resolve, reject) => {
      socket.once("close", resolve);
      socket.once("error", reject);
    });
    assert.equal(code, 1008);
  }
});

test("serves the controller page and only the art it needs", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const page = await fetch(`http://127.0.0.1:${port}/`);
  assert.equal(page.status, 200);
  assert.match(page.headers.get("content-type"), /text\/html/);
  assert.match(await page.text(), /Mission: Ares/);

  const font = await fetch(`http://127.0.0.1:${port}/art/fonts/Aerospace.ttf`);
  assert.equal(font.status, 200);
  assert.match(font.headers.get("content-type"), /font\/ttf/);

  for (const path of ["/scripts/systems/input/ControllerRelay.cs", "/art/../project.godot", "/nope"]) {
    const response = await fetch(`http://127.0.0.1:${port}${path}`);
    assert.equal(response.status, 404, path);
  }
});
