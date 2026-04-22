using Godot;

/// <summary>
/// Компонент Казармы: автоматически тренирует солдат.
/// Прикрепляется к зданию (Building) в BuildingRegistry.PlaceBuilding.
///
/// Логика: каждые TrainingTimeSec — если хватает золота, списываем и
/// спавним Soldier рядом с казармой.
/// Прогресс-бар показывается в BuildingInfoPanel через TrainingProgress.
/// </summary>
public partial class TrainingQueue : Node
{
    public const float TrainingTimeSec = 60f;  // секунд на одного солдата
    public const int   GoldPerSoldier  = 150;  // цена тренировки

    private float  _progress;
    private Node2D _unitsContainer;

    /// <summary>Прогресс текущей тренировки [0..1].</summary>
    public float TrainingProgress => _progress / TrainingTimeSec;

    public void Initialize(Node2D unitsContainer)
    {
        _unitsContainer = unitsContainer;
    }

    public override void _Process(double delta)
    {
        // Тренируем только если хватает золота
        if (!ResourceManager.Instance.CanAfford(GoldPerSoldier, 0, 0))
        {
            _progress = 0f; // сброс если нет денег (стимул копить)
            return;
        }

        _progress += (float)delta;
        if (_progress >= TrainingTimeSec)
        {
            _progress = 0f;
            TrainSoldier();
        }
    }

    private void TrainSoldier()
    {
        if (!ResourceManager.Instance.SpendBuildCost(GoldPerSoldier, 0, 0)) return;

        var parent   = GetParent() as Node2D;
        var spawnPos = (parent?.GlobalPosition ?? Vector2.Zero) + new Vector2(36f, 44f);

        var soldier = new Soldier { Name = $"Soldier_{Time.GetTicksMsec()}" };

        if (_unitsContainer != null && IsInstanceValid(_unitsContainer))
            _unitsContainer.AddChild(soldier);
        else
            GetTree().Root.GetNode("World").AddChild(soldier);

        soldier.GlobalPosition = spawnPos;

        GD.Print($"[TrainingQueue] ⚔️ Солдат обучен! Позиция: {spawnPos}");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.AlertRaised, "⚔️ Солдат готов к бою!");
    }
}
