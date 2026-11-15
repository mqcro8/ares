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

test("forwards touch-native minigame commands to the game", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "CMD1", "game");
  const controller = await connect(port, "CMD1", "controller");
  t.after(() => closeAll(game, controller));

  const pick = nextMessage(game, (message) => message.type === "command");
  controller.send(JSON.stringify({ type: "command", name: "wire_pick", index: 2 }));
  assert.deepEqual(await pick, { type: "command", name: "wire_pick", index: 2 });

  const set = nextMessage(game, (message) => message.type === "command" && message.name === "freq_set");
  controller.send(JSON.stringify({ type: "command", name: "freq_set", index: 1, value: 0.42 }));
  assert.deepEqual(await set, { type: "command", name: "freq_set", index: 1, value: 0.42 });

  const select = nextMessage(game, (message) => message.type === "command" && message.name === "hull_select");
  controller.send(JSON.stringify({ type: "command", name: "hull_select", index: 3 }));
  assert.deepEqual(await select, { type: "command", name: "hull_select", index: 3 });
});

test("drops invalid minigame commands", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "CMD2", "game");
  const controller = await connect(port, "CMD2", "controller");
  t.after(() => closeAll(game, controller));

  const invalid = [
    { type: "command", name: "pause" },
    { type: "command", name: "wire_pick" },
    { type: "command", name: "wire_pick", index: -1 },
    { type: "command", name: "wire_pick", index: 1.5 },
    { type: "command", name: "freq_set", index: 0, value: 2 },
    { type: "command", name: "freq_set", index: 0 },
    { type: "command", name: "hull_select", index: 4 },
    { type: "command", name: "hull_select", value: 1 },
    { type: "command", name: "hull_restart", index: 0 },
  ];
  for (const message of invalid) controller.send(JSON.stringify(message));

  await new Promise((resolve) => setTimeout(resolve, 100));
  assert.deepEqual(game.received.filter((message) => message.type === "command"), []);
});

test("forwards context to the phone and replays it when a phone joins", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "CTX1", "game");
  t.after(() => closeAll(game));

  const context = { type: "context", mode: "wires", active: true, state: { left: ["#f00"] } };
  game.send(JSON.stringify(context));

  const first = await connect(port, "CTX1", "controller");
  t.after(() => closeAll(first));
  assert.deepEqual(await nextMessage(first, (message) => message.type === "context"), context);

  const second = await connect(port, "CTX1", "controller");
  t.after(() => closeAll(second));
  assert.deepEqual(await nextMessage(second, (message) => message.type === "context"), context);
});

test("supports ending cinematic context and controls", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "END1", "game");
  const controller = await connect(port, "END1", "controller");
  t.after(() => closeAll(game, controller));

  const context = {
    type: "context", mode: "ending", active: true,
    state: { outcome: "good", phase: "dialogue", beat: 2, beat_count: 3, can_skip: true, paused: false },
  };
  game.send(JSON.stringify(context));
  assert.deepEqual(await nextMessage(controller, (message) => message.type === "context"), context);

  for (const name of ["ending_pause", "ending_skip", "ending_continue"]) {
    const received = nextMessage(game, (message) => message.type === "command" && message.name === name);
    controller.send(JSON.stringify({ type: "command", name }));
    assert.deepEqual(await received, { type: "command", name });
  }
});

test("drops invalid context and never forwards it from the phone", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());

  const game = await connect(port, "CTX2", "game");
  const controller = await connect(port, "CTX2", "controller");
  t.after(() => closeAll(game, controller));

  for (const message of [
    { type: "context", mode: "unknown", active: true, state: {} },
    { type: "context", mode: "wires", active: "yes", state: {} },
    { type: "context", mode: "wires", active: true, state: [] },
  ]) game.send(JSON.stringify(message));

  controller.send(JSON.stringify({ type: "context", mode: "wires", active: true, state: {} }));

  await new Promise((resolve) => setTimeout(resolve, 100));
  assert.deepEqual(controller.received.filter((message) => message.type === "context"), []);
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

