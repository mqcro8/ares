Project for the Nasa Space Apps Challenge 

We are making a game

Project architecture:

ares/
├── project.godot, README.md
├── credits.md                    asset + license ledger (M0 task)
│
├── scenes/                       one folder per stage
│   ├── main_menu/ intro/ training/ mission_planning/
│   ├── spacecraft/ transit_failure/ mars/ rover/ gateway/
│   ├── ending/ credits/
│   ├── minigames/                NEW: puzzle scenes that stages launch
│   └── ui/                       NEW: dialogue overlay, HUD, failure screen
│
├── scripts/
│   ├── player/  rover/
│   ├── stages/                   NEW: one controller script per stage scene
│   ├── mission/                  MissionState (the only autoload), MissionStage
│   ├── resource_manager/         loadout rules + consequences, holds no state
│   ├── systems/
│   │   ├── minigames/            shared minigame contract
│   │   └── input/                keyboard now, Arduino later, same interface
│   ├── ui/
│   └── data/                     NEW: Resource class definitions (schemas)
│
├── data/                         .tres instances of those schemas
│   ├── equipment/  mission_parameters/  events/
│   └── failures/                 NEW: one per failure cause + advice line
│
├── art/
│   ├── player/ rover/ mars/ spacecraft/ gateway/
│   ├── cinematics/               NEW: parallax backdrops
│   └── ui/                       NEW: retro-NASA panels, mono font
│
├── audio/  (music/ sfx/)         NEW
└── hardware/                     NEW, optional: Arduino firmware, .gdignore