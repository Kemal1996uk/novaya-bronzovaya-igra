using Godot;

[GlobalClass]
public partial class BuildingData : Resource
{
    [Export] public string   BuildingId       { get; set; } = "";
    [Export] public string   DisplayName      { get; set; } = "Здание";
    [Export] public string   Description      { get; set; } = "";
    [Export] public Vector2I FootprintSize    { get; set; } = Vector2I.One;
    [Export] public Color    PlaceholderColor { get; set; } = new Color(0.6f, 0.6f, 0.6f);
    [Export] public int      WallHeight       { get; set; } = 26;

    // ─── Стоимость строительства ─────────────────────────────────────────────
    [Export] public int  GoldCost   { get; set; } = 0;
    [Export] public int  WoodCost   { get; set; } = 0;
    [Export] public int  StoneCost  { get; set; } = 0;

    // ─── Спрайт (необязательно) ───────────────────────────────────────────────
    // Если путь задан — используется Sprite2D вместо процедурного куба.
    // SpritePathAlt:     спрайт уровня 2 (апгрейд).
    // SpritePathVariant: второй вариант L1 — при размещении чередуется с SpritePath.
    [Export] public string  SpritePath          { get; set; } = "";
    [Export] public string  SpritePathAlt       { get; set; } = "";
    [Export] public string  SpritePathVariant    { get; set; } = "";
    [Export] public string  SpritePathVariant2   { get; set; } = "";
    [Export] public float   SpriteScale          { get; set; } = 1f;
    [Export] public Vector2 SpriteOffset         { get; set; } = Vector2.Zero;
    [Export] public Vector2 SpriteOffsetAlt      { get; set; } = Vector2.Zero;
    [Export] public Vector2 SpriteOffsetVariant  { get; set; } = Vector2.Zero;
    [Export] public Vector2 SpriteOffsetVariant2 { get; set; } = Vector2.Zero;

    // ─── Требования к размещению ─────────────────────────────────────────────
    [Export] public bool           RequiresRoad      { get; set; } = true;
    [Export] public TileConstraint TileConstraint    { get; set; } = TileConstraint.None;
    /// <summary>Сколько рабочих (или граждан) нужно для работы здания. 0 = не требует.</summary>
    [Export] public int            WorkerCost        { get; set; } = 0;
    /// <summary>Если true — требуются Граждане (дома L2), а не обычные рабочие.</summary>
    [Export] public bool           RequiresCitizens  { get; set; } = false;

    // ─── Спрайт ───────────────────────────────────────────────────────────────

    // ─── Производство ────────────────────────────────────────────────────────
    [Export] public string OutputResourceId   { get; set; } = "";
    [Export] public int    OutputAmount       { get; set; } = 1;
    [Export] public float  ProductionCycleSec { get; set; } = 8f;
    [Export] public int    LocalInventoryCap  { get; set; } = 20;
}