test("team rooms have four fixed slots, retain reconnect tokens, and relay host state", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());
  const host = await connect(port, "TEAM1", "team_host");
  t.after(() => closeAll(host));
  const players = [];
  for (let i = 0; i < 3; i++) players.push(await connect(port, "TEAM1", "player"));
  t.after(() => closeAll(...players));
  const joined = await Promise.all(players.map((socket) =>
    nextMessage(socket, (message) => message.type === "team_joined")));
  assert.deepEqual(joined.map((message) => message.slot).sort(), [1, 2, 3]);
  const lobby = nextMessage(host, (message) => message.type === "team_lobby" &&
    message.players.length === 4 && message.players.every((player) => player.connected && player.ready));
  host.send(JSON.stringify({ type: "team_ready", slot: 0, ready: true }));
  for (let i = 0; i < players.length; i++)
    players[i].send(JSON.stringify({ type: "team_ready", ready: true }));
  assert.equal((await lobby).players.length, 4);
  const pong = nextMessage(players[0], (message) => message.type === "team_pong");
  players[0].send(JSON.stringify({ type: "team_ping", sent_at: 123456 }));
  assert.equal((await pong).sent_at, 123456);
  const starts = players.map((socket) => nextMessage(socket, (message) => message.type === "team_start"));
  host.send(JSON.stringify({ type: "team_start" }));
  assert.equal((await Promise.all(starts)).length, 3);

  const full = new WebSocket(`ws://127.0.0.1:${port}/?room=TEAM1&role=player`);
  const closeCode = await new Promise((resolve, reject) => {
    full.once("close", resolve);
    full.once("error", reject);
  });
  assert.equal(closeCode, 1013);

  const token = joined[1].token;
  const lost = players[1];
  const closed = new Promise((resolve) => lost.once("close", resolve));
  lost.close();
  await closed;
  const retry = new WebSocket(`ws://127.0.0.1:${port}/?room=TEAM1&role=player&token=${token}`);
  retry.received = [];
  retry.on("message", (data) => retry.received.push(JSON.parse(data.toString())));
  await new Promise((resolve, reject) => { retry.once("open", resolve); retry.once("error", reject); });
  t.after(() => closeAll(retry));
  assert.equal((await nextMessage(retry, (message) => message.type === "team_joined")).slot, joined[1].slot);

  retry.send(JSON.stringify({ type: "team_event", name: "gateway_task_complete", index: 1 }));
  await new Promise((resolve) => setTimeout(resolve, 30));
  assert.equal(host.received.some((message) => message.type === "team_event"), false,
    "players cannot complete another slot's assigned repair");
  const taskEvent = nextMessage(host, (message) => message.type === "team_event");
  retry.send(JSON.stringify({ type: "team_event", name: "gateway_task_complete", index: joined[1].slot }));
  assert.equal((await taskEvent).index, joined[1].slot);

  const marsEvent = nextMessage(host, (message) => message.type === "team_event" &&
    message.name === "mars_task_complete");
  retry.send(JSON.stringify({ type: "team_event", name: "mars_task_complete", index: 5 }));
  assert.equal((await marsEvent).index, 5);

  const pickupEvent = nextMessage(host, (message) => message.type === "team_event" &&
    message.name === "oxygen_pickup_claimed");
  const pickupResult = nextMessage(retry, (message) => message.type === "team_pickup_result");
  retry.send(JSON.stringify({ type: "team_event", name: "oxygen_pickup_claim", id: "MarsBase/OxygenCanister1" }));
  assert.equal((await pickupEvent).id, "MarsBase/OxygenCanister1");
  assert.equal((await pickupResult).accepted, true);
  const duplicateResult = nextMessage(players[0], (message) => message.type === "team_pickup_result");
  players[0].send(JSON.stringify({ type: "team_event", name: "oxygen_pickup_claim", id: "MarsBase/OxygenCanister1" }));
  assert.equal((await duplicateResult).accepted, false);

  const scoreEvent = nextMessage(host, (message) => message.type === "team_event" &&
    message.name === "score_submit");
  retry.send(JSON.stringify({ type: "team_event", name: "score_submit", score: 87.5, passed: true }));
  const submittedScore = await scoreEvent;
  assert.equal(submittedScore.score, 87.5);
  assert.equal(submittedScore.slot, joined[1].slot);

  const remotePosition = nextMessage(host, (message) => message.type === "team_position");
  const teammatePosition = nextMessage(players[0], (message) => message.type === "team_position");
  retry.send(JSON.stringify({
    type: "team_position", scene: "res://scenes/mars/MarsBase.tscn",
    x: 123.5, y: 80, facing: "left", flip: true,
  }));
  assert.equal((await remotePosition).slot, joined[1].slot);
  assert.equal((await teammatePosition).x, 123.5);

  const state = { stage: "Mars", objectiveComplete: [true, false, false, false] };
  const update = nextMessage(retry, (message) => message.type === "team_state");
  host.send(JSON.stringify({ type: "team_state", state }));
  assert.deepEqual((await update).state, state);
});

