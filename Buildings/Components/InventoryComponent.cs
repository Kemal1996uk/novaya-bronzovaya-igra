using Godot;
using System.Collections.Generic;

/// <summary>
/// Локальный буфер ресурсов на здании.
/// Производственные здания накапливают ресурс здесь;
/// тележки Склада забирают его отсюда в GlobalStockpile.
/// </summary>
public partial class InventoryComponent : Node
{
    public int MaxCapacity { get; set; } = 20;

    private readonly Dictionary<string, int> _items = new();
    public IReadOnlyDictionary<string, int> Items => _items;

    public int TotalCount
    {
        get { int t = 0; foreach (var v in _items.Values) t += v; return t; }
    }

    // ─── Добавление ───────────────────────────────────────────────────────────

    public int TryAdd(string resourceId, int amount)
    {
        int canAdd = Mathf.Min(amount, MaxCapacity - TotalCount);
        if (canAdd <= 0) return 0;
        _items.TryGetValue(resourceId, out int cur);
        _items[resourceId] = cur + canAdd;
        return canAdd;
    }

    // ─── Изъятие ──────────────────────────────────────────────────────────────

    /// <summary>Изъять до amount единиц. Возвращает фактически изъятое.</summary>
    public int TryRemove(string resourceId, int amount)
    {
        if (!_items.TryGetValue(resourceId, out int cur)) return 0;
        int take = Mathf.Min(amount, cur);
        int left = cur - take;
        if (left == 0) _items.Remove(resourceId);
        else           _items[resourceId] = left;
        return take;
    }

    // ─── Запросы ──────────────────────────────────────────────────────────────

    public int  GetAmount(string resourceId) { _items.TryGetValue(resourceId, out int v); return v; }
    public bool HasSpace(int amount = 1)     => TotalCount + amount <= MaxCapacity;
    public bool HasResource(string id, int amount = 1)
        => _items.TryGetValue(id, out int v) && v >= amount;

    /// <summary>Первый ресурс с ненулевым количеством (для тележки).</summary>
    public (string id, int amount) FirstResource()
    {
        foreach (var kv in _items)
            if (kv.Value > 0) return (kv.Key, kv.Value);
        return (null, 0);
    }

    /// <summary>Производительность в % (сколько заполнен склад).</summary>
    public float FillPercent => MaxCapacity > 0 ? (float)TotalCount / MaxCapacity : 0f;
}
