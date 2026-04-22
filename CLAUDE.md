# Новая Бронзовая Игра — Контекст проекта для Claude

## Что это
Изометрический сити-билдер бронзового века. Godot 4.6, C#, 2D изометрия (DiamondDown 128×64).
Разрабатывается одним человеком (Kemal) с помощью Claude Code (вайбкодинг).

## Стек
- **Движок**: Godot 4.6.1 stable mono (Apple M4, Metal 4.0, Forward+)
- **Язык**: C# (.NET)
- **Рендер**: Forward Plus
- **Тайлы**: TileMapLayer, изометрия DiamondDown, 128×64px
- **Навигация**: AStarGrid2D (NavigationManager.cs)

---

## Git и GitHub — правила

- **Репозиторий**: `git init` сделан, ветка `main`
- **GitHub**: https://github.com/Kemal1996uk/novaya-bronzovaya-igra (подключён, публичный)
- **GitHub CLI**: установлен в `~/.local/bin/gh` (аккаунт Kemal1996uk авторизован)
- **Stop-хук** в `.claude/settings.json` — автокоммит + `git push origin main` после каждой сессии
- После крупных фич — именованный коммит: `git add -A && git commit -m "описание"`
- **Автопуш**: Stop-хук пушит на GitHub автоматически — ничего делать не нужно

---

## MCP серверы проекта (все активны)

Конфиг хранится в `~/.claude.json` → секция проекта `/Users/kemal/новая-бронзовая-игра-`

| MCP | Назначение | Статус |
|-----|-----------|--------|
| **godot-mcp** | Запускает Godot, читает ошибки runtime, захватывает GD.Print() | ✅ работает |
| **sequential-thinking** | Пошаговое рассуждение для сложных задач | ✅ работает |
| **context7** | Актуальная документация Godot 4.6 / C# в реальном времени | ✅ работает |
| **duckduckgo** | Поиск в интернете (использовать при ошибках и новых API) | ✅ работает |
| **gimp** | Редактирование PNG спрайтов | ✅ работает |

**Пути для Godot-MCP:**
- Godot бинарь: `/Applications/Godot_mono.app/Contents/MacOS/Godot`
- npx: `/Users/kemal/.nvm/versions/node/v24.15.0/bin/npx`
- gh CLI: `~/.local/bin/gh`

**Как использовать godot-mcp:**
```
1. mcp__godot-mcp__run_project  → запустить игру
2. sleep 8 секунд
3. mcp__godot-mcp__get_debug_output → читать вывод и ошибки
4. mcp__godot-mcp__stop_project → остановить
```
Всегда запускать после написания нового кода чтобы проверить ошибки runtime!

**Ожидаемый вывод при успешном старте World.tscn:**
```
[IsoTileMap] Остров 160×160 сгенерирован.
[NavigationManager] Сетка 160×160 готова (Road-only режим).
[CombatManager] Инициализирован. Первая атака через 120с.
[World] Остров готов. Стройте Лесопилку рядом с лесом!
       🪙15000 🪵60 🪨50
errors: []   ← должен быть пустым
```

---

## Правила работы Claude (выработаны совместно)

1. **Перед изменением файла** — всегда читать его (Read tool)
2. **После написания кода** — запускать через godot-mcp и читать debug output
3. **Искать решения через DuckDuckGo** при ошибках компилятора или незнакомых API
4. **Использовать Context7** для актуальной документации Godot/C# API
5. **Не перемещать существующие .cs файлы** — сломает ссылки в .tscn сценах
6. **Новый код** — в соответствующую папку по структуре ниже
7. **Максимум ~300-400 строк на файл** — если больше, делить на части
8. **Коммитить после каждой рабочей фичи** (Stop-хук делает это + push автоматически)
9. **Обновлять CLAUDE.md** после каждого крупного изменения архитектуры
10. **Проверять dotnet build** перед запуском: `dotnet build --nologo 2>&1 | tail -10`

---

## Структура проекта
```
├── Autoloads/          # Синглтоны (EventBus, GameManager, BuildingRegistry, NavigationManager, ResourceManager, WorkerManager)
├── Buildings/          # Building.cs + Components/ (CartAgent, FarmField, HouseLevel, Inventory, ProductionCycle, Smelter, Warehouse, WorkerAssignment, TrainingQueue)
├── Combat/             # CombatManager.cs — волны бандитов (2+N каждые 3 мин)
├── Data/               # BuildingData, BuildingDatabase, ResourceDatabase (ScriptableObject-аналоги)
│   └── Buildings/      # BuildingData.cs, BuildingDatabase.cs, TileConstraint.cs
├── Economy/            # ResourceManager.cs
├── Input/              # BuildPlacementGhost.cs
├── Roads/              # RoadTool.cs, CanalTool.cs
├── UI/                 # BuildMenu, ResourceBar, BuildingInfoPanel, MainMenu
│   └── HUD/            # CombatHud.cs — алерт НАПАДЕНИЕ! + счётчики юнитов
├── Units/              # HumanUnit.cs
│   ├── Base/           # ПУСТО — базовые классы
│   ├── Military/       # Soldier.cs — боевой юнит игрока (ЛКМ=выделить, ПКМ=двигать)
│   └── AI/             # Bandit.cs — враг (красный ромб, атакует здания и солдат)
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
GameManager      — пауза, скорость, MapFilePath, EditorMapMode, EditorMapWidth/Height
BuildingRegistry — размещение/снос зданий, AllBuildings, DemolishBuilding()
NavigationManager— AStarGrid2D, дороги для CartAgent
ResourceManager  — ресурсы: gold, wood, stone, grain, fish, copper, tin, bronze
WorkerManager    — рабочие из домов L1/L2
```

