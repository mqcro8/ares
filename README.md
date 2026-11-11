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
Autoloads: `AudioManager`, `MissionState`, and `PauseManager`. Audio usage is documented in [`docs/audio.md`](docs/audio.md).

## Current state

- **Main menu** — `Begin Mission` opens `scenes/intro/Intro.tscn`; Options opens Volume and Controls submenus; Credits warns until the credits scene exists; Quit exits. Menu music plays while the current scene lives under `scenes/main_menu/`.
- **Intro** — `scenes/intro/Intro.tscn` is a short skippable mission briefing (`Esc` / `E` / `Space` / `Enter`) with four pixel-art backdrops that crossfade alongside the dialogue. Gateway is a surface base on Mars. Dialogue and objectives come from `data/dialogue/`; the intro then advances to training.
- **Training** — `scenes/training/Training.tscn` contains the physical course; its opening briefing teaches movement, interaction, and jump controls. `TrainingLab.tscn` is a 768×432 workshop matching the gym spawn room, with consistently sized benches, supply locker, launch simulator, and manually unlocked Launch Pad. `Q` toggles the translucent mission monitor. Simulator practice returns to the lab after its success animation.
- **Mission monitor** — reusable pixel-art monitor shows live stamina, training objectives and station prompts. It starts expanded at the screen's upper right; press `Q` to collapse or expand it. Ship health/integrity/energy rows wait for real gameplay models.
- **Equipment/loadout** — the training supply locker gives a free, single-slot **Maintenance Kit** (`data/equipment/items/maintenance_kit.tres`). The kit exposes the `Repair` capability, costs cargo rather than extra hotbar slots, and must be selected before wiring/fuse practice starts. Locker contents, the player inventory, and cargo survive training scene changes through `MissionState`; failed transfers are atomic.
- **Launch** — `scenes/RocketLaunch/Launching.tscn` is the shared practice/real-launch scene. Its progress bar tracks time held in the green band. Successful simulator practice returns to training automatically; real launch still uses the industrial launch-site background.
- **Minigames** — wiring has cable glow/hover effects; all four training drills show matching tutorial art. Frequency Box uses `1`–`3` to select a control, arrows to adjust, and `Shift` for fine tuning. `PauseManager` freezes gameplay and music from `Esc` or the launch pause button; active minigames use the on-screen cancel button.
- **Player + hotbar** — `scripts/player_scene.tscn` is the player prefab: `Player` + a 5-slot hotbar (preloaded with the test item). `TrainingLab` now uses the same hotbar and shared mission inventory. `Inventory` handles stacking and emits `InventoryChanged`; slots are selected with `1`–`5`.
- **Rover** — `scenes/rover/rover.tscn` is a top-down rover prototype with Mars ground, track and rover sprites. Movement/impact logic is still a work in progress (current scripts are placeholders).
- **Mission state** — `MissionState` autoload holds the current `MissionStage`, mission time, shared player/locker inventories, cargo capacity, and the resource axes (budget, mass, energy, communications, spare parts, rover impacts/collisions).
- **Audio** — `AudioManager` autoload with a shared catalog (`data/audio/sound_bank.tres`), crossfaded music, pooled one-shot SFX, and positional/looping emitters. Menu music, rover ambience, collision, explosion, and victory sounds are wired; the intro plays its wind ambience. See [`docs/audio.md`](docs/audio.md).

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
│   ├── rocket_launch/             LaunchHoldModel.cs
│   ├── player/                   Player.cs, PlayerAnimation.cs, HandEquip.cs,
│   │                             player_scene.tscn (prefab with hotbar + test item)
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
│   │   ├── input/                keyboard now, Arduino later, same interface  (planned, empty)
│   │   └── minigames/            WireMinigame, FuseBoxMinigame, FrequencyBoxMinigame,
│   │                             HullRepairMinigame
│   ├── ui/
│   │   ├── Main_menu/            MainMenu.cs, OptionsMenu.cs, VolumeMenu.cs, ControlsMenu.cs
│   │   ├── mission_monitor/       snapshot model/presenter, monitor view and segmented bar
│   │   └── ...                    HotbarUI.cs, ItemButton.cs, button_1.tscn
│   ├── data/                     Resource class definitions (schemas)  (planned, empty)
│   ├── Hotbar.tscn, hotbar_scene.tscn     early hotbar scenes
│   └── test_scene.tscn           scratch scene, misplaced under scripts/
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

Third-party assets and licenses are tracked in [`credits.md`](credits.md). The optional Arduino controller notes live in [`hardware/README.md`](hardware/README.md).
