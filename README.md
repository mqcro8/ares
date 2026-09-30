# Mission: Ares

## Nasa Space Apps Challenge 2026

We will (probably) participate in the Space Mission Design Game challenge

## Architecture
```
ares/
├── project.godot, README.md
├── credits.md                    asset + license ledger (M0 task)
│
├── scenes/                       one folder per stage
│   ├── main_menu/ intro/ training/ mission_planning/
│   ├── spacecraft/ transit_failure/ mars/ rover/ gateway/
│   ├── ending/ credits/
│   ├── minigames/                puzzle scenes that stages launch
│   └── ui/                       dialogue overlay, HUD, failure screen
│
├── scripts/
│   ├── player/  rover/
│   ├── stages/                   one controller script per stage scene
│   ├── mission/                  MissionState (the only autoload), MissionStage
│   ├── resource_manager/         loadout rules + consequences, holds no state
│   ├── systems/
│   │   ├── minigames/            shared minigame contract
│   │   └── input/                keyboard now, Arduino later, same interface
│   ├── ui/
│   └── data/                     Resource class definitions (schemas)
│
├── data/                         .tres instances of those schemas
│   ├── equipment/  mission_parameters/  events/
│   └── failures/                 one per failure cause + advice line
│
├── art/
│   ├── player/ rover/ mars/ spacecraft/ gateway/
│   ├── cinematics/               parallax backdrops
│   └── ui/                       retro-NASA panels, mono font
│
├── audio/  (music/ sfx/)         
└── hardware/                     optional: Arduino firmware, .gdignore
```
## Contributions

| Workstream | Folders | Suggested owner |
| --- | --- | --- |
| Core | `scripts/mission`, `resource_manager`, `scripts/data`, `data/equipment`, `scenes/mission_planning` |  |
| Minigames + input | `scripts/systems`, `scenes/minigames`, `data/failures` |  |
| World | `scripts/player`, `rover`, `stages`, and the Training/Spacecraft/Mars/Rover/Gateway scenes |  |
| Shell + UI | main menu, intro, ending, credits, `scenes/ui`, `scripts/ui` |  |
| Art | `art/`, plus assembling their own tilemap scenes | Niki + Pasaye + Eva |
| Audio | `audio/` | Sergio + Macqro |