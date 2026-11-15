# Audio — Music & SFX

## P0 mechanical feedback (CC0)

The additive P0 bank uses six small WAV derivatives of three Joseph SARDIN / BigSoundBank sources.
`data/audio/sources.json` records source pages, original BWF URLs, SHA-256 hashes, edit filters,
license and the exact runtime ID/file mapping. `credits.md` contains human-readable credits.
Existing audio has not been overwritten or relicensed; its provenance still needs auditing.
The new files have been technically inspected, not listened to. Human audition remains required.

- UI: `ui_navigate`, `ui_confirm`, `ui_back`, `ui_denied`.
- Panels/equipment: `panel_open`, `panel_close`, `equipment_take`, `equipment_store`.
- Repairs: `connector_snap`, `electrical_fault`, `relay_engage`, `objective_complete`.
- `FeedbackSounds` is owner-local: normally 100 ms between matching cues, 300 ms for rejection.
  Sound throttling never discards gameplay inputs. Equipment feedback compares actual state before/after.
- `UiNavigationSounds` binds navigation only, deduplicates hover/focus and disconnects on exit.
  Action owners select confirmation/rejection after checking their result. Disabled buttons stay silent.
- `AudioManager.PlayUi(id)` uses the existing non-positional SFX pool and processes during pause.
  Normal `PlaySfx` resets the reused voice to inherited pause behavior. No new bus or music policy.
- Wires and fuses use the same paths for mouse, keyboard and phone commands. No new loops.
  Fuse reset uses the short electrical interruption cue in this first batch; dedicated breaker is deferred.
  Objective completion uses two recorded switch clicks, not a musical reward or stage-complete fanfare.
- Panel sounds cover equipment and wire/fuse practices/overlays. Other minigames remain unchanged.

Verification: `dotnet build --no-restore`, then import with Godot .NET and run
`godot --headless --path . res://tests/AudioFeedbackTest.tscn`.
Also run `LoadoutTest.tscn`, `TrainingPracticeTest.tscn`, `GatewayMissionTest.tscn`
and `ProjectConsistencyTest.tscn`. Headless checks do not validate perceptual mix quality.
For manual audition: test volume/mute, pause navigation, rejected/full loadout, rapid inputs,
wrong/cancelled wire drops, fuse reset/solve, and repeated scene exits. Listen for harshness,
clipping and excessive repetition. Keep only used derivatives in Git; do not add full libraries.

`AudioManager` is an autoload. It loads sounds from `data/audio/sound_bank.tres`, plays music and SFX, and routes everything to the existing `Music` / `SFX` buses. You do **not** need to add `AudioStreamPlayer` nodes to your scene for normal one-shot sounds.

The Master / Music / SFX volume sliders already control the output.

## TL;DR

```csharp
using Ares.Systems.Audio;

// Music (survives scene changes, crossfades if the track changes)
AudioManager.PlayMusic(SoundIds.MenuMusic);

// One-shot sound, any scene
AudioManager.PlaySfx(SoundIds.RoverCollision);

// Stop the music with a fade
AudioManager.StopMusic(0.5);
```

## Music vs SFX

| | Music | SFX |
|---|---|---|
| Method | `PlayMusic(id)` | `PlaySfx(id)` |
| Concurrent | one track | many |
| Looping | `Loop` on the `SoundDef` | one-shots only |
| Use for | menus, stages, ambience | impacts, UI, explosions |

`PlayMusic` with the ID that is already playing does nothing. This is intentional: every menu scene can call `PlayMusic(SoundIds.MenuMusic)` without restarting the track.

## API

### Music

```csharp
AudioManager.PlayMusic(string id, double fadeSeconds = 1.0, bool restart = false);
AudioManager.StopMusic(double fadeSeconds = 0.5);
```

`AudioManager.CurrentMusicId` returns the active track ID, or `null` when stopped.

### One-shot SFX (non-positional)

```csharp
AudioManager.PlaySfx(string id, float volumeDb = 0f, float pitchScale = 1f);
AudioManager.StopAllSfx();
```

`PlaySfx` uses a 16-voice pool. If every voice is busy, the oldest one is reused. `StopAllSfx` stops pooled one-shots, not scene emitters.

### Positional one-shots

```csharp
AudioManager.PlaySfx2D(string id, Vector2 globalPosition, float volumeDb = 0f, float pitchScale = 1f);
AudioManager.PlaySfx3D(string id, Vector3 globalPosition, float volumeDb = 0f, float pitchScale = 1f);
```

The 2D and 3D versions use their own 16-voice pools. For custom falloff/attenuation, use an emitter node. 3D positioning needs an active `Camera3D` or `AudioListener3D`.

### Looping / attached sounds

Add `SfxEmitter2D` (or `SfxEmitter3D`) as a child of the node that should emit the sound.

Inspector:

- `Sound Id` — e.g. `rover_moving`
- `Play On Ready` — start when the scene starts
- `Volume Db Offset` — local trim on top of the `SoundDef` volume

