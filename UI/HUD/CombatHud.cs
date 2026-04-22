using Godot;

/// <summary>
/// Боевой HUD — накладывается поверх игры (CanvasLayer 10).
///
/// Показывает:
///   - "🚨 НАПАДЕНИЕ!" — мигает 6 секунд при появлении бандитов
///   - Счётчики: Солдат N / Бандитов N (обновляется каждую секунду)
/// </summary>
public partial class CombatHud : CanvasLayer
{
    private Label _attackAlert;
    private Label _countersLabel;

    private float _alertTimer;
    private float _counterRefresh;

    private const float AlertDuration   = 6f;
    private const float CounterInterval = 1f;

    public override void _Ready()
    {
        Layer = 10;
        BuildUI();

        EventBus.Instance.CityUnderAttack += () =>
        {
            _attackAlert.Visible = true;
            _alertTimer          = AlertDuration;
        };
    }

    private void BuildUI()
    {
        // ── Алерт "НАПАДЕНИЕ!" ────────────────────────────────────────────────
        _attackAlert = new Label
        {
            Text                = "🚨  НАПАДЕНИЕ!  🚨",
            HorizontalAlignment = HorizontalAlignment.Center,
            AnchorLeft          = 0.5f,
            AnchorRight         = 0.5f,
            AnchorTop           = 0.05f,
            AnchorBottom        = 0.05f,
            OffsetLeft          = -220f,
            OffsetRight         = 220f,
            OffsetTop           = 0f,
            OffsetBottom        = 50f,
            Visible             = false,
        };
        _attackAlert.AddThemeColorOverride("font_color", new Color(1f, 0.2f, 0.15f));
        _attackAlert.AddThemeFontSizeOverride("font_size", 30);
        AddChild(_attackAlert);

        // ── Счётчики юнитов ───────────────────────────────────────────────────
        _countersLabel = new Label
        {
            Text                = "",
            HorizontalAlignment = HorizontalAlignment.Right,
            AnchorLeft          = 1.0f,
            AnchorRight         = 1.0f,
            AnchorTop           = 0.0f,
            AnchorBottom        = 0.0f,
            OffsetLeft          = -200f,
            OffsetRight         = -8f,
            OffsetTop           = 4f,
            OffsetBottom        = 32f,
        };
        _countersLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.85f, 0.7f));
        _countersLabel.AddThemeFontSizeOverride("font_size", 16);
        AddChild(_countersLabel);
    }

    public override void _Process(double delta)
    {
        // Мигание алерта
        if (_alertTimer > 0f)
        {
            _alertTimer          -= (float)delta;
            _attackAlert.Visible  = (int)(_alertTimer * 2.5f) % 2 == 0;
            if (_alertTimer <= 0f)
                _attackAlert.Visible = false;
        }

        // Обновляем счётчики раз в секунду
        _counterRefresh -= (float)delta;
        if (_counterRefresh <= 0f)
        {
            _counterRefresh = CounterInterval;
            RefreshCounters();
        }
    }

    private void RefreshCounters()
    {
        if (GetTree() == null) return;
        int soldiers = GetTree().GetNodesInGroup("soldiers").Count;
        int bandits  = GetTree().GetNodesInGroup("bandits").Count;

        _countersLabel.Text = soldiers > 0 || bandits > 0
            ? $"⚔️ {soldiers}  🔴 {bandits}"
            : "";
    }
}
