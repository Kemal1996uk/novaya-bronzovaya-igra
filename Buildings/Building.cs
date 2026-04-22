using Godot;
using System.Collections.Generic;

/// <summary>
/// Базовый класс здания.
/// Отрисовывает объёмный изометрический блок или спрайт.
/// Показывает красный ! над зданием когда производство стоит.
/// </summary>
public partial class Building : Node2D
{
    public BuildingData Data         { get; private set; }
    public Vector2I     GridPosition { get; private set; }
    public bool IsSelected { get; private set; } = false;

    private (Vector2 offset, int depth)[] _tiles;
    private bool _hasSprite = false;

    // Счётчик для чередования вариантов спрайта L1 (по типу здания)
    private static readonly Dictionary<string, int> _variantCounter = new();

    private EventBus.BuildingClickedEventHandler _onBuildingClicked;

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _onBuildingClicked = (node) =>
        {
            bool was = IsSelected;
            IsSelected = (node == this);
            if (was != IsSelected) QueueRedraw();
        };
        EventBus.Instance.BuildingClicked += _onBuildingClicked;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.BuildingClicked -= _onBuildingClicked;
    }

    public void Initialize(BuildingData data, Vector2I gridPos, IsoTileMap tileMap)
    {
        Data         = data;
        GridPosition = gridPos;
        Name         = $"{data.BuildingId}_{gridPos.X}_{gridPos.Y}";

        ComputeTiles(gridPos, tileMap);
        GlobalPosition = tileMap.GetTileWorldCenter(gridPos);

        ZIndex = (GridPosition.X + Data.FootprintSize.X - 1)
               + (GridPosition.Y + Data.FootprintSize.Y - 1);

        SetupSprite();
        QueueRedraw();
    }

    // ─── Спрайт ───────────────────────────────────────────────────────────────

    private void SetupSprite()
    {
        if (Data == null || string.IsNullOrEmpty(Data.SpritePath)) return;

        // Выбираем вариант спрайта: чередуем SpritePath и SpritePathVariant
        string  path   = Data.SpritePath;
        Vector2 offset = Data.SpriteOffset;

        if (!string.IsNullOrEmpty(Data.SpritePathVariant))
        {
            if (!_variantCounter.TryGetValue(Data.BuildingId, out int count))
                count = 0;

            bool has3 = !string.IsNullOrEmpty(Data.SpritePathVariant2);
            int  mod  = has3 ? 3 : 2;

            int slot = count % mod;
            if (slot == 1)
            {
                path   = Data.SpritePathVariant;
                offset = Data.SpriteOffsetVariant;
            }
            else if (slot == 2)
            {
                path   = Data.SpritePathVariant2;
                offset = Data.SpriteOffsetVariant2;
            }

            _variantCounter[Data.BuildingId] = count + 1;
        }

        var tex = GD.Load<Texture2D>(path);
        if (tex == null)
        {
            GD.PrintErr($"[Building] Спрайт не найден: {path}");
            return;
        }

        var sprite      = new Sprite2D { Name = "Sprite" };
        sprite.Texture  = tex;
        sprite.Scale    = Vector2.One * Data.SpriteScale;
        sprite.Offset   = offset;
        AddChild(sprite);
        _hasSprite = true;
    }

    /// <summary>Заменить спрайт (вызывается при апгрейде дома).</summary>
    public void SwapSprite(string path, Vector2 offset)
    {
        if (string.IsNullOrEmpty(path)) return;
        var tex = GD.Load<Texture2D>(path);
        if (tex == null) { GD.PrintErr($"[Building] SwapSprite: не найден {path}"); return; }

        var sprite = GetNodeOrNull<Sprite2D>("Sprite");
        if (sprite == null)
        {
            // Спрайт ещё не был создан — создаём
            sprite        = new Sprite2D { Name = "Sprite" };
            sprite.Scale  = Vector2.One * (Data?.SpriteScale ?? 1f);
            AddChild(sprite);
            _hasSprite = true;
        }
        sprite.Texture = tex;
        sprite.Offset  = offset;
        QueueRedraw();
    }

    // ─── Отрисовка ────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (Data == null || _tiles == null) return;

        // Процедурный куб (только если нет спрайта)
        if (!_hasSprite)
        {
            foreach (var (offset, _) in _tiles)
                DrawIsoCube(offset, Data.PlaceholderColor, Data.WallHeight);
        }

        // Маршруты тележек (только для склада при выделении)
        GetNodeOrNull<WarehouseComponent>("WarehouseComponent")?.DrawRoutes(this);

        // Алерт ! над зданием (производство стоит)
        var prod  = GetNodeOrNull<ProductionCycleComponent>("ProductionCycleComponent");
        var smelt = GetNodeOrNull<SmelterComponent>("SmelterComponent");
        bool hasAlert = (prod  != null && !prod.IsProducing)
                     || (smelt != null && !smelt.IsProducing);

        if (hasAlert)
        {
            float ay = -32f - Data.WallHeight - 16f;
            DrawCircle(new Vector2(0f, ay), 11f, new Color(0.88f, 0.12f, 0.12f, 0.92f));
            DrawString(ThemeDB.FallbackFont,
                       new Vector2(-4f, ay + 7f), "!",
                       HorizontalAlignment.Left, -1, 18, Colors.White);
        }
    }

    // ─── Изометрический куб ───────────────────────────────────────────────────

    private void DrawIsoCube(Vector2 center, Color baseColor, int wallH)
    {
        var A = center + new Vector2(  0, -32 - wallH);
        var B = center + new Vector2( 64,   0 - wallH);
        var C = center + new Vector2(  0,  32 - wallH);
        var D = center + new Vector2(-64,   0 - wallH);

        var Bbot = center + new Vector2( 64,   0);
        var Cbot = center + new Vector2(  0,  32);
        var Dbot = center + new Vector2(-64,   0);

        var topColor   = baseColor.Lightened(0.28f);
        var rightColor = baseColor;
        var leftColor  = baseColor.Darkened(0.32f);
        var edgeColor  = baseColor.Darkened(0.55f);

        DrawColoredPolygon(new[] { A, B, C, D }, topColor);
        DrawColoredPolygon(new[] { B, C, Cbot, Bbot }, rightColor);
        DrawColoredPolygon(new[] { D, C, Cbot, Dbot }, leftColor);

        DrawPolyline(new[] { A, B, C, D, A }, edgeColor, 1.0f);
        DrawLine(B, Bbot, edgeColor, 1.0f);
        DrawLine(C, Cbot, edgeColor, 1.5f);
        DrawLine(D, Dbot, edgeColor, 1.0f);
        DrawLine(Bbot, Cbot, edgeColor, 1.0f);
        DrawLine(Dbot, Cbot, edgeColor, 1.0f);
    }

    // ─── Вспомогательные ──────────────────────────────────────────────────────

    private void ComputeTiles(Vector2I anchorTile, IsoTileMap tileMap)
    {
        var anchorLocal = tileMap.MapToLocal(anchorTile);
        var list = new List<(Vector2 offset, int depth)>();

        for (int dx = 0; dx < Data.FootprintSize.X; dx++)
            for (int dy = 0; dy < Data.FootprintSize.Y; dy++)
            {
                var local = tileMap.MapToLocal(anchorTile + new Vector2I(dx, dy));
                list.Add((local - anchorLocal, dx + dy));
            }

        list.Sort((a, b) => a.depth.CompareTo(b.depth));
        _tiles = list.ToArray();
    }
}