```csharp
var engine = GetNode<SfxEmitter2D>("EngineLoop");
engine.Play();                 // start or restart
engine.Stop(fadeSeconds: 0.2); // fade out and stop
```

The emitter follows its parent automatically. Use it for engine hums, wind, alarms, and machinery. For a one-shot, use `PlaySfx2D` / `PlaySfx3D` instead.

`SfxEmitter2D` / `SfxEmitter3D` also expose `PlaySound()` (same as `Play()`) and `Stop(float fadeSeconds)`.

## Adding a new sound

1. Put the file in `audio/music/` or `audio/sfx/`.
2. Open `data/audio/sound_bank.tres` and add a `SoundDef`:
   - `Id` — the name used in code, snake_case, e.g. `rover_collision`.
   - `Stream` — drag the audio file.
   - `Volume Db` — per-sound trim, usually `0`.
   - `Pitch Variation` — e.g. `0.05` for impacts; `0` for music.
   - `Loop` — on for music, ambience, and continuous emitters; off for one-shots.
3. (Recommended) Add a constant to `SoundIds.cs`:

   ```csharp
   public const string RoverCollision = "rover_collision";
   ```

4. Call it:

   ```csharp
   AudioManager.PlaySfx(SoundIds.RoverCollision);
   ```

For variations (footsteps, repeated impacts), assign an `AudioStreamRandomizer` to `Stream` instead of a single file. `Loop` is applied to the randomizer's child streams automatically.

## Recipes

### Menu music

Add this to each menu scene you can launch directly (main menu, options, volume, controls):

```csharp
public override void _Ready()
{
    AudioManager.PlayMusic(SoundIds.MenuMusic);
}
```

Because the manager ignores the same ID, navigating between menus does not restart the song.

### Collision

```csharp
private void OnCollision()
{
    AudioManager.PlaySfx2D(SoundIds.RoverCollision, GlobalPosition);
}
```

Use `PlaySfx` instead if the sound does not need a position.

### Game over

```csharp
AudioManager.StopMusic(fadeSeconds: 0.75);
AudioManager.PlaySfx(SoundIds.RoverExplode);
```

### Looping rover engine

1. Add `SfxEmitter2D` as a child of the rover.
2. Set `Sound Id = rover_moving`, `Play On Ready = true`.
3. Set `Loop = true` on the `rover_moving` entry in the sound bank.
4. Stop it when the rover is destroyed:

   ```csharp
   _engine.Stop(fadeSeconds: 0.2);
   ```

### Positional 3D sound

```csharp
AudioManager.PlaySfx3D(SoundIds.DoorOpen, GlobalPosition);
```

For custom attenuation, add an `SfxEmitter3D` to the scene and configure `Unit Size`, `Max Distance`, and `Attenuation Model` in the inspector.

## Rules

- **Do not add your own `AudioStreamPlayer` for normal one-shots.** Use `AudioManager` so volumes, pooling, and fades stay consistent.
- **Do not change bus volumes from gameplay code.** `VolumeMenu` owns `Master` / `Music` / `SFX`.
- **Do not use `PlaySfx` for looping sounds.** A looping voice never frees its pool slot. Use `SfxEmitter2D` / `SfxEmitter3D`.
- **Do not call audio from `_EnterTree`.** The manager builds its players in `_Ready`; use `_Ready` or later.
- **Music is global.** It survives scene changes by design. If a scene needs silence, call `AudioManager.StopMusic()`.
- **One-shots are short.** If a long sound must stop when leaving a scene, stop its emitter or call `StopAllSfx()` in `_ExitTree`.
- **Unknown IDs log a warning once and do nothing.** Check the spelling against `sound_bank.tres`.

## Troubleshooting

| Problem | Check |
|---|---|
| No sound | ID exists in `sound_bank.tres`? File assigned? Bus not muted in the volume menu? |
| Music restarts | Is the ID different? Different IDs crossfade. Same ID is a no-op. |
| Music continues into gameplay | Call `AudioManager.StopMusic()` (or start the gameplay track) in the new scene. |
| Sound loops forever | `Loop` is on for a one-shot. Turn it off in the `SoundDef`, or use an emitter you can stop. |
| No 3D positioning | Use `PlaySfx2D` / `PlaySfx3D` or an emitter, not `PlaySfx`. |
| Too loud/quiet | Adjust `Volume Db` in the `SoundDef`, not the bus. |

## File map

```text
scripts/systems/audio/AudioManager.cs   // autoload
scripts/systems/audio/SoundDef.cs       // one sound entry
scripts/systems/audio/SoundBank.cs      // catalog resource
scripts/systems/audio/SoundIds.cs       // string constants
scripts/systems/audio/SfxEmitter2D.cs   // looping/positional helper
scripts/systems/audio/SfxEmitter3D.cs   // looping/positional helper
data/audio/sound_bank.tres              // the catalog
audio/music/                            // music + ambience
audio/sfx/                              // one-shots
docs/audio.md                           // this guide
```
