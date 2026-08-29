# UltraCinematic

[Русский](#русский) · [English](#english)

## Русский

**UltraCinematic** — мод для ULTRAKILL на BepInEx 5, предназначенный для создания плавных кинематографических пролётов штатной камерой игрока. Все инструменты встроены в стандартное меню `MANAGE CHEATS` в категорию **CINEMATIC**.

### Возможности

- создание и удаление Camera Points прямо во время игры;
- визуализация точек, направления камеры и маршрута в мире;
- Timeline с предпросмотром любого кадра;
- редактирование Position X/Y/Z, Rotation Pitch/Yaw/Roll и FOV каждой точки;
- индивидуальные `Linear`, `Bezier` и `Smooth` Path для сегментов;
- индивидуальные режимы Easing;
- единое Flight Time для всего маршрута и автоматическое распределение времени по измеренной длине кривой;
- Soft Points с настраиваемыми окнами до и после внутренних точек;
- воспроизведение в работающем или полностью замороженном мире;
- `Pause Game` с отдельным свободным перемещением камеры на остановленном времени;
- именованные сохранения Timeline, привязанные к конкретному уровню;
- загрузка, перезапись, удаление и безопасная очистка проектов.

### Использование

1. Включите игровые читы ULTRAKILL.
2. Откройте `MANAGE CHEATS` и найдите категорию **CINEMATIC**.
3. Включите **Cinematic Edit Mode**.
4. Перемещайте игрока обычным способом или штатным Noclip и добавляйте **Camera Points**.
5. Откройте **Timeline**, настройте точки, сегменты, общее время и параметры пролётки.
6. Проверьте маршрут с помощью ползунка или `PREVIEW POINT`.
7. Запустите **Start Cinematic**.

Edit Mode не включает Noclip, не скрывает HUD и не меняет оружие — этими штатными функциями ULTRAKILL пользователь управляет самостоятельно.

### Timeline и плавность

Точки отображаются кружками и нумеруются с 1, а сегменты обозначаются буквами начиная с A. Время отдельных сегментов рассчитывается автоматически по фактической длине кривой, поэтому при `Easing: Linear` базовая пространственная скорость остаётся равномерной.

Soft Points работают с любым сочетанием Path. Для внутренних точек можно задать процент сглаживания до и после точки в диапазоне 1–45%. Переход строится кривой пятой степени с непрерывными скоростью и ускорением. Первая и последняя точки всегда остаются точными.

### Режимы времени

- `LIVE WORLD` — мир продолжает работать во время пролётки.
- `FROZEN WORLD` — мир, физика, частицы и снаряды остановлены, а пролётка идёт по unscaled time.
- `Pause Game` — останавливает время и включает свободное управление: мышь, WASD, Space/Ctrl по вертикали и Shift для ускорения.

Если пролётка была запущена из активного `Pause Game`, после её завершения или ручной остановки восстанавливаются исходные позиция, камера, FOV и замороженное состояние мира.

### Сохранения

Кнопки `SAVE`, `LOAD` и `CLEAR` находятся в верхней части Timeline. Проект сохраняет все точки, Position/Rotation/FOV, Path/Easing, Flight Time, режим времени и Soft Points.

Сейвы хранятся в:

```text
BepInEx/config/UltraCinematic/Timelines
```

Каждый проект привязан к идентификатору активного уровня. Сейвы другого уровня не отображаются и не могут быть загружены. Перед загрузкой файл полностью проверяется, и только затем текущий Timeline заменяется.

### Сборка и установка

Требуются .NET Framework 4.7.2 Developer Pack, BepInEx 5 и установленный ULTRAKILL.

```powershell
$env:ULTRAKILL_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL'
dotnet build -c Release
```

Либо передайте путь напрямую:

```powershell
dotnet build -c Release -p:GameDir="D:\Games\ULTRAKILL"
```

Скопируйте `bin/Release/net472/UltraCinematic.dll` в `ULTRAKILL/BepInEx/plugins/UltraCinematic/UltraCinematic.dll`.

---

## English

**UltraCinematic** is a BepInEx 5 mod for ULTRAKILL that creates smooth cinematic flights using the game's existing player camera. Every tool is integrated into the standard `MANAGE CHEATS` menu under the **CINEMATIC** category.

### Features

- create and remove Camera Points in game;
- visualize points, camera direction, and the complete route in world space;
- Timeline with live preview at any frame;
- edit Position X/Y/Z, Rotation Pitch/Yaw/Roll, and FOV for every point;
- independent `Linear`, `Bezier`, and `Smooth` Path modes per segment;
- independent Easing modes;
- one total Flight Time with automatic timing based on measured curve length;
- configurable Soft Point windows before and after internal points;
- playback in a live or completely frozen world;
- `Pause Game` with a separate free-camera controller on frozen time;
- named, level-specific Timeline saves;
- load, overwrite, delete, and safely clear projects.

### Usage

1. Enable ULTRAKILL cheats.
2. Open `MANAGE CHEATS` and locate the **CINEMATIC** category.
3. Enable **Cinematic Edit Mode**.
4. Move normally or use ULTRAKILL's own Noclip and add **Camera Points**.
5. Open the **Timeline** and configure points, segments, total time, and cinematic settings.
6. Inspect the route with the Timeline cursor or `PREVIEW POINT`.
7. Run **Start Cinematic**.

Edit Mode does not enable Noclip, hide the HUD, or modify weapons. Those remain under the user's control through ULTRAKILL's standard cheats.

### Timeline and smoothing

Points are circular and numbered from 1. Segments are lettered from A. Individual segment time is derived automatically from measured curve length, keeping the base spatial speed uniform when `Easing: Linear` is selected.

Soft Points work with every Path combination. Internal points expose independent 1–45% smoothing windows before and after the point. A quintic transition maintains continuous velocity and acceleration, while the first and last points always remain exact.

### Time modes

- `LIVE WORLD` keeps the world running during playback.
- `FROZEN WORLD` freezes gameplay, physics, particles, and projectiles while playback advances on unscaled time.
- `Pause Game` freezes time and enables free movement with mouse, WASD, Space/Ctrl vertically, and Shift for higher speed.

When playback starts from an active `Pause Game`, automatic completion and manual stop both restore the original pose, camera, FOV, and frozen-world state.

### Saves

The Timeline header contains `SAVE`, `LOAD`, and `CLEAR`. A project stores all points, Position/Rotation/FOV values, Path/Easing modes, Flight Time, time mode, and Soft Point settings.

Save files are stored under:

```text
BepInEx/config/UltraCinematic/Timelines
```

Each project is bound to the active level identity. Saves from another level are neither listed nor loadable. A file is fully validated before it can replace the current in-memory Timeline.

### Build and installation

.NET Framework 4.7.2 Developer Pack, BepInEx 5, and an installed copy of ULTRAKILL are required.

```powershell
$env:ULTRAKILL_DIR = 'C:\Program Files (x86)\Steam\steamapps\common\ULTRAKILL'
dotnet build -c Release
```

Alternatively, provide the game directory directly:

```powershell
dotnet build -c Release -p:GameDir="D:\Games\ULTRAKILL"
```

Copy `bin/Release/net472/UltraCinematic.dll` to `ULTRAKILL/BepInEx/plugins/UltraCinematic/UltraCinematic.dll`.
