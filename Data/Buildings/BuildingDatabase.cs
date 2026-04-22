using Godot;

/// <summary>
/// Все здания игры.
/// Производства теперь используют WorkerCost вместо RequiresWorkers.
/// Добавлены: Медная шахта, Оловянная шахта, Плавильня.
/// Дом уровня 2: SpritePathAlt.
/// </summary>
public static class BuildingDatabase
{
    public static readonly BuildingData Sawmill = new()
    {
        BuildingId         = "sawmill",
        DisplayName        = "Лесопилка",
        Description        = "Добывает дерево. Ставь рядом с лесом.",
        FootprintSize      = new Vector2I(2, 3),
        PlaceholderColor   = new Color(0.60f, 0.40f, 0.20f),
        WallHeight         = 28,
        GoldCost           = 1000,
        RequiresRoad       = true,
        WorkerCost         = 5,
        TileConstraint     = TileConstraint.NearForest,
        OutputResourceId   = "wood",
        OutputAmount       = 2,
        ProductionCycleSec = 8f,
        LocalInventoryCap  = 20,
    };

    public static readonly BuildingData StoneWorkshop = new()
    {
        BuildingId         = "stone_workshop",
        DisplayName        = "Каменотёс",
        Description        = "Добывает камень. Ставь рядом со скалами.",
        FootprintSize      = new Vector2I(2, 3),
        PlaceholderColor   = new Color(0.55f, 0.55f, 0.55f),
        WallHeight         = 28,
        GoldCost           = 1000,
        RequiresRoad       = true,
        WorkerCost         = 5,
        TileConstraint     = TileConstraint.NearRock,
        OutputResourceId   = "stone",
        OutputAmount       = 2,
        ProductionCycleSec = 10f,
        LocalInventoryCap  = 20,
    };

    public static readonly BuildingData Farm = new()
    {
        BuildingId         = "farm",
        DisplayName        = "Ферма",
        Description        = "Производит зерно. Нужно 4 поля рядом.",
        FootprintSize      = new Vector2I(2, 2),
        PlaceholderColor   = new Color(0.35f, 0.75f, 0.22f),
        WallHeight         = 20,
        GoldCost           = 1000,
        WoodCost           = 10,
        RequiresRoad       = true,
        WorkerCost         = 5,
        TileConstraint     = TileConstraint.None,
        OutputResourceId   = "grain",
        OutputAmount       = 3,
        ProductionCycleSec = 8f,
        LocalInventoryCap  = 20,
    };

    public static readonly BuildingData Field = new()
    {
        BuildingId         = "field",
        DisplayName        = "Поле",
        Description        = "Нужно для Фермы (4 шт.).",
        FootprintSize      = new Vector2I(4, 4),
        PlaceholderColor   = new Color(0.55f, 0.82f, 0.30f),
        WallHeight         = 4,
        GoldCost           = 250,
        WoodCost           = 3,
        RequiresRoad       = false,
        OutputResourceId   = "",
        LocalInventoryCap  = 0,
    };

    public static readonly BuildingData FishingHut = new()
    {
        BuildingId         = "fishing_hut",
        DisplayName        = "Рыболовня",
        Description        = "Добывает рыбу. Строй у воды.",
        FootprintSize      = new Vector2I(2, 4),
        PlaceholderColor   = new Color(0.22f, 0.55f, 0.80f),
        WallHeight         = 22,
        GoldCost           = 1000,
        WoodCost           = 8,
        RequiresRoad       = true,
        WorkerCost         = 5,
        TileConstraint     = TileConstraint.NearWater,
        OutputResourceId   = "fish",
        OutputAmount       = 2,
        ProductionCycleSec = 12f,
        LocalInventoryCap  = 20,
    };

    public static readonly BuildingData House = new()
    {
        BuildingId       = "house",
        DisplayName      = "Дом",
        Description      = "Жилой дом. Потребляет рыбу и зерно. Ур.1: 5 иммигрантов. Ур.2: 5 граждан.",
        FootprintSize    = new Vector2I(2, 2),
        PlaceholderColor = new Color(0.90f, 0.72f, 0.35f),
        WallHeight       = 26,
        WoodCost         = 2,
        RequiresRoad     = true,
        OutputResourceId = "",
        LocalInventoryCap = 0,

        // dom_1.png / kem1_2x2.png: 256×256, нарисованы на 2×2 тайл.
        // Передняя вершина 2×2 = якорь + 96.
        // SpriteOffset.Y = 96 - 256/2 = -32.
        SpritePath          = "res://Assets/Buildings/dom_1.png",
        SpritePathVariant   = "res://Assets/Buildings/kem1_2x2.png",
        SpriteScale         = 1.0f,
        SpriteOffset        = new Vector2(0f, -32f),
        SpriteOffsetVariant = new Vector2(0f, -32f),

        SpritePathAlt   = "res://Assets/Buildings/house_level2.png",
        SpriteOffsetAlt = new Vector2(0f, 0f),
    };

