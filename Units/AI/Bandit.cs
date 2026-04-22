using Godot;

/// <summary>
/// Бандит — вражеский юнит. Спавнится CombatManager волнами.
///
/// Поведение:
///   1. Если солдат ближе 150px — атакует солдата
///   2. Иначе — идёт к ближайшему зданию и атакует его
///
/// Визуал: красный ромб + HP-бар (без спрайтов).
/// </summary>
public partial class Bandit : Node2D
{
    // ─── Параметры ────────────────────────────────────────────────────────────
    public int   MaxHp       { get; private set; } = 60;
    public int   CurrentHp   { get; private set; } = 60;
    public int   AttackPower { get; private set; } = 8;
    public float AttackRange { get; private set; } = 55f;
    public float MoveSpeed   { get; private set; } = 40f;
    public bool  IsDead      { get; private set; }

    private const float AttackCooldownSec  = 2.5f;
    private const float SoldierAggroRadius = 150f; // радиус обнаружения солдат

    private float    _attackTimer;
    private Building _buildingTarget;
    private Vector2  _moveDir = Vector2.Down;

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        ZIndex = 3;
        AddToGroup("bandits");
        EventBus.Instance?.EmitSignal(EventBus.SignalName.BanditSpawned, this);
    }

    // ─── Обновление ───────────────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (IsDead) return;

        _attackTimer = Mathf.Max(0f, _attackTimer - (float)delta);

        // Приоритет 1: солдат поблизости
        var soldier = FindNearestSoldier();
        if (soldier != null)
        {
            float d = GlobalPosition.DistanceTo(soldier.GlobalPosition);
            if (d <= AttackRange)
            {
                // Атакуем солдата
                _moveDir = (soldier.GlobalPosition - GlobalPosition).Normalized();
                if (_attackTimer <= 0f)
                {
                    soldier.TakeDamage(AttackPower);
                    _attackTimer = AttackCooldownSec;
                }
            }
            else
            {
                MoveTick(soldier.GlobalPosition, (float)delta);
            }
            ZIndex = (int)(GlobalPosition.Y / 32f) + 3;
            QueueRedraw();
            return;
        }

        // Приоритет 2: атакуем здание
        if (_buildingTarget == null || !IsInstanceValid(_buildingTarget))
            _buildingTarget = FindNearestBuilding();

        if (_buildingTarget != null && IsInstanceValid(_buildingTarget))
        {
            float d = GlobalPosition.DistanceTo(_buildingTarget.GlobalPosition);
            if (d <= AttackRange)
            {
                _moveDir = (_buildingTarget.GlobalPosition - GlobalPosition).Normalized();
                if (_attackTimer <= 0f)
                {
                    _buildingTarget.TakeDamage(AttackPower);
                    _attackTimer = AttackCooldownSec;
                }
            }
            else
            {
                MoveTick(_buildingTarget.GlobalPosition, (float)delta);
            }
        }

        ZIndex = (int)(GlobalPosition.Y / 32f) + 3;
        QueueRedraw();
    }

    private void MoveTick(Vector2 target, float dt)
    {
        _moveDir        = (target - GlobalPosition).Normalized();
        GlobalPosition += _moveDir * MoveSpeed * dt;
    }

    private Soldier FindNearestSoldier()
    {
        Soldier best = null;
        float   minD = SoldierAggroRadius;
        foreach (var node in GetTree().GetNodesInGroup("soldiers"))
        {
            if (node is Soldier s && !s.IsDead)
            {
                float d = GlobalPosition.DistanceTo(s.GlobalPosition);
                if (d < minD) { minD = d; best = s; }
            }
        }
        return best;
    }

    private Building FindNearestBuilding()
    {
        Building best = null;
        float    minD = float.MaxValue;
        foreach (var b in BuildingRegistry.Instance.AllBuildings)
        {
            if (!IsInstanceValid(b)) continue;
            float d = GlobalPosition.DistanceTo(b.GlobalPosition);
            if (d < minD) { minD = d; best = b; }
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
        EventBus.Instance?.EmitSignal(EventBus.SignalName.CombatUnitDied, this, true);
        var tw = CreateTween();
        tw.TweenProperty(this, "modulate:a", 0f, 0.6f);
        tw.TweenCallback(Callable.From(() => QueueFree()));
    }

    // ─── Отрисовка ────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (IsDead) return;

        // Красный ромб — силуэт бандита
        var pts = new Vector2[]
        {
            new(0f, -22f), new(14f, 0f), new(0f, 14f), new(-14f, 0f),
        };
        DrawColoredPolygon(pts, new Color(0.85f, 0.15f, 0.12f));
        DrawPolyline(
            new[] { pts[0], pts[1], pts[2], pts[3], pts[0] },
            new Color(0.45f, 0.0f, 0.0f), 2f);

        // Черепок (skull dot) наверху
        DrawCircle(new Vector2(0f, -14f), 5f, new Color(0.0f, 0.0f, 0.0f, 0.5f));

        // HP-бар
        float ratio = (float)CurrentHp / MaxHp;
        float barW  = 26f;
        DrawRect(new Rect2(-barW / 2f, -33f, barW, 4f), new Color(0.15f, 0.0f, 0.0f, 0.85f));
        DrawRect(new Rect2(-barW / 2f, -33f, barW * ratio, 4f), Colors.OrangeRed);
    }
}
