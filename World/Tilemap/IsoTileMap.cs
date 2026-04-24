using Godot;
using System.Collections.Generic;

/// <summary>
/// Изометрическая тайловая карта 120×120.
/// Генерирует остров: Вода → Пляж → Трава + кластеры Леса/Скал/Меди/Олова.
///
/// Типы тайлов (atlas coords):
///   (0,0) Grass     #5A8C28 — строить, дорогу
///   (1,0) Road      #A08050 — дорога
///   (2,0) Water     #2870D0 — нельзя
///   (3,0) Sand      #D4B87A — строить, дорогу (пляж)
///   (4,0) Forest    #2D5C10 — нельзя строить (нужен Лесопилке)
///   (5,0) Rock      #787878 — нельзя строить (нужен Каменотёсу)
///   (6,0) CopperOre #B85A20 — медная руда (нужна Медной шахте)
///   (7,0) TinOre    #8888AA — оловянная руда (нужна Оловянной шахте)
/// </summary>
public partial class IsoTileMap : TileMapLayer
{
    public Vector2I MapSize { get; private set; } = new Vector2I(160, 160);

    public const int SourceId = 0;

    public static readonly Vector2I AtlasGrass     = new(0, 0);
    public static readonly Vector2I AtlasRoad      = new(1, 0);
    public static readonly Vector2I AtlasWater     = new(2, 0);
    public static readonly Vector2I AtlasSand      = new(3, 0);
    public static readonly Vector2I AtlasForest    = new(4, 0);
    public static readonly Vector2I AtlasRock      = new(5, 0);
    public static readonly Vector2I AtlasCopperOre = new(6, 0);
    public static readonly Vector2I AtlasTinOre    = new(7, 0);
    public static readonly Vector2I AtlasCanal     = new(8, 0);
    public static readonly Vector2I AtlasDesert    = new(9, 0);

    // Внутренняя сетка типов тайлов (быстрый доступ без обращения к TileMap)
    private TileType[,] _typeGrid;

    private TileMapLayer _bgWaterLayer;

    public override void _Ready()
    {
        YSortEnabled = true;

        // Nearest filtering — убирает интерполяцию между тайлами (главная причина швов)
        TextureFilter = TextureFilterEnum.Nearest;

        // Режим карты определяется через GameManager (доступен до _Ready у автозагрузок)
        var gm = GameManager.Instance;
        if (gm?.EditorMapMode == true)
            MapSize = new Vector2I(gm.EditorMapWidth, gm.EditorMapHeight);

        _typeGrid = new TileType[MapSize.X, MapSize.Y];
        SetupTileSet();
        SetupBgWaterLayer();

        if (gm?.EditorMapMode == true)
        {
            FillBlankGrass();
            GD.Print($"[IsoTileMap] Редактор: пустая карта {MapSize.X}×{MapSize.Y}.");
        }
        else if (!string.IsNullOrEmpty(gm?.MapFilePath))
        {
            LoadMapFromFile(gm.MapFilePath);
        }
        else
        {
            GenerateIsland();
            GD.Print($"[IsoTileMap] Остров {MapSize.X}×{MapSize.Y} сгенерирован.");
        }
    }

