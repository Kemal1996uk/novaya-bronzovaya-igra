using Godot;

/// <summary>
/// Глобальный менеджер состояния игры.
/// Управляет скоростью времени (1×, 2×, 3×) и паузой.
///
/// Доступ из любого места: GameManager.Instance.SetSpeed(2f);
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; }

    /// <summary>Текущий множитель скорости (1.0 = нормально, 2.0 = ускорено).</summary>
    public float GameSpeed { get; private set; } = 1.0f;

    /// <summary>Игра на паузе?</summary>
    public bool IsPaused { get; private set; } = false;

    // ─── Карта ────────────────────────────────────────────────────────────────

    /// <summary>Путь к пользовательской карте (null = процедурная генерация).</summary>
    public string MapFilePath { get; set; } = null;

    /// <summary>Запустить в режиме редактора карт?</summary>
    public bool EditorMapMode { get; set; } = false;

    /// <summary>Ширина карты в редакторе.</summary>
    public int EditorMapWidth { get; set; } = 80;

    /// <summary>Высота карты в редакторе.</summary>
    public int EditorMapHeight { get; set; } = 80;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>Поставить игру на паузу.</summary>
    public void PauseGame()
    {
        if (IsPaused) return;
        IsPaused = true;
        Engine.TimeScale = 0.0f;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GamePaused);
        GD.Print("[GameManager] Пауза");
    }

    /// <summary>Снять паузу.</summary>
    public void ResumeGame()
    {
        if (!IsPaused) return;
        IsPaused = false;
        Engine.TimeScale = GameSpeed;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameResumed);
        GD.Print("[GameManager] Возобновлено");
    }

    /// <summary>Переключить паузу.</summary>
    public void TogglePause()
    {
        if (IsPaused) ResumeGame();
        else PauseGame();
    }

    /// <summary>Установить множитель скорости (диапазон 0.5 – 4.0).</summary>
    public void SetSpeed(float speed)
    {
        GameSpeed = Mathf.Clamp(speed, 0.5f, 4.0f);
        if (!IsPaused)
            Engine.TimeScale = GameSpeed;
        EventBus.Instance.EmitSignal(EventBus.SignalName.GameSpeedChanged, GameSpeed);
        GD.Print($"[GameManager] Скорость: {GameSpeed}×");
    }
}
