using Godot;

/// <summary>
/// Управляет волнами бандитов.
/// Добавляется как дочерний узел World в World._Ready().
///
/// Расписание:
///   Волна 1 — через 2 минуты (FirstWaveDelay)
///   Волны 2+ — каждые 3 минуты (WaveInterval)
///   Размер волны: 2 + номер_волны (3, 4, 5...)
/// </summary>
public partial class CombatManager : Node
{
    private IsoTileMap _tileMap;
    private float      _waveTimer;
    private int        _waveNumber;

    private const float FirstWaveDelay = 120f; // секунд до первой волны
    private const float WaveInterval   = 180f; // секунд между волнами

    private readonly RandomNumberGenerator _rng = new();

    public void Initialize(IsoTileMap tileMap)
    {
        _tileMap   = tileMap;
        _waveTimer = FirstWaveDelay;
        _rng.Randomize();
        GD.Print($"[CombatManager] Инициализирован. Первая атака через {FirstWaveDelay}с.");
    }

    public override void _Process(double delta)
    {
        _waveTimer -= (float)delta;
        if (_waveTimer <= 0f)
        {
            SpawnWave();
            _waveTimer = WaveInterval;
        }
    }

    private void SpawnWave()
    {
        _waveNumber++;
        int count = 2 + _waveNumber; // волна 1 → 3 бандита, волна 2 → 4 и т.д.

        GD.Print($"[CombatManager] ⚔️  Волна {_waveNumber}! Бандитов: {count}");
        EventBus.Instance.EmitSignal(EventBus.SignalName.CityUnderAttack);

        var world = GetParent();
        for (int i = 0; i < count; i++)
        {
            var bandit = new Bandit { Name = $"Bandit_W{_waveNumber}_{i}" };
            world.AddChild(bandit);
            bandit.GlobalPosition = GetSpawnPoint();
        }
    }

    /// <summary>
    /// Выбирает случайную точку у края карты на суше (Grass или Sand).
    /// Перебирает случайный край → рандомная координата → ищет ближайший
    /// проходимый тайл в спирали радиуса 15.
    /// </summary>
    private Vector2 GetSpawnPoint()
    {
        if (_tileMap == null) return Vector2.Zero;

        int size  = _tileMap.MapSize.X;
        int edge  = _rng.RandiRange(0, 3);
        int coord = _rng.RandiRange(10, size - 10);

        Vector2I tile = edge switch
        {
            0 => new Vector2I(coord,       4),
            1 => new Vector2I(size - 4,    coord),
            2 => new Vector2I(coord,       size - 4),
            _ => new Vector2I(4,           coord),
        };

        // Ищем ближайший суши-тайл
        for (int r = 0; r <= 15; r++)
        {
            for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
            {
                if (Mathf.Abs(dx) != r && Mathf.Abs(dy) != r) continue; // только контур
                var t = tile + new Vector2I(dx, dy);
                if (!_tileMap.IsValidTile(t)) continue;
                var type = _tileMap.GetTileType(t);
                if (type == TileType.Grass || type == TileType.Sand)
                    return _tileMap.GetTileWorldCenter(t);
            }
        }

        return _tileMap.GetTileWorldCenter(tile);
    }
}
