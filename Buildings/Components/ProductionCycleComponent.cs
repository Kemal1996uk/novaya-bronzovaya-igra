using Godot;

/// <summary>
/// Таймер производства. Каждые CycleSec добавляет OutputAmount ресурса
/// в InventoryComponent здания.
///
/// Рабочие берутся из глобального пула WorkerManager автоматически:
///  — при первом цикле резервируются (TryClaimWorkers / TryClaimCitizens)
///  — при сносе здания освобождаются
///  — при появлении новых рабочих (WorkersChanged) — пытается захватить
/// </summary>
public partial class ProductionCycleComponent : Node
{
    public string OutputResourceId { get; set; } = "";
    public int    OutputAmount     { get; set; } = 1;
    public float  CycleSec        { get; set; } = 8f;

    private Timer              _timer;
    private InventoryComponent _inventory;
    private Building           _building;
    private IsoTileMap         _tileMap;

    // ─── Состояние рабочих ────────────────────────────────────────────────────
    private bool _workersAcquired    = false;
    private bool _workerSetupDone    = false;
    private int  _myWorkerCost       = 0;
    private bool _myRequiresCitizens = false;

    // ─── Причина простоя ──────────────────────────────────────────────────────
    public string IdleReason { get; private set; } = "";
    public bool   IsProducing => string.IsNullOrEmpty(IdleReason);
    public bool   HasWorkers  => _myWorkerCost <= 0 || _workersAcquired;

    public override void _Ready()
    {
        _inventory = GetParent().GetNodeOrNull<InventoryComponent>("InventoryComponent");
        _building  = GetParent() as Building;

        _timer          = new Timer { Name = "ProdTimer", WaitTime = CycleSec, OneShot = false };
        _timer.Timeout += OnCycle;
        AddChild(_timer);
        _timer.Start();
    }

    public override void _ExitTree()
    {
        if (_workerSetupDone && _myWorkerCost > 0 && WorkerManager.Instance != null)
        {
            WorkerManager.Instance.WorkersChanged -= TryAcquireWorkers;
            if (_workersAcquired)
            {
                if (_myRequiresCitizens)
                    WorkerManager.Instance.ReleaseCitizens(_myWorkerCost);
                else
                    WorkerManager.Instance.ReleaseWorkers(_myWorkerCost);
            }
        }
    }

    // ─── Рабочие: ленивая инициализация ──────────────────────────────────────

    private void EnsureWorkerSetup()
    {
        if (_workerSetupDone) return;
        var data = _building?.Data;
        if (data == null) return;           // Initialize ещё не вызван

        _myWorkerCost       = data.WorkerCost;
        _myRequiresCitizens = data.RequiresCitizens;
        _workerSetupDone    = true;

        if (_myWorkerCost > 0)
        {
            WorkerManager.Instance.WorkersChanged += TryAcquireWorkers;
            TryAcquireWorkers();
        }
    }

    private void TryAcquireWorkers()
    {
        if (_workersAcquired || _myWorkerCost <= 0) return;

        bool ok = _myRequiresCitizens
            ? WorkerManager.Instance.TryClaimCitizens(_myWorkerCost)
            : WorkerManager.Instance.TryClaimWorkers(_myWorkerCost);

        if (ok)
        {
            _workersAcquired = true;
            GD.Print($"[Workers] {_building?.Name}: получили {_myWorkerCost} " +
                     (_myRequiresCitizens ? "граждан" : "иммигрантов"));
        }
    }

    // ─── Производственный цикл ────────────────────────────────────────────────

    private void OnCycle()
    {
        EnsureWorkerSetup();

        if (_inventory == null) { SetIdleReason("Нет инвентаря"); return; }
        if (_building  == null) { SetIdleReason("Нет здания");   return; }

        if (_tileMap == null)
            _tileMap = _building.GetTree().Root.FindChild("TileMap_Ground", true, false) as IsoTileMap;

        if (_tileMap != null)
        {
            var data = _building.Data;
            if (data != null)
            {
                if (data.TileConstraint != TileConstraint.None)
                {
                    var needed = data.TileConstraint switch
                    {
                        TileConstraint.NearForest    => TileType.Forest,
                        TileConstraint.NearRock      => TileType.Rock,
                        TileConstraint.NearWater     => TileType.Water,
                        TileConstraint.NearCopperOre => TileType.CopperOre,
                        TileConstraint.NearTinOre    => TileType.TinOre,
                        _                            => TileType.Grass,
                    };
                    bool found = false;
                    for (int dx = 0; dx < data.FootprintSize.X && !found; dx++)
                    for (int dy = 0; dy < data.FootprintSize.Y && !found; dy++)
                        if (_tileMap.IsWithinRadiusOfType(_building.GridPosition + new Vector2I(dx, dy), needed, 3))
                            found = true;

                    if (!found)
                    {
                        SetIdleReason($"Нет {needed} рядом");
                        return;
                    }
                }

                if (data.RequiresRoad && !HasRoad(data))
                {
                    SetIdleReason("Нет дороги");
                    return;
                }
            }
        }

        // Рабочие из глобального пула
        if (_myWorkerCost > 0 && !_workersAcquired)
        {
            TryAcquireWorkers();
            if (!_workersAcquired)
            {
                string kind = _myRequiresCitizens ? "граждан" : "иммигрантов";
                SetIdleReason($"Нет {kind} (нужно {_myWorkerCost})");
                return;
            }
        }

        // Ферма: нужны поля
        var farm = GetParent().GetNodeOrNull<FarmFieldComponent>("FarmFieldComponent");
        if (farm != null && !farm.IsReady)
        {
            SetIdleReason($"Нужно {FarmFieldComponent.RequiredFields} поля, есть {farm.FieldCount}");
            return;
        }

        if (!_inventory.HasSpace(OutputAmount))
        {
            SetIdleReason("Склад полон");
            return;
        }

        int added = _inventory.TryAdd(OutputResourceId, OutputAmount);
        if (added > 0)
        {
            SetIdleReason("");
            GD.Print($"[Production] {_building?.Name}: +{added} {OutputResourceId}" +
                     $" (буфер: {_inventory.GetAmount(OutputResourceId)}/{_inventory.MaxCapacity})");
        }
    }

    private void SetIdleReason(string reason)
    {
        if (IdleReason == reason) return;
        IdleReason = reason;
        _building?.QueueRedraw();
    }

    private bool HasRoad(BuildingData data)
    {
        if (_tileMap == null) return false;
        var dirs = new Vector2I[] { new(1,0), new(-1,0), new(0,1), new(0,-1) };
        for (int dx = 0; dx < data.FootprintSize.X; dx++)
        for (int dy = 0; dy < data.FootprintSize.Y; dy++)
        {
            var tile = _building.GridPosition + new Vector2I(dx, dy);
            foreach (var d in dirs)
                if (_tileMap.GetTileType(tile + d) == TileType.Road) return true;
        }
        return false;
    }
}
