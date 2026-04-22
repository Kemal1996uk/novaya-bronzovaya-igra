using Godot;
using System.Collections.Generic;

/// <summary>
/// Autoload. Хранит глобальный запас ресурсов (GlobalStockpile).
/// Склад пополняется тележками; расходуется на строительство и домами.
///
/// Стартовые ресурсы устанавливаются в GameManager._Ready().
/// </summary>
public partial class ResourceManager : Node
{
    public static ResourceManager Instance { get; private set; }

    // GlobalStockpile — единый склад всего города
    private readonly Dictionary<string, int> _stockpile = new()
    {
        ["wood"]   = 60,
        ["stone"]  = 50,
        ["grain"]  = 10,
        ["fish"]   = 10,
        ["gold"]   = 15000,
        ["copper"] = 0,
        ["tin"]    = 0,
        ["bronze"] = 0,
    };

    public override void _Ready() => Instance = this;

    // ─── Чтение ───────────────────────────────────────────────────────────────

    public int Get(string id)
    {
        _stockpile.TryGetValue(id, out int v);
        return v;
    }

    public IReadOnlyDictionary<string, int> All => _stockpile;

    // ─── Добавление ───────────────────────────────────────────────────────────

    public void Add(string id, int amount)
    {
        if (amount <= 0) return;
        _stockpile.TryGetValue(id, out int cur);
        _stockpile[id] = cur + amount;
        EventBus.Instance.EmitSignal(EventBus.SignalName.StockpileChanged);
    }

    // ─── Трата ────────────────────────────────────────────────────────────────

    public bool CanAfford(int gold, int wood, int stone)
        => Get("gold") >= gold && Get("wood") >= wood && Get("stone") >= stone;

    public bool CanAffordResource(string id, int amount)
        => Get(id) >= amount;

    /// <summary>Списать ресурс; возвращает false если не хватает.</summary>
    public bool Spend(string id, int amount)
    {
        if (amount <= 0) return true;
        if (Get(id) < amount) return false;
        _stockpile[id] -= amount;
        EventBus.Instance.EmitSignal(EventBus.SignalName.StockpileChanged);
        EventBus.Instance.EmitSignal(EventBus.SignalName.ResourceConsumed, id, amount);
        return true;
    }

    /// <summary>Списать золото + дерево + камень за постройку.</summary>
    public bool SpendBuildCost(int gold, int wood, int stone)
    {
        if (!CanAfford(gold, wood, stone)) return false;
        if (gold  > 0) { _stockpile["gold"]  -= gold;  }
        if (wood  > 0) { _stockpile["wood"]  -= wood;  }
        if (stone > 0) { _stockpile["stone"] -= stone; }
        EventBus.Instance.EmitSignal(EventBus.SignalName.StockpileChanged);
        return true;
    }

    // ─── Дорога ───────────────────────────────────────────────────────────────

    public const int RoadCostPerTile = 100; // Драхм за тайл

    public bool CanAffordRoad(int tileCount)
        => Get("gold") >= tileCount * RoadCostPerTile;

    public bool SpendRoad(int tileCount)
        => Spend("gold", tileCount * RoadCostPerTile);
}
