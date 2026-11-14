# Mission: Ares

## Nasa Space Apps Challenge 2026

We will (probably) participate in the Space Mission Design Game challenge.

Built with **Godot 4.7 (.NET)** and **C# (`net8.0`)**.

## Running the project

1. Install Godot 4.7 .NET (the standard build cannot run C# scripts).
2. Open `project.godot` in the editor.
3. Build the C# solution (Godot builds it on run, or use `dotnet build`).
4. Press **F5**.

Main scene: `scenes/main_menu/main_menu_take3.tscn`.
Autoloads: `AudioManager`, `MissionState`, `PauseManager`, and `ControllerRelay`. Audio usage is documented in [`docs/audio.md`](docs/audio.md); the optional phone controller is documented in [`controlers/README.md`](controlers/README.md).

## Controls

| Action | Keys |
| --- | --- |
| Move | `W` `A` `S` `D` |
| Interact | `E` |
| Expand/collapse training monitor | `Q` |
| Select hotbar slot | `1` – `5` |
| Pause | `Esc` |
| Skip intro | `Esc` `E` `Space` `Enter` |

`Hotbar.cs` also checks `hotbar_next` / `hotbar_prev`, but those actions are not in the input map yet.

The phone controller is optional. Enable it from **Options → Controls & controller**, paste the relay address and scan the generated QR code. It reuses the same input actions plus a raw `E` for interaction. While a minigame is open the phone switches to a matching touch panel (wiring, fuse box, frequency, hull repair, launch), with a start/restart overlay for practice modes; the previous page returns when the mode ends. See [`controlers/README.md`](controlers/README.md) for the relay setup and message protocol.

In the gym, small in-world prompts introduce movement, jumping near hurdles, and interaction beside usable stations. Launch practice shows the rocket's full ascent before returning to the lab.

## Architecture

```
ares/
├── project.godot, ares.csproj, ares.sln, README.md, credits.md
├── docs/audio.md                  audio usage guide for the team
├── default_bus_layout.tres        Master / Music / SFX buses
│
├── scenes/                        stage scenes and reusable scene prefabs
│   ├── main_menu/                 main_menu_take3.tscn (main scene), Options.tscn,
│   │                             Volume_menu.tscn, Control_menu.tscn,
│   │                             MenuShell.tscn + MenuTheme.tres (shared shell/theme)
│   ├── intro/                     Intro.tscn (skippable mission briefing)
│   ├── training/                  Training.tscn, TrainingLab.tscn, ControlCabinet.tscn, TrainingBlock.tscn
│   ├── RocketLaunch/              Launching.tscn + launch scene controllers
│   ├── rover/                     rover.tscn (top-down rover prototype)
│   ├── ui/                         MissionMonitor.tscn (shared training monitor)
│   ├── mission_planning/ spacecraft/ transit_failure/
│   ├── minigames/                  wire/fuse, Frequency Box and Hull Repair scenes
│   ├── mars/ gateway/ ending/ credits/ (planned, empty)
│
├── scripts/                      C# (.cs); .cs.uid sidecars are Godot-generated
│   ├── rocket_launch/             LaunchHoldModel.cs, LaunchIgnitionModel.cs
│   ├── player/                   Player.cs, PlayerAnimation.cs, HandEquip.cs
│   ├── rover/                    Rover.cs, Rover_scence.cs, RoverAnimation.cs
│   ├── training/                 training flow, repair practice, supply locker and room interaction
│   ├── mission/                  MissionState.cs (autoload), MissionStage.cs
│   ├── dialogue/                 DialogueLine.cs, DialogueSequence.cs, DialogueView.cs
│   ├── resource_manager/         future route consequences; loadout state lives in MissionState
│   ├── stages/                   IntroSequence.cs; one controller script per stage scene
│   ├── systems/
│   │   ├── PauseManager.cs        global Esc pause menu (autoload)
│   │   ├── audio/                AudioManager.cs (autoload), SoundDef/SoundBank,
│   │   │                         SoundIds, SfxEmitter2D/3D
│   │   ├── inventory/            Inventory.cs, Hotbar.cs, HotbarSlot.cs,
│   │   │                         CargoCapacity.cs, InventoryTransferService.cs
│   │   ├── items/                Item.cs, ItemData.cs ([GlobalClass] Resource),
│   │   │                         ItemCapability.cs, ItemStack.cs, EquipableItem.cs
│   │   ├── input/                ControllerRelay.cs (autoload), ControllerInputBridge.cs,
│   │   │                         ControllerMessage.cs, ControllerCommand.cs,
│   │   │                         ControllerContext.cs, ControllerQr.cs
│   │   └── minigames/            WireMinigame, FuseBoxMinigame, FrequencyBoxMinigame,
│   │                             HullRepairMinigame, MinigameEffects
│   ├── ui/
│   │   ├── Main_menu/            MainMenu.cs, OptionsMenu.cs, VolumeMenu.cs, ControlsMenu.cs
│   │   ├── mission_monitor/       snapshot model/presenter, monitor view and segmented bar
│   │   └── ...                    HotbarUI.cs, ItemButton.cs, button_1.tscn
│   ├── data/                     Resource class definitions (schemas)  (planned, empty)
│   └── Hotbar.tscn, hotbar_scene.tscn     retained hotbar UI and active slot prefab
│
├── data/                         .tres instances of those schemas
│   ├── dialogue/                 intro_transmission.tres, training_intro.tres,
│   │                             training_completion.tres
│   ├── equipment/items/          test_item.tres, maintenance_kit.tres
│   ├── equipment/                (rest planned, empty)
│   └── mission_parameters/ events/ failures/                (planned, empty)
│
├── art/
│   ├── player/                   walk/idle sprites, inventory/ hotbar art
│   ├── rover/                    rover + crash sprites, mars_ground, obstacles, track
│   ├── training/data/            monitor icons and objective state icons
│   ├── training/environment/     training-room obstacles
│   ├── training/equipment/       monitor frame, supply locker + maintenance kit pixel art
│   ├── training/gym/             gym backgrounds and block source art
│   ├── training/launch/          launch interface and backgrounds
│   ├── training/operations/      lab consoles and communications terminal
│   ├── training/stations/        wiring, fuse, frequency, hull + launch stations
│   ├── training/tilesets/        room and training tile atlases
│   └── training/tutorials/       per-minigame visual instructions
│   ├── gateway/                  Background.jpeg (practice launch backdrop)
│   ├── cinematics/               four pixel-art intro backdrops
│   ├── fonts/                    Aerospace.ttf, PressStart2P-Regular.ttf
│   ├── menu_placeholder/         placeholder backgrounds
│   └── tmp/                      scratch art: meowl.png, ominous_red_ball.png
│
├── audio/
│   ├── music/                    menu_test_music.mp3, rover_scene_ambient_wind.mp3
│   └── sfx/                      rover_collision.wav, rover_explode.wav,
│                                 rover_moving.mp3, rover_victory.wav
├── controlers/                   phone controller page + Node relay (`npm start`)
└── hardware/                     optional Arduino firmware, .gdignore + README.md
```

## Contributions

| Workstream | Folders | People |
| --- | --- | --- |
| Core | `scripts/mission`, `scripts/resource_manager`, `scripts/data`, `data/equipment`, `data/mission_parameters`, `scenes/mission_planning` |  |
| Minigames + input | `scripts/systems/minigames`, `scripts/systems/input`, `scripts/systems/items`, `scripts/systems/inventory`, `scenes/minigames`, `data/failures` |  |
| World | `scripts/player`, `scripts/rover`, `scripts/training`, `scripts/stages`, `scenes/training`, `scenes/rover`, and the Spacecraft/Mars/Rover/Gateway scenes |  |
| Shell + UI | `scenes/main_menu`, `scenes/ending`, `scenes/credits`, `scenes/ui`, `scripts/ui` |  |
| Art | `art/`, `art/fonts`, plus assembling their own tilemap scenes | Niki + Pasaye + Eva |
| Audio | `audio/` | Sergio + Macqro |

## Credits

Third-party assets and licenses are tracked in [`credits.md`](credits.md). The optional phone-controller setup lives in [`controlers/README.md`](controlers/README.md).
