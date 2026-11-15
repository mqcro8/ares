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

The game can also take over the page with a touch panel for the active
minigame: `#wires`, `#fuses`, `#frequency`, `#hull` and `#launch`. Opening one of
those hashes manually previews the panel, but during play the context message
drives the switch. When the context ends the page returns to whatever the player
was using (pad, wheel or menu). Practice minigames and the launch scene also
push a start/restart overlay onto the phone, so the phone alone can begin.

Steering is the rotation of the phone about its screen and tilt is the lean of
its top edge. Both come from the direction of gravity. −35° is A, +38° is D,
+25° is W and −25° is S; each key releases 10° before neutral so it does not
flicker.

## Protocol

Phone → relay, held input:

```json
{ "action": "move_left", "strength": 0.62 }
```

`action` uses Godot input-map names (`move_*`, `training_jump`, `hotbar_1`–`5`,
`minigame_action`, `minigame_fine`, `minigame_rotate_left`,
`minigame_rotate_right`, `launch_hold`), except `interact`, and `strength` is
between 0 and 1. The relay validates the payload and forwards it as:

```json
{ "type": "input", "action": "move_left", "strength": 0.62 }
```

Phone → relay, semantic minigame command:

```json
{ "type": "command", "name": "fuse_press", "index": 2 }
```

| Command | Arguments | Meaning |
| --- | --- | --- |
| `wire_pick` | `index` 0–7 | Start dragging a left terminal |
| `wire_connect` | `index` 0–7 | Connect the held wire to a right terminal |
| `fuse_press` | `index` 0–7 | Flip one fuse switch |
| `freq_select` | `index` 0–2 | Focus one frequency knob |
| `freq_set` | `index` 0–2, `value` 0–1 | Set a knob to an absolute value |
| `hull_select` | `index` 0–3 | Select a patch plate |
| `hull_restart` | — | Restart hull repair |
| `minigame_start` | — | Dismiss the phone start gate |
| `minigame_cancel` | — | Return to the lab / restart the exercise |
| `launch_start` | — | Begin ignition |
| `launch_tap` | — | Register one ignition tap |
| `launch_retry` | — | Retry a failed ignition |
| `ending_skip` | — | Skip the current ending cinematic |
| `ending_pause` | — | Toggle the pause menu during the ending |
| `ending_continue` | — | Return to Mission Control from the ending debrief |

The relay drops unknown names, mistyped indexes and unexpected arguments. Game →
phone context:

```json
{ "type": "context", "mode": "hull", "active": true, "state": { "phase": "weld" } }
```

Modes are `none`, `wires`, `fuses`, `frequency`, `hull`, `launch` and `ending`. The relay
caches the latest context and replays it to a phone that joins late, and clears
it when the game disconnects. `active:false` (or `none`) tells the page to
restore its previous view. Context is state, not input: phones may never send it.

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

`ControllerInputBridge` turns held messages into real Godot input events:

| Message | Godot event | Consumer |
| --- | --- | --- |
| `move_*`, `training_jump`, `hotbar_1`–`5` | `InputEventAction` | Rover, Player, Hotbar |
| `minigame_action`, `minigame_fine`, `minigame_rotate_*`, `launch_hold` | `InputEventAction` | Hull repair, launch |
| `interact` | `InputEventKey` (`Key.E`) | TrainingStations |

Commands bypass the input map and reach the active scene through
`ControllerCommandRouter`. A minigame registers itself as
`IControllerCommandSink` and publishes phone state as `IControllerContextSource`;
`PracticeReturn` handles `minigame_start` / `minigame_cancel` and gates gameplay
commands while its instructions overlay is open. `ControllerContext` is
token-scoped, so a scene that is leaving cannot clear a newer scene's context.
The relay sends context at most ~15 times a second and immediately re-sends it
when a phone connects.

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

The Node tests cover input forwarding, minigame commands, context
forwarding/replay, invalid payloads, presence messages, connection replacement
and static-file serving. On the Godot side, run `tests/ControllerInputTest.tscn`;
it checks message parsing, input injection, command parsing/validation/routing,
context publishing, release-all, room codes, relay URLs and QR generation. Also
run `tests/TrainingPracticeTest.tscn` and `tests/RocketLaunchTest.tscn` after
touching the minigame hooks.
