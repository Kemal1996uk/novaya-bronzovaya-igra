using Godot;

/// <summary>
/// Управляемый персонаж. Появляется при строительстве дома.
/// Левый клик на персонаже — выделить/снять выделение.
/// Левый клик на карте (пока выделен) — переместиться туда.
/// Escape — снять выделение.
///
/// Спрайты (Assets/Units/):
///   human_idle_{dir}.png  — 256×512px, 1 кадр
///   human_walk_{dir}.png  — 4320×880px, кадр 480×880, 9 кадров в ряд
///   dir ∈ { S, SE, E, SW, N, NW, W, NE }
/// </summary>
public partial class HumanUnit : Node2D
{
    // ─── Константы спрайта ────────────────────────────────────────────────────
    //
    // Idle: 256×512px,  персонаж 460px, ноги у нижнего края
    // Walk: 4320×880px, 9 кадров по 480×880, персонаж 800px, ноги у нижнего края
    // Все направления нормализованы к одинаковому размеру.
    //
    private const float IdleScale    = 0.19f;    // 512  × 0.19 ≈  97px отображаемая высота
    private const float WalkScale    = 0.114f;   // 880  × 0.114 ≈ 100px отображаемая высота
    private const float IdleOffsetY  = -49f;     // 512/2 × 0.19 = 48.6 → -49px
    private const float WalkOffsetY  = -50f;     // 880/2 × 0.114 = 50.2 → -50px

    private const int   WalkFrameW   = 480;      // ширина кадра (4320 / 9)
    private const int   WalkFrameH   = 880;      // высота кадра
    private const int   WalkFrameN   = 9;        // кадров в ряду
    private const float WalkFps      = 6f;       // 9 кадров / 6fps = 1.5с цикл

    // ─── Направления ──────────────────────────────────────────────────────────

    // Формула atan2 даёт индекс 0-7 (по часовой от East в screen-space):
    //   0=E, 1=SE, 2=S, 3=SW, 4=W, 5=NW, 6=N, 7=NE
    // Эти имена совпадают с именами файлов — смещение не нужно.
    private static readonly string[] DirByFormula =
        { "E", "SE", "S", "SW", "W", "NW", "N", "NE" };

    // ─── Движение / выделение ─────────────────────────────────────────────────

    private const float Speed        = 70f;
    private const float SelectRadius = 50f;

    // ─── Поля ─────────────────────────────────────────────────────────────────

