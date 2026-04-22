using Godot;
using System.Collections.Generic;

/// <summary>
/// Инструмент прокладки ирригационных каналов.
/// Режим: ЛКМ на тайл A → ЛКМ на тайл B → A* прокладывает путь.
/// ПКМ / Escape — отмена.
///
/// Визуал канала: синий центр + коричневая рамка (берега/бортики).
/// Эффект: через 60 секунд после укладки Sand вокруг → Grass (радиус 4 тайла).
/// Стоимость: 100 Драхм за тайл (как дорога).
/// Активируется из BuildMenu кнопкой «Канал».
/// </summary>
public partial class CanalTool : Node2D
{
    private IsoTileMap _tileMap;
    private bool       _active;
    private bool       _waitingForEnd;
    private Vector2I   _startTile;

    // Предпросмотр — список тайлов текущего пути
    private readonly List<Vector2I> _previewPath = new();

    // Цвета превью
    private static readonly Color PreviewBlue    = new(0.16f, 0.44f, 0.82f, 0.75f);
    private static readonly Color PreviewRed     = new(0.9f,  0.2f,  0.2f,  0.75f);
    private static readonly Color PreviewBorder  = new(0.48f, 0.29f, 0.12f, 0.90f);

    public void Initialize(IsoTileMap tileMap)
    {
        _tileMap = tileMap;
        SetProcess(false);
        EventBus.Instance.CanalModeEntered += OnEnter;
        EventBus.Instance.CanalModeExited  += OnExit;
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
        var path      = ComputeCanalPath(_startTile, hoverTile);

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
            // Выбрали начало — только Grass, Sand, Canal (не Water/Forest/Rock)
            var t = _tileMap.GetTileType(tile);
            bool passable = t == TileType.Grass || t == TileType.Sand
                         || t == TileType.Road  || t == TileType.Canal;
            if (!passable) return;

            _startTile     = tile;
            _waitingForEnd = true;
            _previewPath.Clear();
            GD.Print($"[CanalTool] Начало: {tile}");
        }
        else
        {
            // Выбрали конец — строим
            var path = ComputeCanalPath(_startTile, tile);
            if (path.Count == 0)
            {
                GD.PrintErr("[CanalTool] Путь не найден.");
                return;
            }

            // Новые тайлы (не canal) — считаем стоимость
            var newTiles = new List<Vector2I>();
            foreach (var t in path)
                if (_tileMap.GetTileType(t) != TileType.Canal)
                    newTiles.Add(t);

            if (!ResourceManager.Instance.CanAffordRoad(newTiles.Count))
            {
                int cost = newTiles.Count * ResourceManager.RoadCostPerTile;
                EventBus.Instance.EmitSignal(EventBus.SignalName.AlertRaised,
                    $"Не хватает Драхм для канала! Нужно {cost}Д");
                return;
            }

            ResourceManager.Instance.SpendRoad(newTiles.Count);

            foreach (var t in path)
            {
                _tileMap.PlaceCanal(t);
                EventBus.Instance.EmitSignal(EventBus.SignalName.CanalPlaced, t);
            }

            GD.Print($"[CanalTool] Канал: {path.Count} тайлов, {newTiles.Count} новых.");

            // Сброс для следующего сегмента
            _startTile     = tile;
            _previewPath.Clear();
            QueueRedraw();
        }
    }

    private void Cancel()
    {
        EventBus.Instance.EmitSignal(EventBus.SignalName.CanalModeExited);
    }

    // ─── A* по Grass/Sand/Road/Canal тайлам ───────────────────────────────────

    private List<Vector2I> ComputeCanalPath(Vector2I from, Vector2I to)
    {
        if (from == to) return new List<Vector2I> { from };

        var open       = new SortedSet<(int f, int idx)>();
        var gCost      = new Dictionary<Vector2I, int>();
        var parent     = new Dictionary<Vector2I, Vector2I>();
        var closed     = new HashSet<Vector2I>();
        int idx        = 0;

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
                bool passable = nbType == TileType.Grass || nbType == TileType.Sand
                             || nbType == TileType.Road  || nbType == TileType.Canal;
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

    // ─── Предпросмотр ─────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (!_active || _previewPath.Count == 0) return;

        int newCount = 0;
        foreach (var tile in _previewPath)
            if (_tileMap.GetTileType(tile) != TileType.Canal)
                newCount++;

        bool canAfford = ResourceManager.Instance.CanAffordRoad(newCount);

        foreach (var tile in _previewPath)
        {
            var center = _tileMap.GetTileWorldCenter(tile) - GlobalPosition;
            var fill   = canAfford ? PreviewBlue : PreviewRed;
            DrawIsoCanalPreview(center, fill);
        }
    }

    /// <summary>Рисует ромб с синим центром и коричневой рамкой (имитация канала).</summary>
    private void DrawIsoCanalPreview(Vector2 center, Color fill)
    {
        // Внешний ромб (коричневая рамка — чуть больше)
        float hw = 56f, hh = 28f;
        var outer = new Vector2[]
        {
            center + new Vector2(0,   -hh),
            center + new Vector2(hw,    0),
            center + new Vector2(0,    hh),
            center + new Vector2(-hw,   0),
        };
        DrawPolygon(outer, new Color[] { PreviewBorder });

        // Внутренний ромб (синий/красный — чуть меньше)
        float ihw = 46f, ihh = 23f;
        var inner = new Vector2[]
        {
            center + new Vector2(0,   -ihh),
            center + new Vector2(ihw,    0),
            center + new Vector2(0,    ihh),
            center + new Vector2(-ihw,   0),
        };
        DrawPolygon(inner, new Color[] { fill });
    }
}
