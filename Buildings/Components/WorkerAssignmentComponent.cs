using Godot;

/// <summary>
/// Компонент производственного здания для управления назначенными рабочими.
/// Каждое производство требует ровно 1 дом (5 жителей).
/// Без назначенного дома производство стоит.
/// </summary>
public partial class WorkerAssignmentComponent : Node
{
    public const int ResidentsPerHouse = 5;

    /// <summary>Назначенный дом (null = нет рабочих).</summary>
    public Building AssignedHouse { get; private set; }

    /// <summary>Есть ли рабочие?</summary>
    public bool HasWorkers => AssignedHouse != null && IsInstanceValid(AssignedHouse);

    public override void _Ready()
    {
        EventBus.Instance.BuildingDemolished += OnBuildingDemolished;
    }

    private void OnBuildingDemolished(Node building)
    {
        if (AssignedHouse == building)
        {
            AssignedHouse = null;
            GD.Print($"[Workers] {GetParent()?.Name}: дом снесён, рабочие ушли.");
        }
    }

    // Вызывается из World при назначении — не вызывает обратных методов.
    public void SetAssignedHouse(Building house) => AssignedHouse = house;
    public void ClearAssignedHouse()             => AssignedHouse = null;
}
