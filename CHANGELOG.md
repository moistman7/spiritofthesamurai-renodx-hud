# Changelog

## 1.0.0
- Initial standalone release, split out of the Ultrawide plugin's 1.7.x experimental HUD and
  vignette features.
- Separate HUD rendering (F7): UI/Canvas is moved to an unused layer and drawn by a second base
  camera onto the scene camera's output, so RenoDX UI Brightness affects only the HUD.
  Scene pixels are unchanged with it on or off (checked in one level at two UI brightness values).
- Vignette removal (F8) for `UI/Canvas/vignette`.
- Imports `SeparateHud` / `DisableVignette` from the ultrawide config on first run.