## Ключевые архитектурные решения
- **EventBus** — вся связь между системами через сигналы, без прямых ссылок GetNode()
- **HumanUnit** — свободное движение (не по дорогам), спавнится из дома
- **CartAgent** — движение строго по дорогам (NavigationManager A*)
- **IsoTileMap** — `_typeGrid[x,y]` для быстрого lookup типов тайлов
- **Карта** — 160×160, процедурная генерация (seed=42) ИЛИ загрузка из user://maps/*.json
- **Редактор карт** — GameManager.EditorMapMode=true перед переходом на MapEditor.tscn
- **Группы Godot** — `"soldiers"` и `"bandits"` для поиска юнитов через GetNodesInGroup()
- **ResourceManager API**: `CanAfford(gold,wood,stone)`, `SpendBuildCost(g,w,s)`, `Spend(id,amount)`, `Add(id,amount)`

## Тайлы (TileType enum + atlas coords)
```
Grass=0 (0,0)  Road=1 (1,0)   Water=2 (2,0)  Sand=3 (3,0)
Forest=4 (4,0) Rock=5 (5,0)   CopperOre=6 (6,0) TinOre=7 (7,0)
Canal=8 (8,0)  — синий центр + коричневая рамка, конвертирует Sand→Grass через 60 сек
```

## EventBus — все сигналы
```
TileClicked(Vector2I, Vector2)
PlacementModeEntered(BuildingData) / PlacementModeExited()
BuildingPlaced(Node) / BuildingDemolished(Node) / BuildingClicked(Node)
DemolishModeEntered() / DemolishModeExited()
RoadModeEntered() / RoadModeExited() / RoadPlaced(Vector2I)
CanalModeEntered() / CanalModeExited() / CanalPlaced(Vector2I)
StockpileChanged() / ResourceConsumed(string, int)
WorkerAssignModeEntered(Node) / WorkerAssignModeExited()
AlertRaised(string)
GamePaused() / GameResumed() / GameSpeedChanged(float)
SoldierSpawned(Node2D) / BanditSpawned(Node2D)
CombatUnitDied(Node2D, bool wasEnemy) / CityUnderAttack()
```

---

## Текущий статус (апрель 2026)
✅ Изометрическая карта 160×160 с процедурной генерацией острова
✅ Строительство зданий (дом, склад, лесопилка, ферма, рыбный промысел и др.)
✅ Экономика (Драхмы, дерево, камень, зерно, рыба, медь, олово, бронза)
✅ Жители (HumanUnit, 8 направлений анимации)
✅ Носильщики (CartAgent, дороги, 8 направлений)
✅ Дороги и Каналы (A* прокладка, клик A→B)
✅ Главное меню + Редактор карт + Библиотека карт (сохранение в user://maps/)
✅ Git + GitHub с автокоммитом и автопушем
✅ Боевая система: Казарма → Солдаты, волны Бандитов, HP зданий, CombatHud
✅ MCP инструменты: godot-mcp, context7, sequential-thinking, duckduckgo, gimp

## Боевая система (апрель 2026)
- **Казарма** (3×3, 2000Д+15Д+20К): тренирует 1 солдата каждые 60 сек за 150 Д
- **Soldier**: ЛКМ=выделить, ПКМ=приказ двигаться, авто-атака бандитов в 65px
  HP=100, Атака=20, Скорость=55. Стальной оттенок. Один солдат выделён в момент.
- **Bandit**: красный ромб, HP=60, Атака=8 по зданиям/16 по солдатам
  Агрит солдат в 150px, иначе идёт к ближайшему зданию. Спавн на краях карты.
- **CombatManager**: волна 1 через 2 мин, дальше каждые 3 мин, размер 2+N
- **HP зданий**: 200 HP, TakeDamage(), HP-бар при повреждении, снос при 0 HP
- **CombatHud**: мигающий алерт «🚨 НАПАДЕНИЕ!» (6 сек), счётчик ⚔️N 🔴N

🔜 Следующие фичи: лучник, башня/стена, звуки, сохранение игры

---

## Спрайты юнитов
- Idle: `human_idle_{dir}.png` — 256×512px, 1 кадр
- Walk: `human_walk_{dir}.png` — 4320×880px, 9 кадров по 480×880px (однострочный спрайт-лист)
- Загрузка без .import: `Image.LoadFromFile(ProjectSettings.GlobalizePath(path))`
- Направление: `atan2(move.Y, move.X)` → 45° сектора → индекс → `DirByFormula[]`
- 8 направлений: E, SE, S, SW, W, NW, N, NE

## Типичные проблемы и решения
- **Компилятор не видит класс**: проверь namespace, partial class
- **Текстура не грузится**: использовать двойной fallback (ResourceLoader → Image.LoadFromFile)
- **Тайл не отображается**: проверить что source.CreateTile(atlasCoord) вызван в SetupTileSet()
- **Анимация не меняется**: использовать `_sprite.Animation.Equals(anim)` вместо `==`
- **OS.GetTicksMsec() не существует**: использовать `Time.GetTicksMsec()`
- **Runtime ошибки**: использовать godot-mcp → run_project → get_debug_output

---

## Что нужно сделать при старте новой сессии

1. Прочитать этот файл (Claude делает это автоматически)
2. Проверить `git log --oneline -5` — увидеть последние изменения
3. Запустить `dotnet build --nologo` — убедиться что всё компилируется
4. Спросить пользователя: "Продолжаем? Что делаем сегодня?"
