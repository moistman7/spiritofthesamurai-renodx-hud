# The Spirit of the Samurai — RenoDX HUD

The HDR HUD part of RenoDX support for *The Spirit of the Samurai* (Windows, Unity/Mono). RenoDX
([renodx-unity](https://github.com/NotVoosh/renodx-unity)) is how the game gets HDR; this BepInEx 5
plugin makes its HUD and vignette controls work in this game. It is independent of the ultrawide
plugin.

> Unofficial community mod. It contains no game files. Use at your own risk.

## What it does

- **Separate HUD rendering (F7).** The game draws its HUD into the scene before post-processing,
  so RenoDX's *UI Brightness* slider has no visible effect on it. This plugin draws UI/Canvas with a
  second camera after post-processing, so the slider applies to the HUD and the scene is unchanged.
  Experimental.
- **Vignette removal (F8).** Hides the game's own `UI/Canvas/vignette` sprite, which RenoDX's
  Vignette slider does not reach. The damage and fatigue overlays are left alone. This works without
  RenoDX too.

Both are **off** on a fresh install (the HUD one is still experimental). Each hotkey toggles its
feature and saves the choice.

## Requirements

- The Spirit of the Samurai, Windows x64 (tested on Unity 2021.3.45, URP 2D renderer).
- [BepInEx 5.4.23.5 x64 (Mono)](https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5).
- For HUD brightness: ReShade with add-on support and the generic Unity RenoDX addon.

## Install

1. Close the game and install BepInEx 5 x64 into the game folder (where `samurai.exe` is).
2. Copy `SpiritOfTheSamuraiRenoDXHud.dll` to `BepInEx/plugins/`.
3. Launch the game. `BepInEx/LogOutput.log` should contain `Loading [Spirit of the Samurai RenoDX HUD 1.0.0]`.
4. In a gameplay scene, press F7 (HUD) and/or F8 (vignette). In the log you should see
   `HUD composition active: ...` after F7.

To uninstall, delete the DLL.

## Settings

`BepInEx/config/renodx.spiritofthesamurai.hdrhud.cfg`:

    [HUD]
    SeparateHud = false
    [Vignette]
    DisableVignette = false

On first run the plugin copies both values from `renodx.spiritofthesamurai.ultrawide.cfg` if it
exists, so moving over from ultrawide 1.7.x keeps your settings.

## Using it with the ultrawide plugin

Use the Ultrawide plugin **1.8.1 or later** (1.8.0 also works but leaves the damage border at 16:9
width when the HUD is separate). Ultrawide 1.7.x has its own copy of these two features; if it is
installed, this plugin logs a warning and stays inactive so they don't fight over the same objects.

## Known limitations

- Applies to everything under UI/Canvas (menus, fades, video), but only the gameplay HUD and
  cutscenes/menus in one level have been visually checked. Other levels are untested.
- It refuses to attach if the scene camera is not a URP base camera, or if no unused layer exists.
- RenoDX's UI Brightness is applied by the addon; this plugin does not add its own tonemapper.
- A game, URP or RenoDX update can change what is patched. Press F7 (or set `SeparateHud = false`)
  to return to the original composition.

## How it works

While enabled, everything under UI/Canvas is moved to an unused layer. The scene camera drops that
layer from its culling mask; a second base camera renders only that layer straight onto the scene
camera's output, with post-processing off. Two small Harmony patches keep URP from giving that camera
its own 8-bit target (which would clip the scene) and from post-processing it. Layers are restored
when you press F7 again. Everything is in `HudComposition.cs`.

## Build

Needs the .NET SDK, the game, and BepInEx 5 installed (the project references their DLLs).

    dotnet build -c Release -p:GameDir="<path to the game folder>"

or set the `SPIRIT_OF_THE_SAMURAI_DIR` environment variable. Add `-p:SkipDeploy=true` to skip copying
the DLL into `BepInEx/plugins`.

## Credits and license

Built on [BepInEx](https://github.com/BepInEx/BepInEx), [Harmony](https://github.com/pardeike/Harmony)
and made to work with [renodx-unity](https://github.com/NotVoosh/renodx-unity). MIT licensed, see
`LICENSE`. *The Spirit of the Samurai* belongs to its developers.
