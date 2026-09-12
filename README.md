# RopedTogether for PEAK

RopedTogether lets climbers clip directly to one another and use the link as a dynamic belay. The rope is intentionally implemented as networking + physics + visuals, not as an inventory item, so the rope and connector markers add no item weight and do not call PEAK's stamina systems.

## Controls

- `F`: clip to / unclip from the climber you are aiming at.
- `Shift + F`: disconnect every rope attached to you.
- Gamepad default: `RB` toggles; `LB + RB` disconnects all.

All bindings are configurable in the generated BepInEx config.

## Behavior

- New links can be created within 4 m by default.
- Rope is slack until 6 m by default.
- Once taut, spring + damping forces arrest separation.
- If you are falling and a linked partner is above you, the rope applies stronger arrest assistance plus a capped upward rescue component.
- A stable climber receives only a fraction of the downward counter-pull from a falling partner, making belays useful instead of simply dragging both players off the wall.
- Multiple links are supported, but total assistance is capped to avoid multi-rope launch behavior.
- Rope and endpoint connectors are visual-only: no Rigidbody, no active collider, no inventory slot, no stamina drain.

## Multiplayer

All players should install the mod. Remove/disable the separate `PeakRopes` plugin if present; RopedTogether declares it incompatible to prevent double rope forces. Rope link state is synchronized with Photon buffered RPCs, while each client applies force only to its own local climber. This keeps movement authority local and makes the assist symmetric when everyone has the plugin.

## Build

Requirements:

1. PEAK installed locally.
2. BepInExPack_PEAK installed in the PEAK directory.
3. .NET SDK 10 or newer (the current PEAK modding template recommends SDK 10+).

Set the game path in one of two ways:

- Copy `PeakGameDir.props.example` to `PeakGameDir.props` and edit the path, or
- Set the `PEAK_GAME_DIR` environment variable.

Then build:

```powershell
dotnet build .\src\RopedTogether\RopedTogether.csproj -c Release
```

The DLL will be at:

```text
src\RopedTogether\bin\Release\RopedTogether.dll
```

To build and copy directly into PEAK's BepInEx plugins folder:

```powershell
dotnet msbuild .\src\RopedTogether\RopedTogether.csproj -t:Deploy -p:Configuration=Release
```

## Install

Copy `RopedTogether.dll` into:

```text
PEAK\BepInEx\plugins\RopedTogether\
```

Launch PEAK. The config file is created automatically under `BepInEx\config`.

## Important compatibility note

This source targets the current BepInEx 5 / Unity 6 PEAK modding layout and references the game's local managed assemblies. PEAK updates can rename internal classes or members; if a future update breaks the mod, rebuild against the updated game assemblies and adjust any changed `Character` API names.
