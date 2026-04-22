using Godot;
using System.Collections.Generic;

/// <summary>
/// Носильщик склада. Состояния:
///   Idle → MovingToSource → PickingUp → MovingToWarehouse → Delivering → Idle
///
/// Визуал: walk3_spritesheet.png — горизонтальная полоска 8192×128px.
/// 8 направлений × 16 кадров = 128 кадров, каждый 64×128px.
/// Анимация выбирается по вектору движения.
/// </summary>
public partial class CartAgent : Node2D
{
    public enum CartState { Idle, MovingToSource, PickingUp, MovingToWarehouse, Delivering }

    public const int MaxLoad = 13;

    public CartState State      { get; private set; } = CartState.Idle;
    public bool      IsAvailable => State == CartState.Idle;

    private Building           _warehouse;
    private WarehouseComponent _warehouseComp;
    private IsoTileMap         _tileMap;

    private Building _sourceBuilding;
    private string   _cargoResourceId;
    private int      _cargoAmount;

    // Навигация
    private Vector2[] _path;
    private int       _pathIndex;
    private const float Speed = 80f;

    // Визуал — 8-направленная анимация (те же спрайты что у HumanUnit)
    private AnimatedSprite2D _animSprite;
    private int              _currentDir = -1;

    private const string SpriteFolder  = "res://Assets/Units/";
    private const int    FrameW        = 480;    // human_walk_*.png: 4320/9
    private const int    FrameH        = 880;    // высота кадра
    private const int    FramesPerDir  = 9;
    private const int    DirCount      = 8;
    private const float  SpriteScale   = 0.114f;
    private const float  SpriteOffY    = -50f;   // ноги на origin
    private const float  Fps           = 6f;

    // Те же 8 направлений что в HumanUnit
    private static readonly string[] DirNames =
        { "E", "SE", "S", "SW", "W", "NW", "N", "NE" };

    // ─── Инициализация ────────────────────────────────────────────────────────

