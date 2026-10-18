# Changelog

## 1.1.0 — 2026-10-18
- Removed the vignette toggle (F8, `[Vignette] DisableVignette`); it now lives in the Ultrawide plugin.
  Old config entries are ignored.

## 1.0.0
- Initial release.
- Separate HUD rendering (F7): UI/Canvas is moved to an unused layer and drawn by a second base
  camera onto the scene camera's output, so RenoDX UI Brightness affects only the HUD.
  Scene pixels are unchanged with it on or off (checked in one level at two UI brightness values).
- Vignette removal (F8) for `UI/Canvas/vignette`.
