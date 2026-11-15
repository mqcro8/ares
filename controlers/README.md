# Phone controller

The phone controller is optional. Players can keep using the keyboard and mouse,
or enable a phone from **Options → Controls & controller**. The toggle, room code,
join link and QR code all live on that screen, and the choice is remembered in
`user://controller.cfg`.

## Vercel hosting

The controller page deploys as static files. The WebSocket relay runs as a
Vercel Function, and Redis shares room presence and context across function
instances.

```powershell
npm install
npm run build
npx vercel
```

Create a Redis database that accepts standard Redis connections. Add its
`redis://` or `rediss://` URL as `REDIS_URL` in Vercel project settings. Deploy
from the repository root and enable Fluid Compute. The game defaults to
`https://ares-seven-fawn.vercel.app`; change the address in **Relay address** if
you use a different domain.

In the game:

1. Open **Options → Controls & controller**.
2. Turn on **Phone controller**.
3. Paste the Vercel site address into **Relay address** and press **Apply**.
4. Scan the QR code with the phone, or open the join link manually.
5. The status changes to **Phone linked** when the game and phone share a room.

HTTPS site addresses are converted to the `/api/relay` WebSocket endpoint. The
QR link uses the site root. `?relay=wss://host/path` remains supported for split
hosting.

For local development, run `npm start` in `controlers` and set the game relay to
`ws://<your-lan-ip>:8000`. Rover-wheel motion sensors need HTTPS in the browser.

## Experimental 2–4 player team rooms

Team rooms use the same **host-run Node relay**, not Vercel or Redis. One player
starts the relay and exposes port 8000 with ngrok:

```powershell
cd controlers
npm start
ngrok http 8000
```

At the real launch pad, choose **Create Crew** and enter the ngrok HTTPS address.
Share that address and the generated room code. Other players finish individual
training, choose **Join Crew**, and enter both values. The host can start with
2–4 connected, ready players; a fifth connection is rejected. Participating
slots are fixed at launch and late joins are rejected. Reconnecting players
reclaim their reserved slot using a token saved locally by the game.

Alternatively, preconfigure each game with user command-line arguments:

```powershell
godot --path . -- --team-host --relay=https://YOUR-NGROK-DOMAIN --room=ABCDE
godot --path . -- --team-join --relay=https://YOUR-NGROK-DOMAIN --room=ABCDE
```

The team room remains experimental. It provides a four-slot lobby,
ready/start barrier, reconnect reservations, and a wait/pause gate on
disconnect. Gateway repairs are distributed across the active crew and shared; Mars
generator, wiring, signal and door progress is shared; failed team rover runs
retry, and Mars entry waits until every participant has a rover result. It shows
remote crew markers in shared world scenes (not animated astronaut sprites).
Oxygen canisters are claimed once by the crew and refill only the collector's
personal suit; suits and loadouts are not transferred between players. The
debrief waits for all participants and shows their mean mission score; the crew
passes only if every participant's result passes.

The in-game crew network label reports WebSocket round-trip latency and
received position updates per second. Remote markers interpolate between 15 Hz
position updates; this smooths movement display but cannot remove network delay.

## Pages

The join link is `https://your-project.vercel.app/?room=ABCDE`. Without `?room=` the page shows
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

HTTPS site addresses use the secure `/api/relay` endpoint; local HTTP addresses
remain `ws://`. The QR code is generated in-game with
`Net.Codecrete.QrCodeGenerator`; no extra network request is needed.

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