    public void Initialize(Building warehouse, WarehouseComponent wComp, IsoTileMap tileMap)
    {
        _warehouse     = warehouse;
        _warehouseComp = wComp;
        _tileMap       = tileMap;
        GlobalPosition = warehouse.GlobalPosition;
        ZIndex         = 3;

        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        // 8 анимаций ходьбы: human_walk_E.png … human_walk_NE.png
        foreach (var dir in DirNames)
        {
            string anim = $"walk_{dir}";
            frames.AddAnimation(anim);
            frames.SetAnimationSpeed(anim, Fps);
            frames.SetAnimationLoop(anim, true);

            var tex = LoadTex($"{SpriteFolder}human_walk_{dir}.png");
            if (tex == null) continue;

            for (int i = 0; i < FramesPerDir; i++)
                frames.AddFrame(anim, new AtlasTexture
                {
                    Atlas  = tex,
                    Region = new Rect2(i * FrameW, 0, FrameW, FrameH)
                });
        }

        // Idle — первый кадр idle_S, полупрозрачный
        frames.AddAnimation("idle");
        frames.SetAnimationSpeed("idle", 1f);
        frames.SetAnimationLoop("idle", false);
        var idleTex = LoadTex($"{SpriteFolder}human_idle_S.png");
        if (idleTex != null) frames.AddFrame("idle", idleTex);

        _animSprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Scale        = Vector2.One * SpriteScale,
            Position     = new Vector2(0f, SpriteOffY),
            Modulate     = new Color(1f, 1f, 1f, 0.5f)
        };
        _animSprite.Play("idle");
        AddChild(_animSprite);
    }

    // ─── Загрузка текстуры (без .import через Image.LoadFromFile) ────────────

    private static Texture2D LoadTex(string resPath)
    {
        var t = ResourceLoader.Load<Texture2D>(resPath);
        if (t != null) return t;

        string osPath = ProjectSettings.GlobalizePath(resPath);
        var img = Image.LoadFromFile(osPath);
        if (img != null) return ImageTexture.CreateFromImage(img);

        GD.PushWarning($"[CartAgent] Текстура не найдена: {resPath}");
        return null;
    }

    // ─── Направление → индекс 0-7 ────────────────────────────────────────────

    /// <summary>Вектор движения → индекс направления 0-7 (8 секторов по 45°).</summary>
    private static int GetDirectionIndex(Vector2 move)
    {
        float angle = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(move.Y, move.X)), 360f);
        return (int)(Mathf.Round(angle / 45f) % 8);
    }

    // ─── Запуск рейса ─────────────────────────────────────────────────────────

    public void StartTrip(Building source)
    {
        _path = NavigationManager.Instance.FindBuildingPath(_warehouse, source);
        if (_path.Length == 0)
        {
            GD.Print($"[Cart] Нет пути к {source.Name} — нет дороги.");
            return;
        }
        _sourceBuilding = source;
        State           = CartState.MovingToSource;
        _pathIndex      = 0;
        if (_animSprite != null) _animSprite.Modulate = Colors.White;
        QueueRedraw();
    }

    // ─── Обновление ───────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        switch (State)
        {
            case CartState.MovingToSource:    if (MoveAlongPath((float)delta)) EnterPickUp();     break;
            case CartState.PickingUp:         PerformPickup();                                    break;
            case CartState.MovingToWarehouse: if (MoveAlongPath((float)delta)) EnterDelivering(); break;
            case CartState.Delivering:        PerformDelivery();                                  break;
        }
    }

    // ─── Логика состояний ─────────────────────────────────────────────────────

    private void EnterPickUp() => State = CartState.PickingUp;

    private void PerformPickup()
    {
        var inv = _sourceBuilding?.GetNodeOrNull<InventoryComponent>("InventoryComponent");
        if (inv == null) { ReturnIdle(); return; }

        var (resId, available) = inv.FirstResource();
        if (resId == null) { ReturnIdle(); return; }

        int take = Mathf.Min(MaxLoad, available);
        _cargoResourceId = resId;
        _cargoAmount     = inv.TryRemove(resId, take);

        _path = NavigationManager.Instance.FindBuildingPath(_sourceBuilding, _warehouse);
        if (_path.Length == 0)
        {
            inv.TryAdd(_cargoResourceId, _cargoAmount);
            _cargoResourceId = null;
            _cargoAmount     = 0;
            ReturnIdle();
            return;
        }
        _pathIndex = 0;
        State      = CartState.MovingToWarehouse;
        QueueRedraw();
    }

    private void EnterDelivering() => State = CartState.Delivering;

    private void PerformDelivery()
    {
        if (_cargoAmount > 0 && _cargoResourceId != null)
        {
            ResourceManager.Instance.Add(_cargoResourceId, _cargoAmount);
            GD.Print($"[Cart] Доставил: +{_cargoAmount} {_cargoResourceId} в склад.");
        }
        _cargoResourceId = null;
        _cargoAmount     = 0;
        ReturnIdle();
    }

    private void ReturnIdle()
    {
        State           = CartState.Idle;
        _sourceBuilding = null;
        _path           = null;
        _currentDir     = -1;
        if (_animSprite != null)
        {
            _animSprite.Play("idle");
            _animSprite.Modulate = new Color(1, 1, 1, 0.5f);
        }
        QueueRedraw();
    }

    // ─── Движение ─────────────────────────────────────────────────────────────

    private bool MoveAlongPath(float delta)
    {
        if (_path == null || _pathIndex >= _path.Length) return true;

        var   target = _path[_pathIndex];
        var   dir    = target - GlobalPosition;
        float dist   = dir.Length();

        if (dist < 2f)
        {
            GlobalPosition = target;
            _pathIndex++;
            if (_pathIndex >= _path.Length) return true;
        }
        else
        {
            var move = dir.Normalized();
            GlobalPosition += move * Speed * delta;

            // Переключаем анимацию по направлению
            if (_animSprite != null)
            {
                int newDir = GetDirectionIndex(move);
                if (newDir != _currentDir)
                {
                    _currentDir = newDir;
                    _animSprite.Play($"walk_{DirNames[newDir]}");
                }
            }
        }
        QueueRedraw();
        return false;
    }

    // ─── Визуал ───────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_cargoAmount > 0)
            DrawString(ThemeDB.FallbackFont, new Vector2(-8, -28),
                $"{_cargoAmount}", HorizontalAlignment.Left, -1, 11, Colors.White);
    }

    public Vector2[] CurrentPath => _path;
}
