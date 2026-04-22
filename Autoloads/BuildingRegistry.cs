using Godot;
using System.Collections.Generic;

/// <summary>
/// Глобальный реестр зданий. Размещение, проверки, список всех построенных зданий.
///
/// CanPlace проверяет:
///   1. Тайлы в границах карты
///   2. Тайлы не заняты
///   3. Тайлы — buildable (Grass/Sand)
///   4. TileConstraint (NearForest / NearRock / NearWater)
///   5. RequiresRoad — хотя бы 1 смежный тайл = Road
///   6. Хватает ресурсов (gold + wood + stone)
/// </summary>
public partial class BuildingRegistry : Node
{
    public static BuildingRegistry Instance { get; private set; }

    private readonly List<Building>    _buildings     = new();
    private readonly HashSet<Vector2I> _occupiedTiles = new();

    private Node2D     _buildingContainer;
    private IsoTileMap _tileMap;

    public override void _Ready() => Instance = this;

    public void Initialize(Node2D buildingContainer, IsoTileMap tileMap)
    {
        _buildingContainer = buildingContainer;
        _tileMap           = tileMap;
    }

    // ─── Проверка ─────────────────────────────────────────────────────────────

    public bool CanPlace(BuildingData data, Vector2I anchorTile)
    {
        if (_tileMap == null) return false;

        // 1. Все тайлы в пределах карты, незаняты и buildable
        for (int dx = 0; dx < data.FootprintSize.X; dx++)
        for (int dy = 0; dy < data.FootprintSize.Y; dy++)
        {
            var tile = anchorTile + new Vector2I(dx, dy);
            if (!_tileMap.IsValidTile(tile))        return false;
            if (_occupiedTiles.Contains(tile))       return false;
            if (!_tileMap.IsBuildable(tile))         return false;
        }

        // 2. TileConstraint — проверяем по всем тайлам footprint + adjacent
        if (!CheckTileConstraint(data, anchorTile)) return false;

        // 3. Ресурсы (дорога НЕ требуется при размещении — только для работы здания)
        if (!ResourceManager.Instance.CanAfford(data.GoldCost, data.WoodCost, data.StoneCost))
            return false;

        return true;
    }

    private bool CheckTileConstraint(BuildingData data, Vector2I anchor)
    {
        if (data.TileConstraint == TileConstraint.None) return true;

        var needed = data.TileConstraint switch
        {
            TileConstraint.NearForest    => TileType.Forest,
            TileConstraint.NearRock      => TileType.Rock,
            TileConstraint.NearWater     => TileType.Water,
            TileConstraint.NearCopperOre => TileType.CopperOre,
            TileConstraint.NearTinOre    => TileType.TinOre,
            _                            => TileType.Grass,  // None — не используется в этом блоке
        };

        // Проверяем в радиусе 3 тайла от каждого тайла footprint
        for (int dx = 0; dx < data.FootprintSize.X; dx++)
        for (int dy = 0; dy < data.FootprintSize.Y; dy++)
        {
            var tile = anchor + new Vector2I(dx, dy);
            if (_tileMap.IsWithinRadiusOfType(tile, needed, 3)) return true;
        }
        return false;
    }

    // ─── Размещение ───────────────────────────────────────────────────────────

    public Building PlaceBuilding(BuildingData data, Vector2I anchorTile)
    {
        if (!CanPlace(data, anchorTile))
        {
            GD.PrintErr($"[BuildingRegistry] Нельзя построить {data.DisplayName} на {anchorTile}");
            return null;
        }

        // Списать стоимость
        ResourceManager.Instance.SpendBuildCost(data.GoldCost, data.WoodCost, data.StoneCost);

        var building = new Building();

        // ── Компоненты ────────────────────────────────────────────────────────
        if (data.LocalInventoryCap > 0)
        {
            building.AddChild(new InventoryComponent
            {
                Name        = "InventoryComponent",
                MaxCapacity = data.LocalInventoryCap,
            });
        }

        if (!string.IsNullOrEmpty(data.OutputResourceId))
        {
            building.AddChild(new ProductionCycleComponent
            {
                Name             = "ProductionCycleComponent",
                OutputResourceId = data.OutputResourceId,
                OutputAmount     = data.OutputAmount,
                CycleSec         = data.ProductionCycleSec,
            });
        }

        // Склад — добавляем WarehouseComponent
        if (data.BuildingId == "warehouse")
        {
            building.AddChild(new WarehouseComponent { Name = "WarehouseComponent" });
        }

        // Ферма — добавляем FarmFieldComponent
        if (data.BuildingId == "farm")
        {
            building.AddChild(new FarmFieldComponent { Name = "FarmFieldComponent" });
        }

        // Дом — добавляем HouseLevelComponent
        if (data.BuildingId == "house")
        {
            building.AddChild(new HouseLevelComponent { Name = "HouseLevelComponent" });
        }

        // Плавильня — добавляем SmelterComponent вместо ProductionCycleComponent
        if (data.BuildingId == "smelter")
        {
            building.AddChild(new SmelterComponent { Name = "SmelterComponent" });
        }

        _buildingContainer.AddChild(building);
        building.Initialize(data, anchorTile, _tileMap);

        // Пометить тайлы занятыми
        for (int dx = 0; dx < data.FootprintSize.X; dx++)
        for (int dy = 0; dy < data.FootprintSize.Y; dy++)
            _occupiedTiles.Add(anchorTile + new Vector2I(dx, dy));

        _buildings.Add(building);
        EventBus.Instance.EmitSignal(EventBus.SignalName.BuildingPlaced, building);

        GD.Print($"[BuildingRegistry] ✓ {data.DisplayName} на {anchorTile}  " +
                 $"[{data.GoldCost}Д / {data.WoodCost}Д / {data.StoneCost}К]");
        return building;
    }

    // ─── Снос ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Сносит здание: освобождает тайлы, возвращает 50% стоимости золотом.
    /// </summary>
    public bool DemolishBuilding(Building building)
    {
        if (building == null || !_buildings.Contains(building)) return false;

        var data = building.Data;
        var anchor = building.GridPosition;

        // Освободить тайлы
        for (int dx = 0; dx < data.FootprintSize.X; dx++)
        for (int dy = 0; dy < data.FootprintSize.Y; dy++)
            _occupiedTiles.Remove(anchor + new Vector2I(dx, dy));

        _buildings.Remove(building);

        // Возврат 50% стоимости золотом
        int refund = data.GoldCost / 2;
        if (refund > 0) ResourceManager.Instance.Add("gold", refund);

        EventBus.Instance.EmitSignal(EventBus.SignalName.BuildingDemolished, building);
        building.QueueFree();

        GD.Print($"[BuildingRegistry] Снесено: {data.DisplayName}. Возврат: {refund}Д");
        return true;
    }

    // ─── Запросы ──────────────────────────────────────────────────────────────

    public bool IsTileOccupied(Vector2I tile)   => _occupiedTiles.Contains(tile);
    public IReadOnlyList<Building> AllBuildings  => _buildings;
    public int BuildingCount                     => _buildings.Count;

    /// <summary>Все здания с InventoryComponent (источники ресурсов для тележек).</summary>
    public IEnumerable<Building> ProductionBuildings()
    {
        foreach (var b in _buildings)
            if (b.GetNodeOrNull<InventoryComponent>("InventoryComponent") != null
                && b.Data?.BuildingId != "warehouse")
                yield return b;
    }
}
