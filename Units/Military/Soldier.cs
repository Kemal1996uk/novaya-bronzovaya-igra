using Godot;

/// <summary>
/// Солдат — военный юнит игрока. Спавнится из Казармы.
///
/// Управление:
///   ЛКМ на солдате  — выделить / снять выделение
///   ПКМ (выделен)   — приказ двигаться в точку
///   ESC             — снять выделение
///
/// Автоматически атакует Бандитов в радиусе AttackRange.
/// Спрайты: те же что у HumanUnit (стальной оттенок).
/// </summary>
public partial class Soldier : Node2D
{
    // ─── Параметры боя ────────────────────────────────────────────────────────
    public int   MaxHp       { get; private set; } = 100;
    public int   CurrentHp   { get; private set; } = 100;
    public int   AttackPower { get; private set; } = 20;
    public float AttackRange { get; private set; } = 65f;
    public float MoveSpeed   { get; private set; } = 55f;
    public bool  IsDead      { get; private set; }

    private const float AttackCooldownSec = 1.5f;
    private const float SelectRadius      = 20f;

    // ─── Спрайт (аналогично HumanUnit) ───────────────────────────────────────
    private const float IdleScale   = 0.19f;
    private const float WalkScale   = 0.114f;
    private const float IdleOffsetY = -49f;
    private const float WalkOffsetY = -50f;
    private const int   WalkFrameW  = 480;
    private const int   WalkFrameH  = 880;
    private const int   WalkFrameN  = 9;
    private const float WalkFps     = 6f;
    private static readonly string[] DirNames =
        { "E", "SE", "S", "SW", "W", "NW", "N", "NE" };

    // ─── Состояние ───────────────────────────────────────────────────────────
    private AnimatedSprite2D _sprite;
    private bool    _selected;
    private Vector2 _moveTarget;
    private bool    _hasMove;
    private bool    _moving;
    private string  _curDir = "S";

    private Bandit  _attackTarget;
    private float   _attackTimer;

