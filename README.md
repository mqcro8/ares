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
Autoloads: `AudioManager` (`scripts/systems/audio/AudioManager.cs`) and `MissionState` (`scripts/mission/MissionState.cs`). Audio usage is documented in [`docs/audio.md`](docs/audio.md).

## Current state

- **Main menu** — Start opens `scenes/intro/Training.tscn`; Options opens Volume and Controls submenus; Credits warns until the credits scene exists; Quit exits. Menu music plays while the current scene lives under `scenes/main_menu/`.
- **Training** — `scenes/intro/Training.tscn`: a 3-line briefing (Spanish) that locks player physics, then free 4-directional movement with idle/walk animation. No hotbar in this scene yet.
- **Player + hotbar** — `scripts/player_scene.tscn` is the player prefab: `Player` + a 5-slot hotbar (preloaded with the test item). `Inventory` handles stacking and emits `InventoryChanged`; slots are selected with `1`–`5`.
- **Rover** — `scenes/rover/rover.tscn` is a top-down rover prototype with Mars ground, track and rover sprites. Movement/impact logic is still a work in progress (current scripts are placeholders).
- **Mission state** — `MissionState` autoload holds the current `MissionStage`, mission time, and the resource axes (budget, mass, energy, communications, spare parts, rover impacts/collisions).
- **Audio** — `AudioManager` autoload with a shared catalog (`data/audio/sound_bank.tres`), crossfaded music, pooled one-shot SFX, and positional/looping emitters. Menu music, rover ambience, collision, explosion, and victory sounds are wired. See [`docs/audio.md`](docs/audio.md).

## Controls

| Action | Keys |
| --- | --- |
| Move | `W` `A` `S` `D` |
| Select hotbar slot | `1` – `5` |

`Hotbar.cs` also checks `hotbar_next` / `hotbar_prev`, but those actions are not in the input map yet.

## Architecture

```
ares/
├── project.godot, ares.csproj, ares.sln, README.md, credits.md
├── docs/audio.md                  audio usage guide for the team
├── default_bus_layout.tres        Master / Music / SFX buses
│
├── scenes/                        one folder per stage
│   ├── main_menu/                 main_menu_take3.tscn (main scene), Options.tscn,
│   │                             Volume_menu.tscn, Control_menu.tscn,
│   │                             MenuShell.tscn + MenuTheme.tres (shared shell/theme)
│   ├── intro/                     Training.tscn (briefing + movement room)
│   ├── rover/                     rover.tscn (top-down rover prototype)
│   ├── training/ mission_planning/ spacecraft/ transit_failure/
│   ├── mars/ gateway/ ending/ credits/ ui/ minigames/        (planned, empty)
│
├── scripts/                      C# (.cs); .cs.uid sidecars are Godot-generated
│   ├── player/                   Player.cs, PlayerAnimation.cs, HandEquip.cs,
│   │                             player_scene.tscn (prefab with hotbar + test item)
│   ├── rover/                    Rover.cs, Rover_scence.cs, RoverAnimation.cs
│   ├── training/                 TrainingIntro.cs (briefing, locks player physics)
│   ├── mission/                  MissionState.cs (autoload), MissionStage.cs
│   ├── resource_manager/         loadout rules + consequences, holds no state  (planned, empty)
│   ├── stages/                   one controller script per stage scene  (planned)
│   ├── systems/
│   │   ├── audio/                AudioManager.cs (autoload), SoundDef/SoundBank,
│   │   │                         SoundIds, SfxEmitter2D/3D
│   │   ├── inventory/            Inventory.cs ([GlobalClass] Resource), Hotbar.cs, HotbarSlot.cs
│   │   ├── items/                Item.cs, ItemData.cs ([GlobalClass] Resource),
│   │   │                         ItemStack.cs, EquipableItem.cs
│   │   ├── input/                keyboard now, Arduino later, same interface  (planned, empty)
│   │   └── minigames/            shared minigame contract  (planned, empty)
│   ├── ui/
│   │   ├── Main_menu/            MainMenu.cs, OptionsMenu.cs, VolumeMenu.cs, ControlsMenu.cs
│   │   └── ...                   HotbarUI.cs, ItemButton.cs, button_1.tscn
│   ├── data/                     Resource class definitions (schemas)  (planned, empty)
│   ├── Hotbar.tscn, hotbar_scene.tscn     early hotbar scenes
│   └── test_scene.tscn           scratch scene, misplaced under scripts/
│
├── data/                         .tres instances of those schemas
│   ├── equipment/items/          test_item.tres
│   ├── equipment/                (rest planned, empty)
│   └── mission_parameters/ events/ failures/                (planned, empty)
│
├── art/
│   ├── player/                   walk/idle sprites, inventory/ hotbar art
│   ├── rover/                    rover + crash sprites, mars_ground, obstacles, track
│   ├── trainning/16x16/          interior/room tilesets (folder name typo)
│   ├── cinematics/ gateway/ mars/ spacecraft/ ui/           (planned, empty)
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
| World | `scripts/player`, `scripts/rover`, `scripts/training`, `scripts/stages`, `scenes/intro`, `scenes/rover`, and the Training/Spacecraft/Mars/Rover/Gateway scenes |  |
| Shell + UI | `scenes/main_menu`, `scenes/ending`, `scenes/credits`, `scenes/ui`, `scripts/ui` |  |
| Art | `art/`, `art/fonts`, plus assembling their own tilemap scenes | Niki + Pasaye + Eva |
| Audio | `audio/` | Sergio + Macqro |

## Credits

Third-party assets and licenses are tracked in [`credits.md`](credits.md). The optional Arduino controller notes live in [`hardware/README.md`](hardware/README.md).
