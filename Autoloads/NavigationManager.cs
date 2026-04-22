using Godot;

using System.Collections.Generic;

/// <summary>
/// Глобальный менеджер навигации. Использует AStarGrid2D на тайловой сетке.
///
/// Тележки ходят ТОЛЬКО по Road тайлам.
/// При прокладке дороги (RoadPlaced) → тайл становится проходимым.
/// Путь к зданию строится через ближайший смежный Road тайл (FindBuildingPath).
/// </summary>
public partial class NavigationManager : Node
{
    public static NavigationManager Instance { get; private set; }

    private AStarGrid2D _grid;
    private IsoTileMap  _tileMap;

    public override void _Ready()
    {
        Instance = this;
    }

    // ─── Инициализация ────────────────────────────────────────────────────────

    public void Initialize(IsoTileMap tileMap)
    {
        _tileMap = tileMap;

        _grid = new AStarGrid2D();
        _grid.Region       = new Rect2I(Vector2I.Zero, tileMap.MapSize);
        _grid.CellSize     = Vector2.One;
        _grid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.Never;
        _grid.Update();

        // Всё непроходимо — только Road тайлы открывают путь
        for (int x = 0; x < tileMap.MapSize.X; x++)
        for (int y = 0; y < tileMap.MapSize.Y; y++)
            _grid.SetPointSolid(new Vector2I(x, y), true);

        EventBus.Instance.RoadPlaced += tile => SetTilePassable(tile, true);

        GD.Print($"[NavigationManager] Сетка {tileMap.MapSize.X}×{tileMap.MapSize.Y} готова (Road-only режим).");
    }

    // ─── Поиск пути между зданиями ───────────────────────────────────────────

    /// <summary>
    /// Строит путь от здания <paramref name="from"/> к зданию <paramref name="to"/>
    /// только по Road тайлам. Возвращает пустой массив если связи нет.
    /// </summary>
    public Vector2[] FindBuildingPath(Building from, Building to)
    {
        if (_grid == null || _tileMap == null) return System.Array.Empty<Vector2>();

        var fromRoad = FindBuildingRoadEntry(from);
        var toRoad   = FindBuildingRoadEntry(to);

        if (!fromRoad.HasValue || !toRoad.HasValue) return System.Array.Empty<Vector2>();

        var result = new List<Vector2>();
        // Начало — центр исходного здания
        result.Add(_tileMap.GetTileWorldCenter(from.GridPosition));

        if (fromRoad.Value == toRoad.Value)
        {
            // Оба здания выходят на один тайл дороги
            result.Add(_tileMap.GetTileWorldCenter(fromRoad.Value));
        }
        else
        {
            var idPath = _grid.GetIdPath(fromRoad.Value, toRoad.Value);
            if (idPath == null || idPath.Count == 0) return System.Array.Empty<Vector2>();
            foreach (var t in idPath)
                result.Add(_tileMap.GetTileWorldCenter(t));
        }

        // Конец — центр целевого здания
        result.Add(_tileMap.GetTileWorldCenter(to.GridPosition));
        return result.ToArray();
    }

    /// <summary>
    /// Ищет ближайший Road тайл, смежный с любым тайлом footprint здания.
    /// Возвращает null если таких тайлов нет.
    /// </summary>
    private Vector2I? FindBuildingRoadEntry(Building b)
    {
        if (b?.Data == null) return null;
        var fp  = b.Data.FootprintSize;
        var gp  = b.GridPosition;
        var dirs = new[] { new Vector2I(1,0), new Vector2I(-1,0),
                           new Vector2I(0,1), new Vector2I(0,-1) };

        // Перебираем все тайлы footprint и их соседей
        for (int dx = 0; dx < fp.X; dx++)
        for (int dy = 0; dy < fp.Y; dy++)
        {
            var tile = gp + new Vector2I(dx, dy);
            foreach (var d in dirs)
            {
                var nb = tile + d;
                if (_tileMap.IsValidTile(nb) && _tileMap.GetTileType(nb) == TileType.Road)
                    return nb;
            }
        }
        return null;
    }

    // ─── Путь для людей (preferRoad) ─────────────────────────────────────────

    /// <summary>
    /// Строит путь для человека: если рядом есть дороги — идёт по ним,
    /// иначе возвращает прямой путь до цели.
    /// SearchRadius — сколько тайлов искать ближайшую дорогу от точки.
    /// </summary>
    public Vector2[] FindHumanPath(Vector2 fromWorld, Vector2 toWorld, int searchRadius = 8)
    {
        if (_grid == null || _tileMap == null) return new[] { toWorld };

        var fromRoad = FindNearestRoadTile(_tileMap.GlobalToTile(fromWorld), searchRadius);
        var toRoad   = FindNearestRoadTile(_tileMap.GlobalToTile(toWorld),   searchRadius);

        if (!fromRoad.HasValue || !toRoad.HasValue)
            return new[] { toWorld };   // нет дорог поблизости — прямо

        if (fromRoad.Value == toRoad.Value)
        {
            return new[] { _tileMap.GetTileWorldCenter(fromRoad.Value), toWorld };
        }

        var idPath = _grid.GetIdPath(fromRoad.Value, toRoad.Value);
        if (idPath == null || idPath.Count == 0)
            return new[] { toWorld };   // дороги не связаны — прямо

        var result = new System.Collections.Generic.List<Vector2>();
        foreach (var t in idPath)
            result.Add(_tileMap.GetTileWorldCenter(t));
        result.Add(toWorld);
        return result.ToArray();
    }

    private Vector2I? FindNearestRoadTile(Vector2I origin, int radius)
    {
        if (_tileMap.IsValidTile(origin) && _tileMap.GetTileType(origin) == TileType.Road)
            return origin;

        Vector2I? best     = null;
        int       bestDist = int.MaxValue;
        for (int dx = -radius; dx <= radius; dx++)
        for (int dy = -radius; dy <= radius; dy++)
        {
            var nb   = origin + new Vector2I(dx, dy);
            int dist = dx * dx + dy * dy;
            if (dist < bestDist && _tileMap.IsValidTile(nb) &&
                _tileMap.GetTileType(nb) == TileType.Road)
            {
                bestDist = dist;
                best     = nb;
            }
        }
        return best;
    }

    // ─── Управление проходимостью ─────────────────────────────────────────────

    public void SetTilePassable(Vector2I tile, bool passable)
    {
        if (_grid == null || !IsInGrid(tile)) return;
        _grid.SetPointSolid(tile, !passable);
    }

    // ─── Вспомогательные ─────────────────────────────────────────────────────

    private bool IsInGrid(Vector2I tile)
        => _tileMap != null && _tileMap.IsValidTile(tile);
}
