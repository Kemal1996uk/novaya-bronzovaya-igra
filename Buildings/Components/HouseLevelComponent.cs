using Godot;

/// <summary>
/// Компонент жилого дома (уровни 1 и 2).
///
/// Уровень 1 — Жители: доход 100/50 Д/мин, дают 5 рабочих в пул.
/// Уровень 2 — Граждане: доход 200/100 Д/мин, дают 5 граждан в пул.
/// Апгрейд: Full-довольство + 2 дерева + 2 камня.
///
/// Потребление: 1 рыба + 1 зерно раз в 60 сек.
/// </summary>
public partial class HouseLevelComponent : Node
{
    // ─── Константы ────────────────────────────────────────────────────────────

    public const int Residents = 5;

    private const float ConsumeInterval = 60f;
    private const float GoldInterval    = 60f;

    // ─── Уровень ──────────────────────────────────────────────────────────────

    public int Level { get; private set; } = 1;

    // Доход зависит от уровня (устанавливается при апгрейде)
    private int _goldFull    = 100;
    private int _goldPartial = 50;

    // ─── Состояние ────────────────────────────────────────────────────────────

    public enum SatisfactionState { None, Partial, Full }

    public SatisfactionState Satisfaction { get; private set; } = SatisfactionState.None;
    public bool LastCycleSatisfied => Satisfaction != SatisfactionState.None;
    public float ConsumeProgress   => Mathf.Clamp(_consumeTimer / ConsumeInterval, 0f, 1f);

    // ─── Приватные поля ───────────────────────────────────────────────────────

    private float    _consumeTimer;
    private float    _goldTimer;
    private Building _building;
    private bool     _registered = false;

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        _building = GetParent() as Building;
        WorkerManager.Instance.RegisterHouse(Level);
        _registered = true;
        EventBus.Instance.BuildingDemolished += OnBuildingDemolished;
    }

    public override void _ExitTree()
    {
        if (_registered && WorkerManager.Instance != null)
            WorkerManager.Instance.UnregisterHouse(Level);
    }

    private void OnBuildingDemolished(Node building)
    {
        // Ничего не нужно — AssignedTo убран, пул управляется через _ExitTree
    }

    // ─── Апгрейд ──────────────────────────────────────────────────────────────

    /// <summary>True если все условия апгрейда выполнены.</summary>
    public bool CanUpgrade()
        => Level == 1
        && Satisfaction == SatisfactionState.Full
        && ResourceManager.Instance.CanAfford(0, 2, 2);

    /// <summary>
    /// Апгрейд дома до уровня 2. Меняет спрайт, доход, тип жителей в пуле.
    /// </summary>
    public void Upgrade()
    {
        if (!CanUpgrade()) return;

        ResourceManager.Instance.SpendBuildCost(0, 2, 2);
        Level        = 2;
        _goldFull    = 200;
        _goldPartial = 100;

        WorkerManager.Instance.UpgradeHouseToL2();

        // Сменить спрайт через Building
        var data = _building?.Data;
        if (data != null && !string.IsNullOrEmpty(data.SpritePathAlt))
            _building.SwapSprite(data.SpritePathAlt, data.SpriteOffsetAlt);

        GD.Print($"[House] {_building?.Name}: апгрейд до уровня 2 — Граждане");
    }

    // ─── Цикл ─────────────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // ── Доход ─────────────────────────────────────────────────────────────
        int goldRate = Satisfaction switch
        {
            SatisfactionState.Full    => _goldFull,
            SatisfactionState.Partial => _goldPartial,
            _                         => 0,
        };

        if (goldRate > 0)
        {
            _goldTimer += dt;
            if (_goldTimer >= GoldInterval)
            {
                _goldTimer = 0f;
                ResourceManager.Instance.Add("gold", goldRate);
                GD.Print($"[House] {_building?.Name}: +{goldRate}🪙 " +
                         $"(L{Level}, {(Satisfaction == SatisfactionState.Full ? "сыты" : "частично")})");
            }
        }
        else
        {
            _goldTimer = 0f;
        }

        // ── Потребление ───────────────────────────────────────────────────────
        _consumeTimer += dt;
        if (_consumeTimer < ConsumeInterval) return;
        _consumeTimer = 0f;

        bool hasFish  = ResourceManager.Instance.CanAffordResource("fish",  1);
        bool hasGrain = ResourceManager.Instance.CanAffordResource("grain", 1);

        if (hasFish && hasGrain)
        {
            ResourceManager.Instance.Spend("fish",  1);
            ResourceManager.Instance.Spend("grain", 1);
            Satisfaction = SatisfactionState.Full;
        }
        else if (hasFish)
        {
            ResourceManager.Instance.Spend("fish", 1);
            Satisfaction = SatisfactionState.Partial;
        }
        else if (hasGrain)
        {
            ResourceManager.Instance.Spend("grain", 1);
            Satisfaction = SatisfactionState.Partial;
        }
        else
        {
            Satisfaction = SatisfactionState.None;
        }
    }
}