    private void SetupBgWaterLayer()
    {
        // Фоновый слой воды — закрывает серые щели под анимацией
        // Используем sea.png, fallback — сплошной синий
        Texture2D bgTex = GD.Load<Texture2D>("res://Assets/Tiles/sea.png");
        if (bgTex == null)
        {
            var img = Image.CreateEmpty(128, 64, false, Image.Format.Rgba8);
            img.Fill(new Color(0.10f, 0.24f, 0.50f));
            bgTex = ImageTexture.CreateFromImage(img);
        }

        var bgTileSet = new TileSet
        {
            TileShape  = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown,
            TileSize   = new Vector2I(128, 64)
        };
        var bgSource = new TileSetAtlasSource
        {
            Texture           = bgTex,
            TextureRegionSize = new Vector2I(128, 64)
        };
        bgSource.CreateTile(new Vector2I(0, 0));
        bgTileSet.AddSource(bgSource, 0);

        _bgWaterLayer = new TileMapLayer
        {
            Name          = "BgWaterLayer",
            YSortEnabled  = false,
            ZIndex        = -100,
            TileSet       = bgTileSet
        };
        AddChild(_bgWaterLayer);

        for (int x = 0; x < MapSize.X; x++)
            for (int y = 0; y < MapSize.Y; y++)
                _bgWaterLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0));
    }

    // ─── Генерация острова ────────────────────────────────────────────────────

    private void GenerateIsland()
    {
        var rng = new RandomNumberGenerator();
        rng.Seed  = 42;
        rng.State = 0; // фиксируем внутреннее состояние → карта всегда одинакова

        int cx = MapSize.X / 2; // 80
        int cy = MapSize.Y / 2; // 80
        float rx = 68f;         // полуось X острова
        float ry = 60f;         // полуось Y острова
        float beachW = 5f;      // широкий пляж/пустыня по краям

        // 1. Заполнить всё водой
        for (int x = 0; x < MapSize.X; x++)
            for (int y = 0; y < MapSize.Y; y++)
                SetTileInternal(new Vector2I(x, y), TileType.Water);

        // 2. Суша и пляж по эллипсу
        for (int x = 0; x < MapSize.X; x++)
        for (int y = 0; y < MapSize.Y; y++)
        {
            float dx = x - cx;
            float dy = y - cy;
            float ellipse = (dx * dx) / (rx * rx) + (dy * dy) / (ry * ry);

            if (ellipse <= 1f)
                SetTileInternal(new Vector2I(x, y), TileType.Grass);
            else if (ellipse <= (1f + beachW / rx))
                SetTileInternal(new Vector2I(x, y), TileType.Sand);
        }

        // 3. Пустыня — половина острова (полуплоскость со случайной ориентацией)
        // Делим остров случайной линией через центр. Все тайлы Grass по одну сторону → Desert.
        // Граница размыта шумом ±8 тайлов для естественности.
        float desertAngle = rng.Randf() * Mathf.Tau;
        float dnx = Mathf.Cos(desertAngle); // нормаль к границе пустыни
        float dny = Mathf.Sin(desertAngle);
        for (int x = 0; x < MapSize.X; x++)
        for (int y = 0; y < MapSize.Y; y++)
        {
            if (_typeGrid[x, y] != TileType.Grass) continue;
            float proj  = (x - cx) * dnx + (y - cy) * dny;
            float noise = (rng.Randf() - 0.5f) * 16f; // размытие границы
            if (proj + noise > 0f)
                SetTileInternal(new Vector2I(x, y), TileType.Desert);
        }

        // 4. Forest кластеры — много и крупные, только на Grass (зелёная половина)
        int forestPatches = rng.RandiRange(22, 30);
        for (int i = 0; i < forestPatches; i++)
            SpawnCluster(rng, TileType.Forest, TileType.Grass, 10, 20, cx, cy, rx - 4, ry - 4);

        // 5. Rock кластеры — могут быть и в пустыне, и на траве
        int rockPatches = rng.RandiRange(8, 13);
        for (int i = 0; i < rockPatches / 2; i++)
            SpawnCluster(rng, TileType.Rock, TileType.Grass, 5, 10, cx, cy, rx - 4, ry - 4);
        for (int i = rockPatches / 2; i < rockPatches; i++)
            SpawnCluster(rng, TileType.Rock, TileType.Desert, 5, 10, cx, cy, rx - 4, ry - 4);

        // 6. CopperOre — только в центре острова (радиус ≤ 22)
        int copperPatches = rng.RandiRange(4, 7);
        for (int i = 0; i < copperPatches; i++)
            SpawnCluster(rng, TileType.CopperOre, TileType.Grass, 3, 7, cx, cy, 22f, 20f);

        // 7. TinOre — только в центре острова (радиус ≤ 22)
        int tinPatches = rng.RandiRange(4, 7);
        for (int i = 0; i < tinPatches; i++)
            SpawnCluster(rng, TileType.TinOre, TileType.Grass, 3, 7, cx, cy, 22f, 20f);

        // 8. Реки
        CarveRiver(rng, cx, cy, halfWidth: 1, steps: 100); // большая
        CarveRiver(rng, cx, cy, halfWidth: 0, steps:  55); // малая 1
        CarveRiver(rng, cx, cy, halfWidth: 0, steps:  55); // малая 2

        // 9. Отрисовать всё в TileMapLayer
        for (int x = 0; x < MapSize.X; x++)
            for (int y = 0; y < MapSize.Y; y++)
                ApplyTileVisual(new Vector2I(x, y));
    }

    /// <summary>
    /// Вырезает реку — случайная извилистая линия воды через остров.
    /// halfWidth=0 → 1 тайл шириной; halfWidth=1 → 3 тайла (большая река).
    /// </summary>
    private void CarveRiver(RandomNumberGenerator rng, int cx, int cy, int halfWidth, int steps)
    {
        float rx = MapSize.X * 0.40f;
        float ry = MapSize.Y * 0.36f;

        // Начало — точка у берега острова
        float angle = rng.Randf() * Mathf.Tau;
        float px = cx + Mathf.Cos(angle) * rx * 0.88f;
        float py = cy + Mathf.Sin(angle) * ry * 0.88f;

        // Начальное направление — к центру с небольшим разбросом
        float dx = cx - px + (rng.Randf() - 0.5f) * 30f;
        float dy = cy - py + (rng.Randf() - 0.5f) * 30f;
        float dlen = Mathf.Sqrt(dx * dx + dy * dy);
        dx /= dlen; dy /= dlen;

        for (int s = 0; s < steps; s++)
        {
            int ix = (int)px, iy = (int)py;
            for (int wx = -halfWidth; wx <= halfWidth; wx++)
            for (int wy = -halfWidth; wy <= halfWidth; wy++)
            {
                var t = new Vector2I(ix + wx, iy + wy);
                if (IsValidTile(t))
                    SetTileInternal(t, TileType.Water);
            }

            // Случайный изгиб поперёк направления
            float perpX = -dy, perpY = dx;
            float drift = (rng.Randf() - 0.5f) * 0.6f;
            dx += perpX * drift;
            dy += perpY * drift;
            dlen = Mathf.Sqrt(dx * dx + dy * dy);
            dx /= dlen; dy /= dlen;

            px += dx;
            py += dy;

            if (!IsValidTile(new Vector2I((int)px, (int)py))) break;
        }
    }

    private void SpawnCluster(RandomNumberGenerator rng, TileType type, TileType requireOn,
                              int minSize, int maxSize, int cx, int cy, float rx, float ry)
    {
        // Попытки найти точку внутри Grass-зоны острова
        for (int attempt = 0; attempt < 20; attempt++)
        {
            float angle = rng.Randf() * Mathf.Tau;
            float dist  = rng.Randf();
            int px = cx + (int)(Mathf.Cos(angle) * rx * dist);
            int py = cy + (int)(Mathf.Sin(angle) * ry * dist);
            var origin = new Vector2I(px, py);

            if (!IsValidTile(origin) || _typeGrid[origin.X, origin.Y] != requireOn) continue;

            int size = rng.RandiRange(minSize, maxSize);
            var queue = new Queue<Vector2I>();
            queue.Enqueue(origin);
            int placed = 0;

            while (queue.Count > 0 && placed < size)
            {
                var tile = queue.Dequeue();
                if (!IsValidTile(tile) || _typeGrid[tile.X, tile.Y] != requireOn) continue;

                SetTileInternal(tile, type);
                placed++;

                // случайно распространяем кластер
                var dirs = new Vector2I[]
                    { new(1,0), new(-1,0), new(0,1), new(0,-1) };
                foreach (var d in dirs)
                    if (rng.Randf() < 0.55f)
                        queue.Enqueue(tile + d);
            }
            return;
        }
    }

    private void SetTileInternal(Vector2I tile, TileType type)
    {
        _typeGrid[tile.X, tile.Y] = type;
    }

    private void ApplyTileVisual(Vector2I tile)
    {
        var type = _typeGrid[tile.X, tile.Y];

        var atlas = type switch
        {
            TileType.Grass     => AtlasGrass,
            TileType.Road      => AtlasRoad,
            TileType.Water     => AtlasWater,
            TileType.Sand      => AtlasSand,
            TileType.Forest    => AtlasForest,
            TileType.Rock      => AtlasRock,
            TileType.CopperOre => AtlasCopperOre,
            TileType.TinOre    => AtlasTinOre,
            TileType.Canal     => AtlasCanal,
            TileType.Desert    => AtlasDesert,
            _                  => AtlasGrass,
        };
        SetCell(tile, SourceId, atlas);
    }

    // ─── Публичные методы ─────────────────────────────────────────────────────

    public TileType GetTileType(Vector2I tile)
        => IsValidTile(tile) ? _typeGrid[tile.X, tile.Y] : TileType.Water;

    /// <summary>Есть ли тип type среди 4 соседей тайла?</summary>
    public bool IsAdjacentToType(Vector2I tile, TileType type)
    {
        var dirs = new Vector2I[] { new(1,0), new(-1,0), new(0,1), new(0,-1) };
        foreach (var d in dirs)
            if (GetTileType(tile + d) == type) return true;
        return false;
    }

    /// <summary>Есть ли тип type в квадратном радиусе (Chebyshev) вокруг тайла?</summary>
    public bool IsWithinRadiusOfType(Vector2I tile, TileType type, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            if (dx == 0 && dy == 0) continue;
            if (GetTileType(tile + new Vector2I(dx, dy)) == type) return true;
        }
        return false;
    }

    /// <summary>Можно ли на тайле что-то строить (Grass или Sand)?</summary>
    public bool IsBuildable(Vector2I tile)
    {
        var t = GetTileType(tile);
        return t == TileType.Grass || t == TileType.Sand || t == TileType.Desert;
    }

    /// <summary>Можно ли проложить дорогу/канал по тайлу?</summary>
    public bool IsRoadable(Vector2I tile)
    {
        var t = GetTileType(tile);
        return t == TileType.Grass || t == TileType.Sand || t == TileType.Desert
            || t == TileType.Road  || t == TileType.Canal;
    }

    // Исходные типы тайлов до прокладки дороги (для восстановления при сносе)
    private readonly System.Collections.Generic.Dictionary<Vector2I, TileType> _roadOriginals = new();

    /// <summary>Установить дорогу на тайл.</summary>
    public void PlaceRoad(Vector2I tile)
    {
        if (!IsValidTile(tile)) return;
        if (_typeGrid[tile.X, tile.Y] != TileType.Road)
            _roadOriginals[tile] = _typeGrid[tile.X, tile.Y];
        _typeGrid[tile.X, tile.Y] = TileType.Road;
        SetCell(tile, SourceId, AtlasRoad);
    }

    /// <summary>Установить канал на тайл.</summary>
    public void PlaceCanal(Vector2I tile)
    {
        if (!IsValidTile(tile)) return;
        if (_typeGrid[tile.X, tile.Y] != TileType.Canal)
            _roadOriginals[tile] = _typeGrid[tile.X, tile.Y];
        _typeGrid[tile.X, tile.Y] = TileType.Canal;
        SetCell(tile, SourceId, AtlasCanal);
    }

    /// <summary>
    /// Превращает все тайлы Sand в Grass в радиусе (Chebyshev) вокруг center.
    /// Вызывается через ~60 секунд после укладки канала.
    /// </summary>
    public void ConvertSandToGrassAround(Vector2I center, int radius = 4)
    {
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            var nb = center + new Vector2I(dx, dy);
            if (IsValidTile(nb) && (_typeGrid[nb.X, nb.Y] == TileType.Sand
                                   || _typeGrid[nb.X, nb.Y] == TileType.Desert))
            {
                _typeGrid[nb.X, nb.Y] = TileType.Grass;
                SetCell(nb, SourceId, AtlasGrass);
            }
        }
    }

    /// <summary>
    /// Установить тип тайла напрямую (используется редактором карт).
    /// </summary>
    public void PaintTile(Vector2I tile, TileType type)
    {
        if (!IsValidTile(tile)) return;
        _typeGrid[tile.X, tile.Y] = type;
        ApplyTileVisual(tile);
    }

    /// <summary>
    /// Заполнить всю карту травой (режим редактора).
    /// </summary>
    private void FillBlankGrass()
    {
        for (int x = 0; x < MapSize.X; x++)
        for (int y = 0; y < MapSize.Y; y++)
        {
            _typeGrid[x, y] = TileType.Grass;
            SetCell(new Vector2I(x, y), SourceId, AtlasGrass);
        }
    }

    /// <summary>Сохранить тайловую карту в JSON-файл.</summary>
    public void SaveMapToFile(string path)
    {
        // Убедиться что папка существует
        EnsureMapsDir();

        var dict = new Godot.Collections.Dictionary();
        dict["width"]  = MapSize.X;
        dict["height"] = MapSize.Y;

        var tilesArr = new Godot.Collections.Array();
        for (int y = 0; y < MapSize.Y; y++)
        for (int x = 0; x < MapSize.X; x++)
            tilesArr.Add((long)(int)_typeGrid[x, y]);
        dict["tiles"] = tilesArr;

        string json = Json.Stringify(dict);
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
        if (f != null)
        {
            f.StoreString(json);
            GD.Print($"[IsoTileMap] Карта сохранена: {path}");
        }
        else
            GD.PrintErr($"[IsoTileMap] Не удалось сохранить карту: {path}");
    }

    /// <summary>Загрузить тайловую карту из JSON-файла.</summary>
    public void LoadMapFromFile(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) { GD.PrintErr($"[IsoTileMap] Файл не найден: {path}"); GenerateIsland(); return; }

        var json = new Json();
        if (json.Parse(f.GetAsText()) != Error.Ok) { GD.PrintErr("[IsoTileMap] Ошибка разбора JSON"); GenerateIsland(); return; }

        var data = json.Data.AsGodotDictionary();
        int w = data["width"].AsInt32();
        int h = data["height"].AsInt32();

        // Переразмещаем сетку если размер отличается
        if (w != MapSize.X || h != MapSize.Y)
        {
            MapSize   = new Vector2I(w, h);
            _typeGrid = new TileType[w, h];
        }

        var tilesArr = data["tiles"].AsGodotArray();
        int idx = 0;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            _typeGrid[x, y] = (TileType)tilesArr[idx++].AsInt32();
            ApplyTileVisual(new Vector2I(x, y));
        }

        GD.Print($"[IsoTileMap] Карта загружена: {path} ({w}×{h})");
    }

    private static void EnsureMapsDir()
    {
        if (!DirAccess.DirExistsAbsolute("user://maps"))
        {
            using var d = DirAccess.Open("user://");
            d?.MakeDir("maps");
        }
    }

    /// <summary>Снести дорогу, восстановить исходный тип. Возвращает false если тайл — не дорога.</summary>
    public bool RemoveRoad(Vector2I tile)
    {
        if (!IsValidTile(tile) || _typeGrid[tile.X, tile.Y] != TileType.Road) return false;
        var original = _roadOriginals.TryGetValue(tile, out var t) ? t : TileType.Grass;
        _roadOriginals.Remove(tile);
        _typeGrid[tile.X, tile.Y] = original;
        ApplyTileVisual(tile);
        return true;
    }

    public Vector2 GetTileWorldCenter(Vector2I tileCoord)
        => TileCoordHelper.TileToGlobal(this, tileCoord);

    public Vector2I GlobalToTile(Vector2 globalPos)
        => TileCoordHelper.GlobalToTile(this, globalPos);

    public bool IsValidTile(Vector2I tileCoord)
        => TileCoordHelper.IsInBounds(tileCoord, MapSize);

    // ─── TileSet с 6 заглушками ───────────────────────────────────────────────

    /// <summary>
    /// Единый атлас-источник, все тайлы — жёсткие процедурные ромбы.
    /// Никаких PNG, никаких анимаций: чистый путь без причин для швов.
    /// use_texture_padding=true — встроенная защита Godot от швов.
    /// </summary>
    private void SetupTileSet()
    {
        var tileSet = new TileSet
        {
            TileShape  = TileSet.TileShapeEnum.Isometric,
            TileLayout = TileSet.TileLayoutEnum.DiamondDown,
            TileSize   = new Vector2I(128, 64)
        };

        var source = new TileSetAtlasSource
        {
            Texture           = CreatePlaceholderTexture(),
            TextureRegionSize = new Vector2I(128, 64),
            UseTexturePadding = true  // явно: 1px padding по краям каждого тайла
        };

        source.CreateTile(AtlasGrass);
        source.CreateTile(AtlasRoad);
        source.CreateTile(AtlasWater);
        source.CreateTile(AtlasSand);
        source.CreateTile(AtlasForest);
        source.CreateTile(AtlasRock);
        source.CreateTile(AtlasCopperOre);
        source.CreateTile(AtlasTinOre);
        source.CreateTile(AtlasCanal);
        source.CreateTile(AtlasDesert);

        tileSet.AddSource(source, SourceId);

        TileSet = tileSet;
    }

    /// <summary>
    /// Атлас всех тайлов: PNG там где есть, иначе процедурный ромб.
    /// sea.png      → Water (idx 2)
    /// desert.png   → Sand (idx 3) + Desert (idx 9), берём первый тайл 128×64
    /// Остальные    → DrawIsoDiamond (сплошной цвет, без бордюра)
    /// </summary>
    private static ImageTexture CreatePlaceholderTexture()
    {
        const int tileW = 128, tileH = 64;
        var image = Image.CreateEmpty(tileW * 10, tileH, false, Image.Format.Rgba8);

        DrawIsoDiamond(image, 0, new Color(0.35f, 0.55f, 0.16f)); // Grass
        DrawIsoDiamond(image, 1, new Color(0.63f, 0.50f, 0.31f)); // Road

        // Water → sea.png (128×64)
        if (!BlitPng(image, 2, "res://Assets/Tiles/sea.png", 0, 0))
            DrawIsoDiamond(image, 2, new Color(0.16f, 0.44f, 0.82f));

        // Sand и Desert → desert.png (один большой ромб 1024×512, ресайзим целиком)
        if (!BlitPngResized(image, 3, "res://Assets/Tiles/desert.png"))
            DrawIsoDiamond(image, 3, new Color(0.83f, 0.72f, 0.48f));

        DrawIsoDiamond(image, 4, new Color(0.18f, 0.36f, 0.06f)); // Forest
        DrawIsoDiamond(image, 5, new Color(0.47f, 0.47f, 0.47f)); // Rock
        DrawIsoDiamond(image, 6, new Color(0.72f, 0.35f, 0.12f)); // CopperOre
        DrawIsoDiamond(image, 7, new Color(0.55f, 0.55f, 0.68f)); // TinOre
        DrawIsoDiamond(image, 8, new Color(0.16f, 0.44f, 0.82f)); // Canal

        // Desert → то же изображение (берег и пустыня одинаковый тайл)
        if (!BlitPngResized(image, 9, "res://Assets/Tiles/desert.png"))
            DrawIsoDiamond(image, 9, new Color(0.91f, 0.82f, 0.54f));

        return ImageTexture.CreateFromImage(image);
    }

    /// <summary>
    /// Ресайзит весь PNG в 128×64 и вставляет в позицию dstIndex атласа.
    /// Используется когда PNG — один большой тайл (не спрайтшит по ячейкам).
    /// </summary>
    private static bool BlitPngResized(Image atlas, int dstIndex, string resPath)
    {
        const int w = 128, h = 64;

        Image src = null;
        var tex = ResourceLoader.Load<Texture2D>(resPath);
        if (tex != null)
            src = tex.GetImage();
        if (src == null)
        {
            string absPath = ProjectSettings.GlobalizePath(resPath);
            if (System.IO.File.Exists(absPath))
                src = Image.LoadFromFile(absPath);
        }
        if (src == null)
        {
            GD.PrintErr($"[IsoTileMap] BlitPngResized: не удалось загрузить {resPath}");
            return false;
        }

        if (src.GetFormat() != Image.Format.Rgba8)
            src.Convert(Image.Format.Rgba8);

        // Ресайз всего изображения до размера одного тайла
        src.Resize(w, h, Image.Interpolation.Bilinear);

        atlas.BlitRect(src, new Rect2I(0, 0, w, h), new Vector2I(dstIndex * w, 0));

        // Алмазная маска
        int ox = dstIndex * w;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx   = (float)x / w;
            float ny   = (float)y / h;
            float dist = Mathf.Abs(nx - 0.5f) + Mathf.Abs(ny - 0.5f);
            if (dist > 0.5f)
                atlas.SetPixel(ox + x, y, Colors.Transparent);
            else
            {
                var c = atlas.GetPixel(ox + x, y);
                atlas.SetPixel(ox + x, y, new Color(c.R, c.G, c.B, 1.0f));
            }
        }

        GD.Print($"[IsoTileMap] BlitPngResized OK: {resPath} → atlas[{dstIndex}]");
        return true;
    }

    /// <summary>
    /// Вставляет тайл [srcTileX, srcTileY] из PNG-файла в позицию dstIndex атласа.
    /// Явно конвертирует в Rgba8 перед блитингом (иначе чёрные пиксели).
    /// После вставки применяет алмазную маску: снаружи ромба alpha=0.
    /// </summary>
    private static bool BlitPng(Image atlas, int dstIndex, string resPath, int srcTileX, int srcTileY)
    {
        const int w = 128, h = 64;

        // Сначала через Godot ResourceLoader (использует .import кэш)
        Image src = null;
        var tex = ResourceLoader.Load<Texture2D>(resPath);
        if (tex != null)
            src = tex.GetImage();

        // Fallback — прямая загрузка файла
        if (src == null)
        {
            string absPath = ProjectSettings.GlobalizePath(resPath);
            if (System.IO.File.Exists(absPath))
                src = Image.LoadFromFile(absPath);
        }

        if (src == null)
        {
            GD.PrintErr($"[IsoTileMap] BlitPng: не удалось загрузить {resPath}");
            return false;
        }

        // ОБЯЗАТЕЛЬНО конвертируем в Rgba8 — иначе BlitRect даёт чёрные пиксели
        if (src.GetFormat() != Image.Format.Rgba8)
            src.Convert(Image.Format.Rgba8);

        // Кроп нужного тайла из спрайтшита
        int sx = srcTileX * w, sy = srcTileY * h;
        Image tile;
        if (src.GetWidth() >= sx + w && src.GetHeight() >= sy + h)
        {
            tile = Image.CreateEmpty(w, h, false, Image.Format.Rgba8);
            tile.BlitRect(src, new Rect2I(sx, sy, w, h), Vector2I.Zero);
        }
        else
        {
            src.Resize(w, h, Image.Interpolation.Nearest);
            tile = src;
        }

        // Вставляем тайл в нужную позицию атласа
        atlas.BlitRect(tile, new Rect2I(0, 0, w, h), new Vector2I(dstIndex * w, 0));

        // Алмазная маска: снаружи ромба → alpha=0, внутри → alpha строго 1
        int ox = dstIndex * w;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx   = (float)x / w;
            float ny   = (float)y / h;
            float dist = Mathf.Abs(nx - 0.5f) + Mathf.Abs(ny - 0.5f);
            if (dist > 0.5f)
                atlas.SetPixel(ox + x, y, Colors.Transparent);
            else
            {
                var c = atlas.GetPixel(ox + x, y);
                atlas.SetPixel(ox + x, y, new Color(c.R, c.G, c.B, 1.0f));
            }
        }

        GD.Print($"[IsoTileMap] BlitPng OK: {resPath} tile({srcTileX},{srcTileY}) → atlas[{dstIndex}]");
        return true;
    }

    /// <summary>
    /// Рисует изометрический ромб (диамант) 128×64 в позиции tileIndex атласа.
    /// Сплошная заливка без бордюра — соседние тайлы одного типа сливаются в поле.
    /// </summary>
    private static void DrawIsoDiamond(Image image, int tileIndex, Color fill)
    {
        const int w = 128, h = 64;
        int offsetX = tileIndex * w;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            float nx = (float)x / w;
            float ny = (float)y / h;
            float dist = Mathf.Abs(nx - 0.5f) + Mathf.Abs(ny - 0.5f);
            if (dist > 0.5f) continue;
            image.SetPixel(offsetX + x, y, fill);
        }
    }
}
