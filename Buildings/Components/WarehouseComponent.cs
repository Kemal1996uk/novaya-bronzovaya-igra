using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Компонент Склада. Управляет 3 тележками CartAgent.
/// Каждые 5 сек сканирует производственные здания с ресурсами
/// и отправляет свободную тележку к ближайшему.
///
/// Также рисует маршруты тележек при клике на склад (подсветка).
/// </summary>
public partial class WarehouseComponent : Node
{
    private const int   CartCount     = 3;
    private const float ScanInterval  = 5f;
    private const float StarvationSec = 60f; // поднять в топ если не собирали > 60 сек

    private Building         _building;
    private IsoTileMap       _tileMap;
    private Node2D           _cartsContainer;
    private List<CartAgent>  _carts = new();
    private float            _scanTimer;
    private bool             _showRoutes;

    // Время последнего сбора (для защиты от голодания дальних зданий)
    private readonly Dictionary<Building, float> _lastCollected = new();

    public override void _Ready()
    {
        _building = GetParent() as Building;
        EventBus.Instance.BuildingClicked += OnBuildingClicked;
    }

    public void Setup(IsoTileMap tileMap, Node2D cartsContainer)
    {
        _tileMap        = tileMap;
        _cartsContainer = cartsContainer;

        // Создаём 3 тележки
        for (int i = 0; i < CartCount; i++)
        {
            var cart = new CartAgent { Name = $"Cart_{i}" };
            cartsContainer.AddChild(cart);
            cart.Initialize(_building, this, tileMap);
            _carts.Add(cart);
        }
        GD.Print($"[Warehouse] {_building?.Name}: {CartCount} тележки готовы.");
    }

    public override void _Process(double delta)
    {
        _scanTimer += (float)delta;
        if (_scanTimer < ScanInterval) return;
        _scanTimer = 0f;
        DispatchCarts();
    }

    // ─── Диспетчеризация ──────────────────────────────────────────────────────

    private void DispatchCarts()
    {
        if (BuildingRegistry.Instance == null) return;

        float now = Time.GetTicksMsec() / 1000f;

        // Собрать здания с ресурсами, отсортировать по приоритету:
        // 1. Давно не собирались (> StarvationSec) — принудительно в топ
        // 2. Остальные — по убыванию FillPercent
        var sources = new List<(Building b, float priority)>();
        foreach (var b in BuildingRegistry.Instance.ProductionBuildings())
        {
            var inv = b.GetNodeOrNull<InventoryComponent>("InventoryComponent");
            if (inv == null || inv.TotalCount == 0) continue;

            _lastCollected.TryGetValue(b, out float lastTime);
            float elapsed = now - lastTime;

            // Приоритет: голодающие получают +1000, остальные — FillPercent
            float priority = elapsed > StarvationSec
                ? 1000f + inv.FillPercent
                : inv.FillPercent;

            sources.Add((b, priority));
        }

        if (sources.Count == 0) return;

        // Убывающий порядок приоритета
        sources.Sort((a, b) => b.priority.CompareTo(a.priority));

        int srcIdx = 0;
        foreach (var cart in _carts)
        {
            if (!cart.IsAvailable)       continue;
            if (srcIdx >= sources.Count) break;

            var target = sources[srcIdx].b;
            _lastCollected[target] = now;
            cart.StartTrip(target);
            srcIdx++;
        }
    }

    // ─── Подсветка маршрутов ──────────────────────────────────────────────────

    private void OnBuildingClicked(Node building)
    {
        _showRoutes = (building == _building);
        _building.QueueRedraw(); // перерисовать склад с маршрутами
    }

    /// <summary>Вызывается из Building._Draw() если showRoutes=true.</summary>
    public void DrawRoutes(Node2D canvas)
    {
        if (!_showRoutes) return;
        foreach (var cart in _carts)
        {
            var path = cart.CurrentPath;
            if (path == null || path.Length < 2) continue;

            for (int i = 0; i < path.Length - 1; i++)
            {
                var from = path[i]   - canvas.GlobalPosition;
                var to   = path[i+1] - canvas.GlobalPosition;
                canvas.DrawLine(from, to, new Color(1, 0.8f, 0.2f, 0.8f), 3f);

                // Стрелка
                var dir     = (to - from).Normalized();
                var mid     = (from + to) * 0.5f;
                var perp    = new Vector2(-dir.Y, dir.X) * 5f;
                canvas.DrawLine(mid, mid - dir * 8f + perp, new Color(1, 0.8f, 0.2f, 0.8f), 2f);
                canvas.DrawLine(mid, mid - dir * 8f - perp, new Color(1, 0.8f, 0.2f, 0.8f), 2f);
            }
        }
    }
}
