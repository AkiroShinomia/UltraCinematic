# UltraCinematic

Personal BepInEx 5 mod for a deterministic cinematic flight of ULTRAKILL's existing `Player/MainCamera`.

## Build

Install the .NET Framework 4.7.2 developer pack and build with the ULTRAKILL directory supplied either as an environment variable or an MSBuild property:

```powershell
$env:ULTRAKILL_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL'
dotnet build -c Release
```

Alternatively: `dotnet build -c Release -p:GameDir="D:\Games\ULTRAKILL"`.

Copy only `bin\Release\net472\UltraCinematic.dll` to `BepInEx\plugins\UltraCinematic\UltraCinematic.dll`.

## Use

Open ULTRAKILL's Cheat Menu and use the **CINEMATIC** category:

1. Enter **Cinematic Edit Mode**.
2. Move the existing player camera normally or with ULTRAKILL's own Noclip cheat.
3. Add at least two **Camera Points**; use **Delete Last Point** to undo the latest one.
4. Open the timeline to adjust total Flight Time, path and easing, preview with the cursor, then start the cinematic.

Edit Mode only exposes the cinematic tools. It does not enable Noclip, hide the HUD, freeze the player, or take over the camera. Player and camera takeover occurs only during playback.

While Edit Mode is enabled, all four cinematic tools report an enabled state so their assigned binds remain visible in ULTRAKILL's active-cheats HUD. Disabling Edit Mode disables all four tools again.

Camera Points are numbered from 1 and display a forward-expanding view cone. Segments are lettered from A and show their label above the world-space trail. Each point exposes editable position, rotation and FOV values with live preview. The Timeline has one total Flight Time; segment timing is derived automatically from sampled curve length, and arc-length mapping keeps the base route speed uniform. Each segment retains its independent path type (`Linear`, auto-tangent `Bezier`, or continuous `Smooth`) and easing mode. Optional Soft Points work across every Path combination and expose independent 1–45% windows before and after each internal point. Their quintic position curve preserves continuous velocity and acceleration instead of forcing the camera through an exact position at an exact frame. The first and last points remain exact.

Opening Timeline pauses the game and locks player/camera input. Dragging across its track temporarily moves both player and camera through the evaluated cinematic pose; releasing the mouse restores their exact pre-preview pose.

Selecting a numbered point opens exact world Position X/Y/Z and Rotation Pitch/Yaw/Roll fields. Values support exact text entry and horizontal drag adjustment. `PREVIEW POINT` temporarily moves player and camera to the edited point until `RETURN` or Timeline close.

`CINEMATIC SETTINGS` selects `LIVE WORLD` or `FROZEN WORLD` playback. Frozen playback advances with unscaled time while gameplay, physics, particles, and projectiles remain paused.

`Pause Game` is a true `DISABLED`/`ENABLED` toggle. It freezes game time and enables a temporary unscaled photo-mode controller: mouse to look, WASD to move, Space/Ctrl for vertical movement, and Shift for faster movement. Disabling it removes the custom controller and restores normal gameplay input. Cinematic Playback started while Pause Game is active temporarily suspends that mode and restores the original pose, camera, FOV, and paused-time state immediately after automatic completion or manual stop.

Timeline projects can be saved by name, loaded, overwritten, and deleted from the Timeline header. A project contains all points, transforms, FOV values, segment modes, total Flight Time, playback mode, and Soft Point settings. Saves are stored as JSON under `BepInEx/config/UltraCinematic/Timelines` and are cryptographically partitioned by active scene identity; the load menu never exposes projects belonging to a different level. Loading validates the entire file before replacing the current in-memory project. `CLEAR` restores Timeline defaults only after confirmation.
