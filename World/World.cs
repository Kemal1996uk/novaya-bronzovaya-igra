using Godot;

/// <summary>
/// Корневой узел мира. Инициализирует все системы.
/// </summary>
public partial class World : Node2D
{
    private IsoTileMap          _tileMap;
    private WorldCamera         _camera;
    private BuildPlacementGhost _ghost;
    private RoadTool            _roadTool;
    private CanalTool           _canalTool;

    private bool _inPlacementMode;
    private bool _inRoadMode;
    private bool _inDemolishMode;
    private bool _inCanalMode;

    // Снос дороги A→B
    private bool     _demolishRoadWaitingEnd;
    private Vector2I _demolishRoadStart;

    public override void _Ready()
    {
        _tileMap = GetNode<IsoTileMap>("TileMap_Ground");
        _camera  = GetNode<WorldCamera>("WorldCamera");
        _ghost   = GetNode<BuildPlacementGhost>("BuildPlacementGhost");

        var buildingsNode = GetNode<Node2D>("Buildings");
        buildingsNode.ZIndex = 1;
        _ghost.ZIndex        = 3;

        _ghost.SetTileMap(_tileMap);
        BuildingRegistry.Instance.Initialize(buildingsNode, _tileMap);
        NavigationManager.Instance.Initialize(_tileMap);

        var cartsNode = new Node2D { Name = "Carts", ZIndex = 3 };
        AddChild(cartsNode);

        _unitsNode = new Node2D { Name = "Units", ZIndex = 3 };
        AddChild(_unitsNode);

        _roadTool = new RoadTool { Name = "RoadTool", ZIndex = 4 };
        AddChild(_roadTool);
        _roadTool.Initialize(_tileMap);

        _canalTool = new CanalTool { Name = "CanalTool", ZIndex = 4 };
        AddChild(_canalTool);
        _canalTool.Initialize(_tileMap);

        var resBar    = new ResourceBar       { Name = "ResourceBar" };
        var infoPanel = new BuildingInfoPanel { Name = "InfoPanel"   };
        AddChild(resBar);
        AddChild(infoPanel);

        // ── Подписки ──────────────────────────────────────────────────────────
        EventBus.Instance.PlacementModeEntered += _ =>
        {
            _inPlacementMode = true; _inRoadMode = false;
            _inDemolishMode = false; _inCanalMode = false; _demolishRoadWaitingEnd = false;
        };
        EventBus.Instance.PlacementModeExited  += () => _inPlacementMode = false;
        EventBus.Instance.RoadModeEntered      += () =>
        {
            _inRoadMode = true; _inPlacementMode = false;
            _inDemolishMode = false; _inCanalMode = false; _demolishRoadWaitingEnd = false;
        };
        EventBus.Instance.RoadModeExited       += () => _inRoadMode = false;
        EventBus.Instance.DemolishModeEntered  += () =>
        {
            _inDemolishMode = true; _inPlacementMode = false;
            _inRoadMode = false; _inCanalMode = false;
        };
        EventBus.Instance.DemolishModeExited   += () =>
        {
            _inDemolishMode = false; _demolishRoadWaitingEnd = false;
        };
        EventBus.Instance.CanalModeEntered     += () =>
        {
            _inCanalMode = true; _inPlacementMode = false;
            _inRoadMode = false; _inDemolishMode = false; _demolishRoadWaitingEnd = false;
        };
        EventBus.Instance.CanalModeExited      += () => _inCanalMode = false;
        EventBus.Instance.CanalPlaced          += OnCanalPlaced;

        EventBus.Instance.BuildingPlaced += OnBuildingPlaced;
        EventBus.Instance.TileClicked   += OnTileClicked;

        CenterCameraOnMap();

        GD.Print("[World] Остров готов. Стройте Лесопилку рядом с лесом!");
        GD.Print($"       🪙{ResourceManager.Instance.Get("gold")} " +
                 $"🪵{ResourceManager.Instance.Get("wood")} " +
                 $"🪨{ResourceManager.Instance.Get("stone")}");
    }

    private Node2D _cartsNode;
    private Node2D _unitsNode;

