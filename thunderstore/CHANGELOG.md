# Changelog

## 1.6.1

- Camera Points and the current Timeline now survive player death and same-level reloads.
- Timeline data is cleared only when leaving the current level.
- Restored player input safely while Unity rebuilds the level after death or restart.

## 1.6.0

- Added reusable global route presets positioned relative to the player.
- Added `Move All` controls for translating the complete route.
- Added insertion before the first point and inside any segment.
- Added deletion of the selected point.
- Added collapsible panels and scrolling to the Timeline editor.
- Replaced the Timeline close label with an `X` button.
- Blocked gameplay input while the Timeline is open.
- Removed the ineffective per-segment smoothing controls and simplified the route model.
- Improved visualizer and Timeline performance.
