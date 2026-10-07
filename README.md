# Mission: Ares

## Nasa Space Apps Challenge 2026

We will (probably) participate in the Space Mission Design Game challenge

## Architecture
```
ares/
├── project.godot, ares.csproj, ares.sln, README.md
├── credits.md                    asset + license ledger (M0 task)
│
├── scenes/                       one folder per stage
│   ├── main_menu/                main_menu_take3.tscn (main scene), Options.tscn,
│   │                             Volume_menu.tscn, Control_menu.tscn
│   ├── intro/ training/ mission_planning/            (empty)
│   ├── spacecraft/ transit_failure/ mars/ rover/ gateway/   (empty)
│   ├── ending/ credits/                           (empty)
│   ├── minigames/                puzzle scenes that stages launch  (empty)
│   └── ui/                       dialogue overlay, HUD, failure screen  (empty)
│
├── scripts/                      C# (.cs); .cs.uid sidecars are Godot-generated
│   ├── player/                   Player.cs (CharacterBody2D, reads Input directly)
│   ├── rover/                    (empty)
│   ├── stages/                   one controller script per stage scene  (empty)
│   ├── mission/                  MissionState.cs, MissionStage.cs
│   ├── resource_manager/         loadout rules + consequences, holds no state  (empty)
│   ├── systems/
│   │   ├── minigames/            shared minigame contract  (empty)
│   │   ├── input/                keyboard now, Arduino later, same interface  (empty)
│   │   ├── items/                ItemData.cs ([GlobalClass] Resource), ItemStack.cs
│   │   ├── inventory/            Inventory.cs ([GlobalClass] Resource, slot array)
│   │   └── audio/                MusicPlayer.cs (autoload, test music only)
│   ├── ui/
│   │   └── Main_menu/            MainMenu.cs, OptionsMenu.cs,
│   │                             VolumeMenu.cs, ControlsMenu.cs
│   ├── data/                     Resource class definitions (schemas)  (empty)
│   └── test_scene.tscn           scratch scene, misplaced under scripts/
│
├── data/                         .tres instances of those schemas
│   ├── equipment/  mission_parameters/  events/       (empty)
│   └── failures/                 one per failure cause + advice line  (empty)
│
├── art/
│   ├── player/ rover/ mars/ spacecraft/ gateway/      (empty)
│   ├── cinematics/               parallax backdrops  (empty)
│   ├── fonts/                    Aerospace.ttf, PressStart2P-Regular.ttf
│   ├── ui/                       retro-NASA panels  (empty)
│   ├── menu_placeholder/         placeholder.jpeg
│   └── tmp/                      scratch art, unsorted: meowl.png, ominous_red_ball.png
│
├── audio/  (music/ sfx/)         music/menu_test_music.mp3 (test), sfx/ (empty)
└── hardware/                     optional: Arduino firmware, .gdignore
```
## Contributions

| Workstream | Folders | People |
| --- | --- | --- |
| Core | `scripts/mission`, `scripts/resource_manager`, `scripts/data`, `data/equipment`, `data/mission_parameters`, `scenes/mission_planning` |  |
| Minigames + input | `scripts/systems/minigames`, `scripts/systems/input`, `scripts/systems/items`, `scripts/systems/inventory`, `scenes/minigames`, `data/failures` |  |
| World | `scripts/player`, `scripts/rover`, `scripts/stages`, and the Training/Spacecraft/Mars/Rover/Gateway scenes |  |
| Shell + UI | `scenes/main_menu`, `scenes/intro`, `scenes/ending`, `scenes/credits`, `scenes/ui`, `scripts/ui` |  |
| Art | `art/`, `art/fonts`, plus assembling their own tilemap scenes | Niki + Pasaye + Eva |
| Audio | `audio/` | Sergio + Macqro |
