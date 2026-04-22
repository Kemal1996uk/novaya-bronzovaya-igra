# Новая Бронзовая Игра — Контекст проекта для Claude

## Что это
Изометрический сити-билдер бронзового века. Godot 4.6, C#, 2D изометрия (DiamondDown 128×64).
Разрабатывается одним человеком (Kemal) с помощью Claude Code (вайбкодинг).

## Стек
- **Движок**: Godot 4.6
- **Язык**: C# (.NET)
- **Рендер**: Forward Plus
- **Тайлы**: TileMapLayer, изометрия DiamondDown, 128×64px
- **Навигация**: AStarGrid2D (NavigationManager.cs)

## Git — правила
- Репозиторий инициализирован: `git init` уже сделан
- **Stop-хук** в `.claude/settings.json` — автокоммит после каждой сессии
- Ветка: `main`
- После крупных фич делать именованный коммит: `git add -A && git commit -m "описание"`
- GitHub пока не подключён (нужно создать remote и сделать push)

## Правила работы Claude
1. **Перед изменением файла** — всегда читать его (Read tool)
2. **Искать решения через DuckDuckGo** при ошибках компилятора или незнакомых API
3. **Не перемещать существующие .cs файлы** — сломает ссылки в .tscn сценах
4. **Новый код** — в соответствующую папку по структуре ниже
5. **Максимум ~300-400 строк на файл** — если больше, делить
6. **Коммитить после каждой рабочей фичи** (Stop-хук делает это автоматически)

## Структура проекта
```
├── Autoloads/          # Синглтоны (EventBus, GameManager, BuildingRegistry, NavigationManager, ResourceManager, WorkerManager)
├── Buildings/          # Building.cs + Components/ (CartAgent, FarmField, HouseLevel, Inventory, ProductionCycle, Smelter, Warehouse, WorkerAssignment)
├── Combat/             # ПУСТО — будущая боевая система
├── Data/               # BuildingData, BuildingDatabase, ResourceDatabase (ScriptableObject-аналоги)
├── Economy/            # ResourceManager.cs
├── Input/              # BuildPlacementGhost.cs
├── Roads/              # RoadTool.cs, CanalTool.cs
├── UI/                 # BuildMenu, ResourceBar, BuildingInfoPanel, MainMenu
│   └── HUD/            # ПУСТО — будущий боевой HUD
├── Units/              # HumanUnit.cs
│   ├── Base/           # ПУСТО — базовые классы
│   ├── Military/       # ПУСТО — солдаты, лучники
│   └── AI/             # ПУСТО — поведение юнитов
├── World/              # World.cs, World.tscn (главная сцена игры), TileType.cs
│   ├── Tilemap/        # IsoTileMap.cs, TileCoordHelper.cs
│   ├── MapEditor.cs    # Редактор карт
│   └── MapEditor.tscn
├── Audio/              # ПУСТО
├── Tests/              # ПУСТО
├── Multiplayer/        # ПУСТО — добавлять не раньше чем будет готова одиночная игра
└── Assets/
    ├── Units/          # human_idle_{dir}.png, human_walk_{dir}.png (8 направлений: N/NE/E/SE/S/SW/W/NW)
    ├── Buildings/      # dom_1.png, house_level2.png и др.
    └── Tiles/          # sea.png, sand.png, sea_anim_strip.png
```

## Главная сцена
- **Запуск**: `res://UI/MainMenu.tscn` (установлено в project.godot)
- **Игра**: `res://World/World.tscn`
- **Редактор**: `res://World/MapEditor.tscn`

## Autoloads (синглтоны)
```
EventBus         — глобальная шина событий (все системы общаются через неё)
GameManager      — пауза, скорость, MapFilePath, EditorMapMode
BuildingRegistry — размещение/снос зданий
NavigationManager— AStarGrid2D, дороги для CartAgent
ResourceManager  — ресурсы: gold, wood, stone, grain, fish
WorkerManager    — рабочие из домов L1/L2
```

## Ключевые архитектурные решения
- **EventBus** — вся связь между системами через сигналы, без прямых ссылок GetNode()
- **HumanUnit** — свободное движение (не по дорогам), спавнится из дома
- **CartAgent** — движение строго по дорогам (NavigationManager A*)
- **IsoTileMap** — `_typeGrid[x,y]` для быстрого lookup типов тайлов
- **Карта** — 160×160, процедурная генерация (seed=42) ИЛИ загрузка из user://maps/*.json
- **Редактор карт** — GameManager.EditorMapMode=true перед переходом на MapEditor.tscn

## Тайлы (TileType enum + atlas coords)
```
Grass=0 (0,0)  Road=1 (1,0)   Water=2 (2,0)  Sand=3 (3,0)
Forest=4 (4,0) Rock=5 (5,0)   CopperOre=6 (6,0) TinOre=7 (7,0)
Canal=8 (8,0)  — синий центр + коричневая рамка, конвертирует Sand→Grass через 60 сек
```

## Текущий статус (апрель 2026)
✅ Изометрическая карта с процедурной генерацией острова
✅ Строительство зданий (дом, склад, лесопилка, ферма, рыбный промысел и др.)
✅ Экономика (Драхмы, дерево, камень, зерно, рыба)
✅ Жители (HumanUnit, 8 направлений анимации)
✅ Носильщики (CartAgent, дороги, 8 направлений)
✅ Дороги и Каналы (A* прокладка, клик A→B)
✅ Главное меню + Редактор карт + Библиотека карт (сохранение в user://maps/)
✅ Git с автокоммитом

🔜 Следующая приоритетная фича: **Боевые юниты** (папка Military/ готова)

## Спрайты юнитов
- Idle: `human_idle_{dir}.png` — 256×512px, 1 кадр
- Walk: `human_walk_{dir}.png` — 4320×880px, 9 кадров по 480×880px (однострочный спрайт-лист)
- Загрузка без .import: `Image.LoadFromFile(ProjectSettings.GlobalizePath(path))`
- Направление: `atan2(move.Y, move.X)` → 45° сектора → индекс → `DirByFormula[]`

## Типичные проблемы и решения
- **Компилятор не видит класс**: проверь namespace, partial class
- **Текстура не грузится**: использовать двойной fallback (ResourceLoader → Image.LoadFromFile)
- **Тайл не отображается**: проверить что source.CreateTile(atlasCoord) вызван в SetupTileSet()
- **Анимация не меняется**: использовать `_sprite.Animation.Equals(anim)` вместо `==`
