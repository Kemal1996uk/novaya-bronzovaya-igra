using Godot;
using System.Collections.Generic;

/// <summary>
/// Инструмент прокладки дорог.
/// Режим: ЛКМ на тайл A → ЛКМ на тайл B → A* прокладывает путь.
/// ПКМ / Escape — отмена.
///
/// Стоимость: 100 Драхм за тайл (ResourceManager.RoadCostPerTile).
/// Активируется из BuildMenu кнопкой «Дорога».
/// </summary>
public partial class RoadTool : Node2D
{
    private IsoTileMap _tileMap;
    private bool       _active;
    private bool       _waitingForEnd;
    private Vector2I   _startTile;

    // Предпросмотр — список тайлов текущего пути
    private readonly List<Vector2I> _previewPath = new();

    public void Initialize(IsoTileMap tileMap)
    {
        _tileMap = tileMap;
        SetProcess(false);
        EventBus.Instance.RoadModeEntered += OnEnter;
        EventBus.Instance.RoadModeExited  += OnExit;
    }

    private void OnEnter()
    {
        _active        = true;
        _waitingForEnd = false;
        _previewPath.Clear();
        SetProcess(true);
        QueueRedraw();
    }

    private void OnExit()
    {
        _active        = false;
        _waitingForEnd = false;
        _previewPath.Clear();
        SetProcess(false);
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!_active) return;