    public static readonly BuildingData Warehouse = new()
    {
        BuildingId        = "warehouse",
        DisplayName       = "Склад",
        Description       = "Главное хранилище. 3 тележки собирают ресурсы.",
        FootprintSize     = new Vector2I(4, 4),
        PlaceholderColor  = new Color(0.55f, 0.42f, 0.28f),
        WallHeight        = 38,
        GoldCost          = 2500,
        WoodCost          = 12,
        StoneCost         = 12,
        RequiresRoad      = true,
        OutputResourceId  = "",
        LocalInventoryCap = 999,
    };

    // ─── Бронзовая цепочка (доступна при 10+ гражданах) ──────────────────────

    public static readonly BuildingData CopperMine = new()
    {
        BuildingId         = "copper_mine",
        DisplayName        = "Медная шахта",
        Description        = "Добывает медь. Ставь у медной руды. Нужно 10 рабочих.",
        FootprintSize      = new Vector2I(2, 2),
        PlaceholderColor   = new Color(0.72f, 0.35f, 0.12f),
        WallHeight         = 24,
        GoldCost           = 500,
        WoodCost           = 10,
        StoneCost          = 10,
        RequiresRoad       = true,
        WorkerCost         = 10,
        TileConstraint     = TileConstraint.NearCopperOre,
        OutputResourceId   = "copper",
        OutputAmount       = 1,
        ProductionCycleSec = 15f,   // 4/мин
        LocalInventoryCap  = 20,
    };

    public static readonly BuildingData TinMine = new()
    {
        BuildingId         = "tin_mine",
        DisplayName        = "Оловянная шахта",
        Description        = "Добывает олово. Ставь у оловянной руды. Нужно 10 рабочих.",
        FootprintSize      = new Vector2I(2, 2),
        PlaceholderColor   = new Color(0.55f, 0.55f, 0.68f),
        WallHeight         = 24,
        GoldCost           = 500,
        WoodCost           = 10,
        StoneCost          = 10,
        RequiresRoad       = true,
        WorkerCost         = 10,
        TileConstraint     = TileConstraint.NearTinOre,
        OutputResourceId   = "tin",
        OutputAmount       = 1,
        ProductionCycleSec = 15f,   // 4/мин
        LocalInventoryCap  = 20,
    };

    public static readonly BuildingData Smelter = new()
    {
        BuildingId        = "smelter",
        DisplayName       = "Плавильня",
        Description       = "Плавит бронзу: 2🪵+2🟤+2🔘 → 2🥉 / мин. Нужно 5 граждан.",
        FootprintSize     = new Vector2I(3, 3),
        PlaceholderColor  = new Color(0.60f, 0.30f, 0.10f),
        WallHeight        = 32,
        GoldCost          = 1500,
        WoodCost          = 15,
        StoneCost         = 20,
        RequiresRoad      = true,
        WorkerCost        = 5,
        RequiresCitizens  = true,
        OutputResourceId  = "",     // SmelterComponent обрабатывает производство
        LocalInventoryCap = 20,
    };

    // ─── Все здания в BuildMenu ───────────────────────────────────────────────

    /// <summary>Базовые здания (всегда доступны).</summary>
    public static BuildingData[] Base => new[]
    {
        Sawmill, StoneWorkshop, Farm, FishingHut, House, Warehouse
    };

    /// <summary>Здания бронзовой эпохи (доступны при 10+ гражданах).</summary>
    public static BuildingData[] Bronze => new[]
    {
        CopperMine, TinMine, Smelter
    };

    /// <summary>Все здания для BuildMenu.</summary>
    public static BuildingData[] All => new[]
    {
        Sawmill, StoneWorkshop, Farm, FishingHut, House, Warehouse,
        CopperMine, TinMine, Smelter
    };

    public static BuildingData Find(string id) => id switch
    {
        "sawmill"        => Sawmill,
        "stone_workshop" => StoneWorkshop,
        "farm"           => Farm,
        "field"          => Field,
        "fishing_hut"    => FishingHut,
        "house"          => House,
        "warehouse"      => Warehouse,
        "copper_mine"    => CopperMine,
        "tin_mine"       => TinMine,
        "smelter"        => Smelter,
        _                => null,
    };
}