    // Только один солдат выделён одновременно
    private static Soldier _currentlySelected;

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        ZIndex = 3;
        AddToGroup("soldiers");
        BuildSpriteFrames();
        EventBus.Instance?.EmitSignal(EventBus.SignalName.SoldierSpawned, this);
    }

    private void BuildSpriteFrames()
    {
        var frames = new SpriteFrames();
        frames.RemoveAnimation("default");

        foreach (var dir in DirNames)
        {
            // ── Idle: 1 кадр ─────────────────────────────────────────────────
            string idleName = $"idle_{dir}";
            frames.AddAnimation(idleName);
            frames.SetAnimationLoop(idleName, false);
            frames.SetAnimationSpeed(idleName, 1f);
            var idleTex = LoadTex($"res://Assets/Units/human_idle_{dir}.png");
            if (idleTex != null) frames.AddFrame(idleName, idleTex);

            // ── Walk: 9 кадров ────────────────────────────────────────────────
            string walkName = $"walk_{dir}";
            frames.AddAnimation(walkName);
            frames.SetAnimationLoop(walkName, true);
            frames.SetAnimationSpeed(walkName, WalkFps);
            var walkTex = LoadTex($"res://Assets/Units/human_walk_{dir}.png");
            if (walkTex != null)
                for (int i = 0; i < WalkFrameN; i++)
                    frames.AddFrame(walkName, new AtlasTexture
                    {
                        Atlas  = walkTex,
                        Region = new Rect2(i * WalkFrameW, 0, WalkFrameW, WalkFrameH),
                    });
        }

        _sprite = new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Position     = new Vector2(0f, IdleOffsetY),
            Scale        = new Vector2(IdleScale, IdleScale),
            Modulate     = new Color(0.75f, 0.88f, 1.10f), // стальной оттенок
        };
        AddChild(_sprite);
        _sprite.Play("idle_S");
    }

    private static Texture2D LoadTex(string path)
    {
        var t = ResourceLoader.Load<Texture2D>(path);
        if (t != null) return t;
        string os = ProjectSettings.GlobalizePath(path);
        var img = Image.LoadFromFile(os);
        return img != null ? ImageTexture.CreateFromImage(img) : null;
    }

    // ─── Обновление ───────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (IsDead) return;

        _attackTimer = Mathf.Max(0f, _attackTimer - (float)delta);

        // Ищем бандита в радиусе
        if (_attackTarget == null || !IsInstanceValid(_attackTarget) || _attackTarget.IsDead)
            _attackTarget = FindNearestBandit();

        if (_attackTarget != null && IsInstanceValid(_attackTarget) && !_attackTarget.IsDead)
        {
            float dist = GlobalPosition.DistanceTo(_attackTarget.GlobalPosition);
            if (dist <= AttackRange)
            {
                FaceToward(_attackTarget.GlobalPosition);
                StopWalkAnim();
                if (_attackTimer <= 0f)
                {
                    _attackTarget.TakeDamage(AttackPower);
                    _attackTimer = AttackCooldownSec;
                }
            }
            else
            {
                // Двигаемся к врагу (приоритет над waypoint)
                MoveTick(_attackTarget.GlobalPosition, (float)delta);
            }
        }
        else if (_hasMove)
        {
            if (GlobalPosition.DistanceTo(_moveTarget) < 5f)
            {
                _hasMove = false;
                StopWalkAnim();
            }
            else
            {
                MoveTick(_moveTarget, (float)delta);
            }
        }
        else
        {
            StopWalkAnim();
        }

        ZIndex = (int)(GlobalPosition.Y / 32f) + 3;
        QueueRedraw();
    }

    private void MoveTick(Vector2 target, float dt)
    {
        var   dir     = (target - GlobalPosition).Normalized();
        string dirName = MoveToDir(dir);
        GlobalPosition += dir * MoveSpeed * dt;
        if (!_moving || dirName != _curDir)
        {
            _curDir = dirName;
            _moving = true;
            PlayAnim(true, dirName);
        }
    }

    private void FaceToward(Vector2 target)
    {
        var dir    = (target - GlobalPosition).Normalized();
        string dn  = MoveToDir(dir);
        if (dn != _curDir || _moving)
        {
            _curDir = dn;
            _moving = false;
            PlayAnim(false, dn);
        }
    }

    private void StopWalkAnim()
    {
        if (!_moving) return;
        _moving = false;
        PlayAnim(false, _curDir);
    }

    private void PlayAnim(bool walk, string dir)
    {
        string anim  = walk ? $"walk_{dir}" : $"idle_{dir}";
        float  scale = walk ? WalkScale : IdleScale;
        float  offY  = walk ? WalkOffsetY : IdleOffsetY;
        if (!_sprite.SpriteFrames.HasAnimation(anim))
            anim = walk ? "walk_S" : "idle_S";
        if (_sprite.Animation.Equals(anim)) return;
        _sprite.Scale    = new Vector2(scale, scale);
        _sprite.Position = new Vector2(0f, offY);
        _sprite.Play(anim);
    }

    private static string MoveToDir(Vector2 move)
    {
        if (move == Vector2.Zero) return "S";
        float ang = Mathf.PosMod(
            Mathf.RadToDeg(Mathf.Atan2(move.Y, move.X)) + 22.5f, 360f);
        return DirNames[(int)(ang / 45f) % 8];
    }

    private Bandit FindNearestBandit()
    {
        Bandit best = null;
        float  minD = float.MaxValue;
        foreach (var node in GetTree().GetNodesInGroup("bandits"))
        {
            if (node is Bandit b && !b.IsDead)
            {
                float d = GlobalPosition.DistanceTo(b.GlobalPosition);
                if (d < minD) { minD = d; best = b; }
            }
        }
        return best;
    }

    // ─── Урон / смерть ────────────────────────────────────────────────────────

    public void TakeDamage(int amount)
    {
        if (IsDead) return;
        CurrentHp -= amount;
        CurrentHp  = Mathf.Max(0, CurrentHp);
        QueueRedraw();
        if (CurrentHp <= 0) Die();
    }

    private void Die()
    {
        IsDead = true;
        if (_currentlySelected == this) _currentlySelected = null;
        EventBus.Instance?.EmitSignal(EventBus.SignalName.CombatUnitDied, this, false);
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 0.8f);
        tw.TweenCallback(Callable.From(() => QueueFree()));
    }

    // ─── Ввод ─────────────────────────────────────────────────────────────────

    public override void _Input(InputEvent @event)
    {
        if (IsDead) return;

        if (@event is InputEventKey key && key.Pressed && !key.Echo
            && key.Keycode == Key.Escape)
        {
            if (_selected) { Deselect(); GetViewport().SetInputAsHandled(); }
            return;
        }

        if (@event is InputEventMouseButton mb && mb.Pressed)
        {
            var   mp   = GetGlobalMousePosition();
            float dist = (mp - (GlobalPosition + new Vector2(0f, -30f))).Length();

            if (mb.ButtonIndex == MouseButton.Left)
            {
                if (dist <= SelectRadius)
                {
                    if (_selected) Deselect(); else Select();
                    GetViewport().SetInputAsHandled();
                }
                else if (_selected)
                {
                    // ЛКМ в другое место = приказ двигаться
                    _moveTarget   = mp;
                    _hasMove      = true;
                    _attackTarget = null;
                }
            }
            else if (mb.ButtonIndex == MouseButton.Right && _selected)
            {
                // ПКМ = приказ двигаться
                _moveTarget   = mp;
                _hasMove      = true;
                _attackTarget = null;
                GetViewport().SetInputAsHandled();
            }
        }
    }

    // ─── Выделение ────────────────────────────────────────────────────────────

    private void Select()
    {
        // Снимаем выделение с предыдущего
        if (_currentlySelected != null && _currentlySelected != this)
            _currentlySelected.Deselect();
        _currentlySelected = this;
        _selected          = true;
        _sprite.Modulate   = new Color(1.0f, 1.2f, 0.5f); // золотой
        QueueRedraw();
    }

    private void Deselect()
    {
        _selected        = false;
        if (_currentlySelected == this) _currentlySelected = null;
        _sprite.Modulate = new Color(0.75f, 0.88f, 1.10f); // стальной
        QueueRedraw();
    }

    // ─── Отрисовка ────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        // HP-бар над персонажем
        float ratio = (float)CurrentHp / MaxHp;
        float barW  = 28f;
        float barY  = IdleOffsetY - 14f;
        DrawRect(new Rect2(-barW / 2f, barY, barW, 4f), new Color(0.15f, 0.15f, 0.15f, 0.8f));
        DrawRect(new Rect2(-barW / 2f, barY, barW * ratio, 4f),
                 ratio > 0.5f ? Colors.LimeGreen : Colors.OrangeRed);

        // Круг выделения
        if (_selected)
            DrawArc(new Vector2(0f, 6f), 16f, 0f, Mathf.Tau, 24,
                    new Color(0.2f, 0.8f, 1f, 0.9f), 2.5f);
    }
}
