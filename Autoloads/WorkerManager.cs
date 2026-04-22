using Godot;

/// <summary>
/// Autoload. Глобальный пул иммигрантов и граждан.
///
/// Иммигранты (Workers)  — дают дома уровня 1 (по 5 с дома).
///   Работают в: лесопилках, рыболовнях, фермах, каменотёсах, шахтах.
/// Граждане (Citizens) — дают дома уровня 2 (по 5 с дома).
///   Работают только в: плавильне.
///
/// Если все дома улучшены до L2 → иммигрантов нет → базовое производство стоит.
/// Это намеренная игровая механика напряжения.
///
/// AvailableWorkers  = (TotalImmigrants + TotalCitizens) − _usedWorkers − _usedCitizens
/// AvailableCitizens = TotalCitizens − _usedCitizens
/// </summary>
public partial class WorkerManager : Node
{
    public static WorkerManager Instance { get; private set; }

    private int _totalWorkers  = 0;  // иммигранты (дома L1)
    private int _totalCitizens = 0;  // граждане   (дома L2)
    private int _usedWorkers   = 0;
    private int _usedCitizens  = 0;

    // Счётчики домов
    private int _housesL1 = 0;
    private int _housesL2 = 0;

    // ─── Свойства (только чтение) ─────────────────────────────────────────────

    public int TotalWorkers      => _totalWorkers;
    public int TotalCitizens     => _totalCitizens;
    public int TotalAll          => _totalWorkers + _totalCitizens;

    /// <summary>Псевдоним: иммигранты = жители домов L1.</summary>
    public int TotalImmigrants   => _totalWorkers;

    /// <summary>Сколько домов L1 на карте.</summary>
    public int HousesL1          => _housesL1;

    /// <summary>Сколько домов L2 на карте.</summary>
    public int HousesL2          => _housesL2;

    /// <summary>Иммигрантов доступно для базовых производств.</summary>
    public int AvailableWorkers  => TotalAll - _usedWorkers - _usedCitizens;

    /// <summary>Псевдоним: доступные иммигранты.</summary>
    public int AvailableImmigrants => AvailableWorkers;

    /// <summary>Граждан доступно (только для плавильни).</summary>
    public int AvailableCitizens => _totalCitizens - _usedCitizens;

    /// <summary>Сколько иммигрантов занято в производствах.</summary>
    public int UsedWorkers   => _usedWorkers;

    /// <summary>Сколько граждан занято в производствах.</summary>
    public int UsedCitizens  => _usedCitizens;

    // ─── Сигнал ───────────────────────────────────────────────────────────────

    [Signal] public delegate void WorkersChangedEventHandler();

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready() => Instance = this;

    // ─── Регистрация домов ────────────────────────────────────────────────────

    /// <summary>Вызывается HouseLevelComponent при размещении дома.</summary>
    public void RegisterHouse(int level)
    {
        if (level >= 2) { _totalCitizens += 5; _housesL2++; }
        else            { _totalWorkers  += 5; _housesL1++; }
        EmitSignal(SignalName.WorkersChanged);
    }

    /// <summary>Вызывается HouseLevelComponent при сносе.</summary>
    public void UnregisterHouse(int level)
    {
        if (level >= 2)
        {
            _totalCitizens = Mathf.Max(0, _totalCitizens - 5);
            _usedCitizens  = Mathf.Min(_usedCitizens, _totalCitizens);
            _housesL2      = Mathf.Max(0, _housesL2 - 1);
        }
        else
        {
            _totalWorkers = Mathf.Max(0, _totalWorkers - 5);
            // Общий пул уменьшился — ограничиваем занятых
            int capAll = TotalAll;
            if (_usedWorkers + _usedCitizens > capAll)
                _usedWorkers = Mathf.Max(0, capAll - _usedCitizens);
            _housesL1 = Mathf.Max(0, _housesL1 - 1);
        }
        EmitSignal(SignalName.WorkersChanged);
    }

    /// <summary>Вызывается HouseLevelComponent при апгрейде L1 → L2.</summary>
    public void UpgradeHouseToL2()
    {
        _totalWorkers  = Mathf.Max(0, _totalWorkers - 5);
        _totalCitizens += 5;
        _housesL1      = Mathf.Max(0, _housesL1 - 1);
        _housesL2++;
        EmitSignal(SignalName.WorkersChanged);
    }

    // ─── Резервирование ───────────────────────────────────────────────────────

    /// <summary>
    /// Зарезервировать n рабочих (не граждан) для производственного здания.
    /// Возвращает true если удалось.
    /// </summary>
    public bool TryClaimWorkers(int n)
    {
        if (n <= 0) return true;
        if (AvailableWorkers < n) return false;
        _usedWorkers += n;
        EmitSignal(SignalName.WorkersChanged);
        return true;
    }

    /// <summary>
    /// Зарезервировать n граждан (для плавильни).
    /// </summary>
    public bool TryClaimCitizens(int n)
    {
        if (n <= 0) return true;
        if (AvailableCitizens < n) return false;
        _usedCitizens += n;
        EmitSignal(SignalName.WorkersChanged);
        return true;
    }

    public void ReleaseWorkers(int n)
    {
        if (n <= 0) return;
        _usedWorkers = Mathf.Max(0, _usedWorkers - n);
        EmitSignal(SignalName.WorkersChanged);
    }

    public void ReleaseCitizens(int n)
    {
        if (n <= 0) return;
        _usedCitizens = Mathf.Max(0, _usedCitizens - n);
        EmitSignal(SignalName.WorkersChanged);
    }
}