test("two-player crews can start with a fixed participant roster and distributed repairs", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());
  const host = await connect(port, "DUO1", "team_host");
  const player = await connect(port, "DUO1", "player");
  t.after(() => closeAll(host, player));
  await nextMessage(player, (message) => message.type === "team_joined");
  const rosterUpdate = nextMessage(host, (message) => message.type === "team_lobby" &&
    message.players.length === 2 && message.players.every((entry) => entry.connected && entry.ready));
  host.send(JSON.stringify({ type: "team_ready", slot: 0, ready: true }));
  player.send(JSON.stringify({ type: "team_ready", ready: true }));
  await rosterUpdate;
  const started = nextMessage(player, (message) => message.type === "team_start");
  host.send(JSON.stringify({ type: "team_start" }));
  assert.deepEqual((await started).participants, [0, 1]);

  player.send(JSON.stringify({ type: "team_event", name: "gateway_task_complete", index: 0 }));
  await new Promise((resolve) => setTimeout(resolve, 30));
  assert.equal(host.received.some((message) => message.type === "team_event"), false,
    "slot 1 cannot claim slot 0's evenly distributed task");
  const accepted = nextMessage(host, (message) => message.type === "team_event" &&
    message.name === "gateway_task_complete");
  player.send(JSON.stringify({ type: "team_event", name: "gateway_task_complete", index: 3 }));
  assert.equal((await accepted).index, 3);

  const lateJoin = new WebSocket(`ws://127.0.0.1:${port}/?room=DUO1&role=player`);
  const code = await new Promise((resolve, reject) => {
    lateJoin.once("close", resolve);
    lateJoin.once("error", reject);
  });
  assert.equal(code, 1008);
});

test("three-player crews keep every Gateway repair assigned", async (t) => {
  const { relay, port } = await startRelay();
  t.after(() => relay.close());
  const host = await connect(port, "TRIO1", "team_host");
  const players = [await connect(port, "TRIO1", "player"), await connect(port, "TRIO1", "player")];
  t.after(() => closeAll(host, ...players));
  await Promise.all(players.map((socket) => nextMessage(socket, (message) => message.type === "team_joined")));
  const rosterReady = nextMessage(host, (message) => message.type === "team_lobby" &&
    message.players.length === 3 && message.players.every((entry) => entry.connected && entry.ready));
  host.send(JSON.stringify({ type: "team_ready", slot: 0, ready: true }));
  for (const player of players) player.send(JSON.stringify({ type: "team_ready", ready: true }));
  await rosterReady;
  const started = nextMessage(players[1], (message) => message.type === "team_start");
  host.send(JSON.stringify({ type: "team_start" }));
  assert.deepEqual((await started).participants, [0, 1, 2]);

  const completed = nextMessage(host, (message) =>
    message.type === "team_event" && message.name === "gateway_task_complete");
  players[1].send(JSON.stringify({ type: "team_event", name: "gateway_task_complete", index: 2 }));
  assert.equal((await completed).slot, 2);
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