    private AnimatedSprite2D _sprite;
    private bool             _selected;
    private Vector2[]        _path;
    private int              _pathIdx;
    private string           _curDir = "S";
    private bool             _moving = false;

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        ZIndex = 3;
        _path  = System.Array.Empty<Vector2>();
        BuildSpriteFrames();
    }

    private void BuildSpriteFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        foreach (var dir in DirByFormula)
        {
            // ── Idle: один кадр 256×512 ───────────────────────────────────────
            string idleName = $"idle_{dir}";
            frames.AddAnimation(idleName);
            frames.SetAnimationLoop(idleName, false);
            frames.SetAnimationSpeed(idleName, 1f);
            var idleTex = LoadTex($"res://Assets/Units/human_idle_{dir}.png");
            if (idleTex == null) { GD.PushError($"[HumanUnit] Idle текстура null: {idleName}"); frames.RemoveAnimation(idleName); continue; }
            frames.AddFrame(idleName, idleTex);

            // ── Walk: 9 кадров в ряд ──────────────────────────────────────────
            string walkName = $"walk_{dir}";
            frames.AddAnimation(walkName);
            frames.SetAnimationLoop(walkName, true);
            frames.SetAnimationSpeed(walkName, WalkFps);
            var walkTex = LoadTex($"res://Assets/Units/human_walk_{dir}.png");
            if (walkTex == null) { GD.PushError($"[HumanUnit] Walk текстура null: {walkName}"); frames.RemoveAnimation(walkName); continue; }
            for (int i = 0; i < WalkFrameN; i++)
            {
                frames.AddFrame(walkName, new AtlasTexture
                {
                    Atlas  = walkTex,
                    Region = new Rect2(i * WalkFrameW, 0, WalkFrameW, WalkFrameH),
                });
            }
        }

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Position     = new Vector2(0f, IdleOffsetY),
            Scale        = new Vector2(IdleScale, IdleScale),
        };
        AddChild(_sprite);
        _sprite.Play("idle_S");
    }

    // ─── Загрузка текстуры (с fallback через Image если .import ещё нет) ─────

    private static Texture2D LoadTex(string resPath)
    {
        // Пробуем ResourceLoader (работает если Godot уже импортировал файл)
        var t = ResourceLoader.Load<Texture2D>(resPath);
        if (t != null) return t;

        // Fallback: Image.LoadFromFile берёт OS-путь, не требует .import-файла
        string osPath = ProjectSettings.GlobalizePath(resPath);
        var img = Image.LoadFromFile(osPath);
        if (img != null)
            return ImageTexture.CreateFromImage(img);

        GD.PushWarning($"[HumanUnit] Текстура не найдена: {resPath}");
        return null;
    }

    // ─── Обновление ───────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (_path != null && _pathIdx < _path.Length)
        {
            var   wp   = _path[_pathIdx];
            var   toWp = wp - GlobalPosition;
            float dist = toWp.Length();

            if (dist < 4f)
            {
                _pathIdx++;
                if (_pathIdx >= _path.Length)
                {
                    _path    = System.Array.Empty<Vector2>();
                    _pathIdx = 0;
                    StopMoving();
                }
            }
            else
            {
                var    move = toWp.Normalized();
                string dir  = MoveToDir(move);

                GlobalPosition += move * Speed * (float)delta;

                if (!_moving || dir != _curDir)
                {
                    _curDir = dir;
                    _moving = true;
                    PlayAnim(true, dir);
                }
            }
        }

        // ZIndex по Y — персонаж рисуется поверх объектов выше на экране
        ZIndex = (int)(GlobalPosition.Y / 32f) + 3;
        QueueRedraw();
    }

    private void StopMoving()
    {
        if (!_moving) return;
        _moving = false;
        PlayAnim(false, _curDir);
    }

    private void SetDestination(Vector2 target)
    {
        // Житель дома ходит свободно — без привязки к дорогам
        _path    = new[] { target };
        _pathIdx = 0;
    }

    private void PlayAnim(bool walk, string dir)
    {
        string anim    = walk ? $"walk_{dir}" : $"idle_{dir}";
        float  scale   = walk ? WalkScale : IdleScale;
        float  offsetY = walk ? WalkOffsetY : IdleOffsetY;

        // Если анимация не загрузилась — падаем на S
        if (!_sprite.SpriteFrames.HasAnimation(anim))
            anim = walk ? "walk_S" : "idle_S";

        if (_sprite.Animation.Equals(anim)) return;

        _sprite.Scale    = new Vector2(scale, scale);
        _sprite.Position = new Vector2(0f, offsetY);
        _sprite.Play(anim);
    }

    // ─── Ввод ─────────────────────────────────────────────────────────────────

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == Key.Escape)
        {
            if (_selected) { Deselect(); GetViewport().SetInputAsHandled(); }
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed
            && mb.ButtonIndex == MouseButton.Left)
        {
            float dist = (GetGlobalMousePosition() - (GlobalPosition + new Vector2(0f, -50f))).Length();

            if (dist <= SelectRadius)
            {
                if (_selected) Deselect();
                else           Select();
                GetViewport().SetInputAsHandled();
            }
            else if (_selected)
            {
                SetDestination(GetGlobalMousePosition());
            }
        }
    }

    // ─── Выделение ────────────────────────────────────────────────────────────

    private void Select()
    {
        _selected = true;
        if (_sprite != null) _sprite.Modulate = new Color(1.3f, 1.3f, 0.5f);
        QueueRedraw();
    }

    private void Deselect()
    {
        _selected = false;
        if (_sprite != null) _sprite.Modulate = Colors.White;
        QueueRedraw();
    }

    // ─── Кружок выделения ─────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_selected)
            DrawArc(new Vector2(0f, 6f), 15f, 0f, Mathf.Tau, 24,
                    new Color(1f, 1f, 0f, 0.9f), 2f);
    }

    // ─── Направление из вектора движения ─────────────────────────────────────

    /// <summary>
    /// Вектор движения (screen-space) → имя направления спрайта.
    /// Формула: atan2 по часовой от East, 45° секторы.
    /// 0=E,1=SE,2=S,3=SW,4=W,5=NW,6=N,7=NE → совпадает с именами файлов.
    /// </summary>
    private static string MoveToDir(Vector2 move)
    {
        if (move == Vector2.Zero) return "S";
        float angle = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(move.Y, move.X)), 360f);
        int   idx   = (int)Mathf.Round(angle / 45f) % 8;
        return DirByFormula[idx];
    }
}
