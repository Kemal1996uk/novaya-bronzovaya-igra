using Godot;

/// <summary>
/// Компонент Плавильни.
/// Каждые 30 сек потребляет 1 дерево + 1 медь + 1 олово → производит 1 бронзу.
/// Итого: 2 бронзы / мин, потребление 2🪵 + 2🟤 + 2🔘 / мин.
/// Требует 5 граждан (дома уровня 2) из WorkerManager.
/// </summary>
public partial class SmelterComponent : Node
{
    private const float CycleSec = 30f;

    private Timer              _timer;
    private InventoryComponent _inventory;
    private Building           _building;

    // ─── Состояние рабочих ────────────────────────────────────────────────────
    private bool _workersAcquired = false;
    private bool _setupDone       = false;

    // ─── Причина простоя ──────────────────────────────────────────────────────
    public string IdleReason  { get; private set; } = "";
    public bool   IsProducing => string.IsNullOrEmpty(IdleReason);
    public bool   HasWorkers  => _workersAcquired;

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _inventory = GetParent().GetNodeOrNull<InventoryComponent>("InventoryComponent");
        _building  = GetParent() as Building;

        _timer          = new Timer { Name = "SmelterTimer", WaitTime = CycleSec, OneShot = false };
        _timer.Timeout += OnCycle;
        AddChild(_timer);
        _timer.Start();
    }

    public override void _ExitTree()
    {
        if (_setupDone && WorkerManager.Instance != null)
        {
            WorkerManager.Instance.WorkersChanged -= TryAcquireWorkers;
            if (_workersAcquired)
                WorkerManager.Instance.ReleaseCitizens(5);
        }
    }

    // ─── Граждане ─────────────────────────────────────────────────────────────

    private void EnsureSetup()
    {
        if (_setupDone) return;
        _setupDone = true;
        WorkerManager.Instance.WorkersChanged += TryAcquireWorkers;
        TryAcquireWorkers();
    }

    private void TryAcquireWorkers()
    {
        if (_workersAcquired) return;
        if (WorkerManager.Instance.TryClaimCitizens(5))
        {
            _workersAcquired = true;
            GD.Print($"[Smelter] {_building?.Name}: получено 5 граждан");
        }
    }

    // ─── Производственный цикл ────────────────────────────────────────────────

    private void OnCycle()
    {
        EnsureSetup();

        if (_inventory == null) { SetIdleReason("Нет инвентаря"); return; }

        // Граждане
        if (!_workersAcquired)
        {
            TryAcquireWorkers();
            if (!_workersAcquired)
            {
                SetIdleReason($"Нет граждан (нужно 5, доступно {WorkerManager.Instance.AvailableCitizens})");
                return;
            }
        }

        // Дорога
        if (!HasRoad())
        {
            SetIdleReason("Нет дороги");
            return;
        }

        // Проверка входных ресурсов
        var rm = ResourceManager.Instance;
        if (!rm.CanAffordResource("wood",   1)) { SetIdleReason("Нет дров (нужно 1🪵/цикл)");   return; }
        if (!rm.CanAffordResource("copper", 1)) { SetIdleReason("Нет меди (нужно 1🟤/цикл)");   return; }
        if (!rm.CanAffordResource("tin",    1)) { SetIdleReason("Нет олова (нужно 1🔘/цикл)");  return; }

        // Место в инвентаре
        if (!_inventory.HasSpace(1)) { SetIdleReason("Буфер бронзы полон"); return; }

        // Плавить!
        rm.Spend("wood",   1);
        rm.Spend("copper", 1);
        rm.Spend("tin",    1);
        _inventory.TryAdd("bronze", 1);

        SetIdleReason("");
        GD.Print($"[Smelter] {_building?.Name}: +1 бронза (буфер: {_inventory.GetAmount("bronze")}/{_inventory.MaxCapacity})");
    }

    private void SetIdleReason(string reason)
    {
        if (IdleReason == reason) return;
        IdleReason = reason;
        _building?.QueueRedraw();
    }

    private bool HasRoad()
    {
        if (_building == null) return false;
        var data   = _building.Data;
        var tileMap = _building.GetTree().Root.FindChild("TileMap_Ground", true, false) as IsoTileMap;
        if (tileMap == null) return false;

        var dirs = new Vector2I[] { new(1,0), new(-1,0), new(0,1), new(0,-1) };
        for (int dx = 0; dx < data.FootprintSize.X; dx++)
        for (int dy = 0; dy < data.FootprintSize.Y; dy++)
        {
            var tile = _building.GridPosition + new Vector2I(dx, dy);
            foreach (var d in dirs)
                if (tileMap.GetTileType(tile + d) == TileType.Road) return true;
        }
        return false;
    }
}
