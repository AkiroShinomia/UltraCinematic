# UltraCinematic

**Русский** · [English below](#english)

UltraCinematic — инструмент для создания плавных кинематографических пролётов в ULTRAKILL. Мод использует штатную камеру игрока и полностью встраивается в `MANAGE CHEATS` отдельной категорией **CINEMATIC**.

## Возможности

- создание, вставка, удаление и перемещение Camera Points прямо в игре;
- наглядная визуализация точек, направления камеры и маршрута;
- Timeline с прокруткой, предпросмотром кадров и редактированием точек;
- настройка Position X/Y/Z, Rotation Pitch/Yaw/Roll и FOV;
- режимы пути `Linear`, `Bezier` и `Smooth`;
- единое время пролёта и равномерное движение по длине маршрута;
- Soft Points для плавного прохождения промежуточных точек;
- воспроизведение в работающем или полностью замороженном мире;
- `Pause Game` со свободным перемещением камеры при остановленном времени;
- сохранения, привязанные к уровню, и глобальные пресеты маршрутов;
- русский и английский интерфейс, стили `Classic` и `Dark`;
- настраиваемая папка сохранений;
- точки сохраняются после смерти или перезапуска текущего уровня и очищаются только после выхода с него.

## Быстрый старт

1. Включите игровые читы ULTRAKILL.
2. Откройте `MANAGE CHEATS` и найдите категорию **CINEMATIC**.
3. Включите **Cinematic Edit Mode**.
4. Перемещайтесь обычным способом или включите штатный Noclip и добавляйте **Camera Points**.
5. Откройте **Timeline**, настройте точки, путь, общее время и режим мира.
6. Проверьте маршрут ползунком Timeline или кнопкой `PREVIEW POINT`.
7. Запустите **Start Cinematic**.

Edit Mode не включает Noclip, не скрывает HUD и не меняет оружие: этими штатными читами пользователь управляет самостоятельно.

## Сохранения и пресеты

`SAVE AS` сохраняет текущий маршрут как проект уровня или как глобальный пресет. Проекты доступны только на той карте, где были созданы. Пресеты можно загрузить на любом уровне — маршрут размещается относительно текущей позиции игрока.

По умолчанию файлы находятся в:

```text
BepInEx/config/UltraCinematic/Timelines
```

Папку можно изменить в настройках Timeline.

## Установка

### Thunderstore Mod Manager / r2modman

Установите UltraCinematic через менеджер. Требуемая версия BepInEx установится автоматически.

### Вручную

1. Установите BepInEx 5 для ULTRAKILL.
2. Скопируйте `UltraCinematic.dll` в:

```text
ULTRAKILL/BepInEx/plugins/UltraCinematic/UltraCinematic.dll
```

---

## English

UltraCinematic is an in-game tool for creating smooth cinematic camera flights in ULTRAKILL. It uses the existing player camera and integrates directly into `MANAGE CHEATS` under a dedicated **CINEMATIC** category.

## Features

- create, insert, delete, and move Camera Points in game;
- visualize points, camera direction, and the complete route;
- scrollable Timeline with frame scrubbing and point editing;
- edit Position X/Y/Z, Rotation Pitch/Yaw/Roll, and FOV;
- `Linear`, `Bezier`, and `Smooth` path modes;
- one total flight time with uniform movement over route length;
- Soft Points for smooth passage through internal points;
- playback in a live or completely frozen world;
- `Pause Game` with independent free-camera movement on frozen time;
- level-specific saves and reusable global route presets;
- complete English and Russian interfaces with `Classic` and `Dark` styles;
- configurable save directory;
- Camera Points survive death or a restart of the current level and are cleared only after leaving it.

## Quick start

1. Enable ULTRAKILL cheats.
2. Open `MANAGE CHEATS` and find the **CINEMATIC** category.
3. Enable **Cinematic Edit Mode**.
4. Move normally or enable ULTRAKILL's own Noclip, then add **Camera Points**.
5. Open the **Timeline** and configure points, paths, total duration, and world mode.
6. Inspect the route with the Timeline cursor or `PREVIEW POINT`.
7. Run **Start Cinematic**.

Edit Mode does not enable Noclip, hide the HUD, or alter weapons. Those standard cheats remain under the user's control.

## Saves and presets

`SAVE AS` stores the current route either as a level project or as a global preset. Level projects are available only on the map where they were created. Presets can be loaded anywhere and are positioned relative to the player's current location.

The default save location is:

```text
BepInEx/config/UltraCinematic/Timelines
```

The directory can be changed from Timeline settings.

## Installation

### Thunderstore Mod Manager / r2modman

Install UltraCinematic through the manager. The required BepInEx version will be installed automatically.

### Manual

1. Install BepInEx 5 for ULTRAKILL.
2. Copy `UltraCinematic.dll` to:

```text
ULTRAKILL/BepInEx/plugins/UltraCinematic/UltraCinematic.dll
```

Source code and issue tracker: [GitHub](https://github.com/AkiroShinomia/UltraCinematic)
