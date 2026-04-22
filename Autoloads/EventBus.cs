using Godot;

/// <summary>
/// Глобальная шина событий. Все системы общаются через неё.
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }

    public override void _Ready() => Instance = this;

    // ─── Карта / Ввод ────────────────────────────────────────────────────────
    [Signal] public delegate void TileClickedEventHandler(Vector2I tileCoord, Vector2 worldPos);

    // ─── Строительство ───────────────────────────────────────────────────────
    [Signal] public delegate void PlacementModeEnteredEventHandler(BuildingData buildingData);
    [Signal] public delegate void PlacementModeExitedEventHandler();
    [Signal] public delegate void BuildingPlacedEventHandler(Node building);
    [Signal] public delegate void BuildingDemolishedEventHandler(Node building);
    [Signal] public delegate void BuildingClickedEventHandler(Node building);

    // ─── Снос ────────────────────────────────────────────────────────────────
    [Signal] public delegate void DemolishModeEnteredEventHandler();
    [Signal] public delegate void DemolishModeExitedEventHandler();

    // ─── Дороги ──────────────────────────────────────────────────────────────
    [Signal] public delegate void RoadModeEnteredEventHandler();
    [Signal] public delegate void RoadModeExitedEventHandler();
    [Signal] public delegate void RoadPlacedEventHandler(Vector2I tile);

    // ─── Каналы ──────────────────────────────────────────────────────────────
    [Signal] public delegate void CanalModeEnteredEventHandler();
    [Signal] public delegate void CanalModeExitedEventHandler();
    [Signal] public delegate void CanalPlacedEventHandler(Vector2I tile);

    // ─── Экономика ───────────────────────────────────────────────────────────
    [Signal] public delegate void StockpileChangedEventHandler();
    [Signal] public delegate void ResourceConsumedEventHandler(string resourceId, int amount);

    // ─── Дома ────────────────────────────────────────────────────────────────

    // ─── Рабочие ─────────────────────────────────────────────────────────────
    [Signal] public delegate void WorkerAssignModeEnteredEventHandler(Node productionBuilding);
    [Signal] public delegate void WorkerAssignModeExitedEventHandler();

    // ─── Уведомления ─────────────────────────────────────────────────────────
    [Signal] public delegate void AlertRaisedEventHandler(string message);

    // ─── Скорость игры ───────────────────────────────────────────────────────
    [Signal] public delegate void GamePausedEventHandler();
    [Signal] public delegate void GameResumedEventHandler();
    [Signal] public delegate void GameSpeedChangedEventHandler(float speed);
}
