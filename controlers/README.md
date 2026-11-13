# Phone controller

The phone controller is optional. Players can keep using the keyboard and mouse,
or enable a phone from **Options → Controls & controller**. The toggle, room code,
join link and QR code all live on that screen, and the choice is remembered in
`user://controller.cfg`.

## One server for the page and the relay

`controlers/server.js` serves the controller page, the art it needs, and the
WebSocket relay from the same port. One tunnel is enough:

```powershell
cd controlers
npm install
npm start          # http://localhost:8000
ngrok http 8000    # copy the https URL it prints
```

`PORT` and `HOST` environment variables change the listen address if 8000 is taken.

In the game:

1. Open **Options → Controls & controller**.
2. Turn on **Phone controller**.
3. Paste the ngrok address into **Relay address** (`https://...` is accepted and
   converted to `wss://...`) and press **Apply**.
4. Scan the QR code with the phone, or open the join link manually.
5. The status changes to **Phone linked** when the game and phone share a room.

The page derives its WebSocket URL from its own origin, so the QR join link and
the relay always use the same tunnel. `?relay=wss://host` is still supported for
split hosting.

For a local network without ngrok, use `ws://<your-lan-ip>:8000`. The field pad
works, but the rover-wheel mode needs HTTPS because browsers only expose motion
sensors on secure origins.

ngrok's free tier shows an interstitial page the first time a browser opens the
tunnel; tap through it and the page reconnects on its own.

## Pages

The join link is `https://your-site/?room=ABCDE`. Without `?room=` the page shows
an error, so add one when previewing.

- `#pad` has the joystick, JUMP, an E button and slots 1–5.
- `#wheel` shows a rotating wheel with the rover sprite in the hub, and W/A/S/D
  indicators that light up as you cross the thresholds.
- The pad and wheel ask you to turn the phone sideways.
- The wheel needs one tap on **Start steering** because iOS requires a user
  gesture to grant motion access. That tap also zeroes the angles; **ZERO**
  recalibrates.

Steering is the rotation of the phone about its screen and tilt is the lean of
its top edge. Both come from the direction of gravity. −35° is A, +38° is D,
+25° is W and −25° is S; each key releases 10° before neutral so it does not
flicker.

## Protocol

Phone → relay:

```json
{ "action": "move_left", "strength": 0.62 }
```

`action` uses Godot input-map names (`move_*`, `training_jump`, `hotbar_1`–`5`),
except `interact`, and `strength` is between 0 and 1. The relay validates the
payload and forwards it as:

```json
{ "type": "input", "action": "move_left", "strength": 0.62 }
```

Lifecycle messages:

- Relay → game: `{"type":"controller_connected"}`,
  `{"type":"controller_disconnected"}`, and `{"type":"room_ready"}`.
- Relay → phone: `{"type":"game_connected"}`, `{"type":"game_disconnected"}`, and
  `{"type":"room_ready"}`.

A new controller or game socket replaces the previous one, so reloading the page
or restarting the game reconnects cleanly. Room codes are 4–8 characters from
`[A-Z0-9]` and are matched case-insensitively.

## Godot side

`ControllerRelay` (autoload) owns the `WebSocketPeer`. It reconnects every two
seconds while enabled, and exposes `Enabled`, `RelayUrl`, `RoomCode`, `JoinUrl`,
`State` and `StatusText` to the Controls screen.

`ControllerInputBridge` turns messages into real Godot input events:

| Message | Godot event | Consumer |
| --- | --- | --- |
| `move_*`, `training_jump`, `hotbar_1`–`5` | `InputEventAction` | Rover, Player, Hotbar |
| `interact` | `InputEventKey` (`Key.E`) | TrainingStations |

Nothing in the gameplay scripts changes. The bridge releases every held action
when the phone disconnects, the relay drops, the controller is disabled, or the
game exits, so no input gets stuck.

Configuration precedence:

1. `--relay=wss://...`, `--room=ABCDE`, `--no-controller`
2. `ARES_RELAY_URL`, `ARES_CONTROLLER=1` environment variables
3. `user://controller.cfg`
4. `ares_controller/relay_url`, `ares_controller/enabled` in `project.godot`

`https://` and `http://` inputs are normalized to `wss://` and `ws://`. The QR
code is generated in-game with `Net.Codecrete.QrCodeGenerator`; no extra endpoint
or network request is needed.

## Tests

```powershell
cd controlers
npm test
```

The Node tests cover input forwarding, invalid payloads, presence messages,
connection replacement and static-file serving. On the Godot side, run
`tests/ControllerInputTest.tscn`; it checks message parsing, input injection,
release-all, room codes, relay URLs and QR generation.