    private void OnBuildingPlaced(Node node)
    {
        if (node is not Building b) return;

        // Склад → создаём тележки
        var wh = b.GetNodeOrNull<WarehouseComponent>("WarehouseComponent");
        if (wh != null)
        {
            _cartsNode = GetNodeOrNull<Node2D>("Carts");
            wh.Setup(_tileMap, _cartsNode);
        }

        // Дом → спавним одного управляемого персонажа рядом со входом
        if (b.Data?.BuildingId == "house")
        {
            var unit = new HumanUnit { Name = $"Human_{b.GridPosition.X}_{b.GridPosition.Y}" };
            _unitsNode.AddChild(unit);
            // Ставим чуть ниже переднего угла здания
            unit.GlobalPosition = b.GlobalPosition + new Vector2(0f, 24f);
        }
    }

    private async void OnCanalPlaced(Vector2I tile)
    {
        // Через 60 секунд — конвертируем Sand→Grass вокруг тайла канала
        await ToSignal(GetTree().CreateTimer(60.0), SceneTreeTimer.SignalName.Timeout);
        if (IsInsideTree() && _tileMap != null)
            _tileMap.ConvertSandToGrassAround(tile, 4);
    }

    private void OnTileClicked(Vector2I tileCoord, Vector2 worldPos)
    {
        if (_inPlacementMode || _inRoadMode || _inCanalMode) return;

        Building hit = FindBuildingAt(tileCoord);

        if (_inDemolishMode)
        {
            if (hit != null)
            {
                // Снести здание
                BuildingRegistry.Instance.DemolishBuilding(hit);
                _demolishRoadWaitingEnd = false;
                return;
            }

            // Снос дороги (A→B)
            if (_tileMap.GetTileType(tileCoord) == TileType.Road)
            {
                if (!_demolishRoadWaitingEnd)
                {
                    _demolishRoadStart      = tileCoord;
                    _demolishRoadWaitingEnd = true;
                    GD.Print($"[World] Снос дороги: точка A = {tileCoord}. Выберите точку B.");
                }
                else
                {
                    var path = _roadTool.GetDemolishPath(_demolishRoadStart, tileCoord);
                    int removed = 0;
                    foreach (var t in path)
                    {
                        if (_tileMap.RemoveRoad(t))
                        {
                            NavigationManager.Instance.SetTilePassable(t, false);
                            // Вернуть 50% стоимости за каждый тайл
                            ResourceManager.Instance.Add("gold", ResourceManager.RoadCostPerTile / 2);
                            removed++;
                        }
                    }
                    GD.Print($"[World] Снесено дорог: {removed} тайлов, возврат {removed * ResourceManager.RoadCostPerTile / 2} Д.");
                    _demolishRoadWaitingEnd = false;
                }
            }
            return;
        }

        if (hit != null)
            EventBus.Instance.EmitSignal(EventBus.SignalName.BuildingClicked, hit);
    }

    private Building FindBuildingAt(Vector2I tileCoord)
    {
        foreach (var building in BuildingRegistry.Instance.AllBuildings)
        {
            if (building.Data == null) continue;
            var fp = building.Data.FootprintSize;
            var gp = building.GridPosition;
            if (tileCoord.X >= gp.X && tileCoord.X < gp.X + fp.X &&
                tileCoord.Y >= gp.Y && tileCoord.Y < gp.Y + fp.Y)
                return building;
        }
        return null;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (_inPlacementMode || _inRoadMode || _inCanalMode) return;

        if (@event is InputEventMouseButton btn
            && btn.Pressed && btn.ButtonIndex == MouseButton.Left)
        {
            var globalMousePos = GetGlobalMousePosition();
            var tileCoord      = _tileMap.GlobalToTile(globalMousePos);
            if (!_tileMap.IsValidTile(tileCoord)) return;

            var worldCenter = _tileMap.GetTileWorldCenter(tileCoord);
            EventBus.Instance.EmitSignal(EventBus.SignalName.TileClicked, tileCoord, worldCenter);
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            if (_inDemolishMode)
            {
                EventBus.Instance.EmitSignal(EventBus.SignalName.DemolishModeExited);
                GetViewport().SetInputAsHandled();
            }
        }
    }

    private void CenterCameraOnMap()
    {
        var center = new Vector2I(_tileMap.MapSize.X / 2, _tileMap.MapSize.Y / 2);
        _camera.Position = _tileMap.GetTileWorldCenter(center);
    }
}