        if (@event is InputEventMouseButton btn && btn.Pressed)
        {
            if (btn.ButtonIndex == MouseButton.Left)
                HandleLeftClick();
            else if (btn.ButtonIndex == MouseButton.Right)
                Cancel();
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == Key.Escape)
            Cancel();
    }

    public override void _Process(double delta)
    {
        if (!_active || !_waitingForEnd) return;

        var hoverTile = _tileMap.GlobalToTile(GetGlobalMousePosition());
        var path      = ComputeRoadPath(_startTile, hoverTile);

        if (PathChanged(path))
        {
            _previewPath.Clear();
            _previewPath.AddRange(path);
            QueueRedraw();
        }
    }

    // ─── Клики ────────────────────────────────────────────────────────────────

    private void HandleLeftClick()
    {
        var tile = _tileMap.GlobalToTile(GetGlobalMousePosition());
        if (!_tileMap.IsValidTile(tile)) return;

        if (!_waitingForEnd)
        {
            // Выбрали начало
            if (!_tileMap.IsRoadable(tile)) return;
            _startTile     = tile;
            _waitingForEnd = true;
            _previewPath.Clear();
            GD.Print($"[RoadTool] Начало: {tile}");
        }
        else
        {
            // Выбрали конец — строим
            var path = ComputeRoadPath(_startTile, tile);
            if (path.Count == 0)
            {
                GD.PrintErr("[RoadTool] Путь не найден.");
                return;
            }

            // Считаем новые тайлы (исключаем уже дороги)
            var newTiles = new List<Vector2I>();
            foreach (var t in path)
                if (_tileMap.GetTileType(t) != TileType.Road)
                    newTiles.Add(t);

            if (!ResourceManager.Instance.CanAffordRoad(newTiles.Count))
            {
                int cost = newTiles.Count * ResourceManager.RoadCostPerTile;
                GD.PrintErr($"[RoadTool] Не хватает Драхм. Нужно: {cost}Д");
                EventBus.Instance.EmitSignal(EventBus.SignalName.AlertRaised,
                    $"Не хватает Драхм! Нужно {cost}Д");
                return;
            }

            ResourceManager.Instance.SpendRoad(newTiles.Count);

            foreach (var t in path)
            {
                _tileMap.PlaceRoad(t);
                EventBus.Instance.EmitSignal(EventBus.SignalName.RoadPlaced, t);
            }

            GD.Print($"[RoadTool] Дорога: {path.Count} тайлов, {newTiles.Count} новых.");

            // После прокладки — сброс для нового сегмента
            _startTile     = tile;
            _previewPath.Clear();
            QueueRedraw();
        }
    }

    private void Cancel()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.RoadModeExited);
    }

    // ─── A* по Grass/Sand тайлам ──────────────────────────────────────────────

    /// <summary>A* только по Road тайлам — для сноса существующих дорог.</summary>
    public List<Vector2I> GetDemolishPath(Vector2I from, Vector2I to)
        => ComputeRoadPath(from, to, roadOnly: true);

    private List<Vector2I> ComputeRoadPath(Vector2I from, Vector2I to, bool roadOnly = false)
    {
        if (from == to) return new List<Vector2I> { from };

        // Простой A* по 4 направлениям прямо здесь (не через NavigationManager — нам нужна только Grass/Sand)
        var open   = new SortedSet<(int f, int idx)>();
        var gCost  = new Dictionary<Vector2I, int>();
        var parent = new Dictionary<Vector2I, Vector2I>();
        var closed = new HashSet<Vector2I>();
        int idx    = 0;

        gCost[from] = 0;
        open.Add((Heuristic(from, to), idx++));
        var idxToTile = new Dictionary<int, Vector2I> { [0] = from };

        var dirs = new Vector2I[] { new(1,0), new(-1,0), new(0,1), new(0,-1) };

        while (open.Count > 0)
        {
            var (_, curIdx) = open.Min;
            open.Remove(open.Min);
            var cur = idxToTile[curIdx];

            if (cur == to)
                return ReconstructPath(parent, from, to);

            if (closed.Contains(cur)) continue;
            closed.Add(cur);

            foreach (var d in dirs)
            {
                var nb = cur + d;
                if (!_tileMap.IsValidTile(nb)) continue;

                var nbType = _tileMap.GetTileType(nb);
                // roadOnly=true: только Road тайлы (для сноса); иначе Grass/Sand/Road
                bool passable = roadOnly
                    ? nbType == TileType.Road
                    : (nbType == TileType.Grass || nbType == TileType.Sand || nbType == TileType.Road);
                if (!passable) continue;

                if (closed.Contains(nb)) continue;

                int ng = gCost[cur] + 1;
                if (!gCost.TryGetValue(nb, out int og) || ng < og)
                {
                    gCost[nb]  = ng;
                    parent[nb] = cur;
                    int f = ng + Heuristic(nb, to);
                    int i = idx++;
                    idxToTile[i] = nb;
                    open.Add((f, i));
                }
            }
        }
        return new List<Vector2I>();
    }

    private static int Heuristic(Vector2I a, Vector2I b)
        => Mathf.Abs(a.X - b.X) + Mathf.Abs(a.Y - b.Y);

    private static List<Vector2I> ReconstructPath(Dictionary<Vector2I, Vector2I> parent,
                                                   Vector2I from, Vector2I to)
    {
        var path = new List<Vector2I>();
        var cur  = to;
        while (cur != from)
        {
            path.Add(cur);
            cur = parent[cur];
        }
        path.Add(from);
        path.Reverse();
        return path;
    }

    private bool PathChanged(List<Vector2I> newPath)
    {
        if (newPath.Count != _previewPath.Count) return true;
        for (int i = 0; i < newPath.Count; i++)
            if (newPath[i] != _previewPath[i]) return true;
        return false;
    }

    // ─── Предпросмотр (рисуем ромбы по пути) ────────────────────────────────

    public override void _Draw()
    {
        if (!_active || _previewPath.Count == 0) return;

        int newCount  = 0;
        int cost      = 0;
        foreach (var tile in _previewPath)
            if (_tileMap.GetTileType(tile) != TileType.Road)
            { newCount++; cost += ResourceManager.RoadCostPerTile; }

        bool canAfford = ResourceManager.Instance.CanAffordRoad(newCount);

        foreach (var tile in _previewPath)
        {
            var center = _tileMap.GetTileWorldCenter(tile) - GlobalPosition;
            var color  = canAfford
                ? new Color(0.8f, 0.65f, 0.25f, 0.7f)
                : new Color(0.9f, 0.2f, 0.2f, 0.7f);
            DrawIsoDiamond(center, color);
        }
    }

    private void DrawIsoDiamond(Vector2 center, Color color)
    {
        float hw = 56f, hh = 28f;
        var pts = new Vector2[]
        {
            center + new Vector2(0, -hh),
            center + new Vector2(hw, 0),
            center + new Vector2(0,  hh),
            center + new Vector2(-hw, 0),
        };
        DrawPolygon(pts, new Color[] { color });
    }
}
