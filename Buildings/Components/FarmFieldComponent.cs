using Godot;

/// <summary>
/// Компонент Фермы. Считает поля (4×4) в радиусе 15 тайлов.
/// Производство (ProductionCycleComponent) активно только при FieldCount >= 4.
///
/// Поля добавляются через инфо-панель Фермы, не из BuildMenu.
/// </summary>
public partial class FarmFieldComponent : Node
{
    public const int RequiredFields = 4;
    public const int SearchRadius   = 15;

    public int  FieldCount { get; private set; }
    public bool IsReady    => FieldCount >= RequiredFields;

    private Building   _building;
    private ProductionCycleComponent _production;
    private float      _checkTimer;
    private const float CheckInterval = 3f;

    public override void _Ready()
    {
        _building   = GetParent() as Building;
        _production = GetParent().GetNodeOrNull<ProductionCycleComponent>("ProductionCycleComponent");
    }

    public override void _Process(double delta)
    {
        _checkTimer += (float)delta;
        if (_checkTimer < CheckInterval) return;
        _checkTimer = 0f;
        CountFields();
    }

    private void CountFields()
    {
        if (_building == null || BuildingRegistry.Instance == null) return;

        int count = 0;
        var farmPos = _building.GridPosition;

        foreach (var b in BuildingRegistry.Instance.AllBuildings)
        {
            if (b.Data?.BuildingId != "field") continue;

            var fieldPos = b.GridPosition;
            int dx = Mathf.Abs(fieldPos.X - farmPos.X);
            int dy = Mathf.Abs(fieldPos.Y - farmPos.Y);
            if (dx <= SearchRadius && dy <= SearchRadius)
                count++;
        }

        FieldCount = count;

        // Обновляем причину простоя производства
        if (_production != null && !IsReady)
            GD.Print($"[Farm] Нужно полей: {RequiredFields}, есть: {FieldCount}");
    }

    // Вызывается из BuildingInfoPanel — начать placement mode для поля
    public bool CanAddField() => FieldCount < RequiredFields;
}
