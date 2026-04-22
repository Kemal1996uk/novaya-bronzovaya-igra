using Godot;
using System.Collections.Generic;

/// <summary>
/// Полоса ресурсов вверху экрана.
/// При наведении на блок — всплывает кастомная панель с данными о добыче/потреблении.
/// </summary>
public partial class ResourceBar : CanvasLayer
{
    private readonly Dictionary<string, Label>   _labels     = new();
    private readonly Dictionary<string, Control> _containers = new();

    // Панель жителей (отдельно от ResourceManager)
    private Label _immigrantsLabel;
    private Label _citizensLabel;

    // Кастомный тултип
    private PanelContainer _hoverPanel;
    private Label          _hoverLabel;
    private string         _hoveredId;

    private float _tooltipRefresh;

    // Таймер сессии
    private double _sessionSeconds;
    private Label  _timerLabel;

    public override void _Ready()
    {
        Layer = 10;

        // ── Верхняя панель ────────────────────────────────────────────────────
        var panel = new PanelContainer();
        panel.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        panel.CustomMinimumSize = new Vector2(0, 60);
        panel.MouseFilter       = Control.MouseFilterEnum.Pass;
        AddChild(panel);

        var hbox = new HBoxContainer();
        hbox.Alignment = BoxContainer.AlignmentMode.Center;
        hbox.AddThemeConstantOverride("separation", 4);
        hbox.MouseFilter = Control.MouseFilterEnum.Pass;
        panel.AddChild(hbox);

        AddItem(hbox, "gold",  "🪙 Золото");
        AddItem(hbox, "wood",  "🪵 Дерево");
        AddItem(hbox, "stone", "🪨 Камень");
        AddItem(hbox, "grain", "🌾 Зерно");
        AddItem(hbox, "fish",  "🐟 Рыба");

        // ── Жители (иммигранты и граждане) ───────────────────────────────────
        _immigrantsLabel = AddWorkerItem(hbox, new Color(0.6f, 0.9f, 1f));
        _citizensLabel   = AddWorkerItem(hbox, new Color(1f, 0.85f, 0.3f));

        // ── Таймер сессии ─────────────────────────────────────────────────────
        var timerBg = new PanelContainer();
        timerBg.CustomMinimumSize   = new Vector2(120, 0);
        timerBg.MouseFilter         = Control.MouseFilterEnum.Ignore;

        _timerLabel = new Label
        {
            Text                = "⏱ 00:00:00",
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        _timerLabel.AddThemeFontSizeOverride("font_size", 20);
        timerBg.AddChild(_timerLabel);
        hbox.AddChild(timerBg);

        // ── Кастомная тултип-панель ───────────────────────────────────────────
        _hoverPanel = new PanelContainer();
        _hoverPanel.CustomMinimumSize = new Vector2(320, 0);
        _hoverPanel.Visible           = false;
        _hoverPanel.MouseFilter       = Control.MouseFilterEnum.Ignore;
        _hoverPanel.ZIndex            = 20;

        _hoverLabel = new Label();
        _hoverLabel.AutowrapMode   = TextServer.AutowrapMode.Off;
        _hoverLabel.MouseFilter    = Control.MouseFilterEnum.Ignore;
        _hoverLabel.AddThemeFontSizeOverride("font_size", 17);
        _hoverPanel.AddChild(_hoverLabel);
        AddChild(_hoverPanel);

        EventBus.Instance.StockpileChanged += Refresh;

        // Подписка на изменение пула жителей
        if (WorkerManager.Instance != null)
            WorkerManager.Instance.WorkersChanged += RefreshWorkers;

        SetProcess(true);
        Refresh();
        RefreshWorkers();
    }

    public override void _Process(double delta)
    {
        // ── Таймер сессии ─────────────────────────────────────────────────────
        _sessionSeconds += delta;
        int total   = (int)_sessionSeconds;
        int hours   = total / 3600;
        int minutes = (total % 3600) / 60;
        int seconds = total % 60;
        _timerLabel.Text = $"⏱ {hours:D2}:{minutes:D2}:{seconds:D2}";

        // ── Тултип ────────────────────────────────────────────────────────────
        if (_hoveredId != null)
        {
            _tooltipRefresh += (float)delta;
            if (_tooltipRefresh >= 1f)
            {
                _tooltipRefresh = 0f;
                _hoverLabel.Text = BuildTooltip(_hoveredId);
            }
        }
    }

    // ─── Построение ───────────────────────────────────────────────────────────

    private Label AddWorkerItem(HBoxContainer hbox, Color fontColor)
    {
        var bg = new PanelContainer();
        bg.CustomMinimumSize   = new Vector2(190, 0);
        bg.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        bg.MouseFilter         = Control.MouseFilterEnum.Ignore;

        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Text                = "...",
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 18);
        label.AddThemeColorOverride("font_color", fontColor);
        bg.AddChild(label);
        hbox.AddChild(bg);
        return label;
    }

    private void AddItem(HBoxContainer hbox, string id, string displayName)
    {
        var bg = new PanelContainer();
        bg.CustomMinimumSize   = new Vector2(170, 0);
        bg.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        bg.MouseFilter         = Control.MouseFilterEnum.Stop;

        var label = new Label
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
            Text                = displayName + ": ...",
            MouseFilter         = Control.MouseFilterEnum.Ignore,
        };
        label.AddThemeFontSizeOverride("font_size", 20);

        bg.AddChild(label);
        hbox.AddChild(bg);

        _labels[id]     = label;
        _containers[id] = bg;

        // ── Наведение ─────────────────────────────────────────────────────────
        bg.MouseEntered += () => OnHoverEnter(id, bg);
        bg.MouseExited  += () => OnHoverExit();
    }

    // ─── Hover-события ────────────────────────────────────────────────────────

    private void OnHoverEnter(string id, Control bg)
    {
        _hoveredId      = id;
        _tooltipRefresh = 0f;

        _hoverLabel.Text = BuildTooltip(id);

        // Позиция: под ресурсным блоком
        var rect = bg.GetGlobalRect();
        float x  = Mathf.Clamp(rect.Position.X, 4f,
            GetViewport().GetVisibleRect().Size.X - _hoverPanel.CustomMinimumSize.X - 4f);
        _hoverPanel.Position = new Vector2(x, rect.End.Y + 4f);

        _hoverPanel.Visible = true;
    }

    private void OnHoverExit()
    {
        _hoveredId          = null;
        _hoverPanel.Visible = false;
    }

    // ─── Обновление значений ──────────────────────────────────────────────────

    private void Refresh()
    {
        var rm = ResourceManager.Instance;
        if (rm == null) return;

        _labels["gold"].Text  = $"🪙 Золото: {rm.Get("gold")}";
        _labels["wood"].Text  = $"🪵 Дерево: {rm.Get("wood")}";
        _labels["stone"].Text = $"🪨 Камень: {rm.Get("stone")}";
        _labels["grain"].Text = $"🌾 Зерно: {rm.Get("grain")}";
        _labels["fish"].Text  = $"🐟 Рыба: {rm.Get("fish")}";
    }

    private void RefreshWorkers()
    {
        var wm = WorkerManager.Instance;
        if (wm == null) return;

        // Иммигранты: жители домов L1
        int immTotal = wm.TotalImmigrants;
        int immUsed  = Mathf.Min(wm.UsedWorkers, immTotal);
        int immAvail = immTotal - immUsed;
        _immigrantsLabel.Text =
            $"🏚 Дома L1: {wm.HousesL1}\n" +
            $"👤 Иммигр.: {immAvail}/{immTotal}";

        // Граждане: жители домов L2
        int citTotal = wm.TotalCitizens;
        int citAvail = wm.AvailableCitizens;
        _citizensLabel.Text =
            $"🏠 Дома L2: {wm.HousesL2}\n" +
            $"🏛 Граждане: {citAvail}/{citTotal}";
    }

    // ─── Построение текста тултипа ────────────────────────────────────────────

    private string BuildTooltip(string id)
    {
        if (id == "gold") return BuildGoldTooltip();

        float prodPerSec  = 0f;
        int   activeCount = 0;
        int   totalCount  = 0;
        float consPerSec  = 0f;

        if (BuildingRegistry.Instance != null)
        {
            foreach (var b in BuildingRegistry.Instance.AllBuildings)
            {
                if (b.Data?.OutputResourceId == id)
                {
                    totalCount++;
                    var prod = b.GetNodeOrNull<ProductionCycleComponent>("ProductionCycleComponent");
                    if (prod != null && prod.IsProducing)
                    {
                        prodPerSec += b.Data.OutputAmount / b.Data.ProductionCycleSec;
                        activeCount++;
                    }
                }

                if (id == "fish" || id == "grain")
                {
                    var house = b.GetNodeOrNull<HouseLevelComponent>("HouseLevelComponent");
                    if (house != null)
                        consPerSec += 1f / 60f; // 1 ед. каждую минуту
                }
            }
        }

        float prodMin = prodPerSec * 60f;
        float consMin = consPerSec * 60f;
        float balance = prodMin - consMin;

        var sb = new System.Text.StringBuilder();

        if (totalCount == 0)
            sb.AppendLine("Производство: нет зданий");
        else
        {
            string prodStr = prodMin > 0
                ? $"+{prodMin:F1} / мин"
                : "0 / мин  (нет дороги или ресурса рядом)";
            sb.AppendLine($"Производство: {prodStr}");
            sb.AppendLine($"  Активных зданий: {activeCount} / {totalCount}");
        }

        if (consMin > 0)
        {
            sb.AppendLine($"Потребление:  -{consMin:F1} / мин  (дома)");
            string balStr = balance >= 0
                ? $"+{balance:F1} / мин  ✓"
                : $"{balance:F1} / мин  ⚠ дефицит!";
            sb.Append($"Баланс:       {balStr}");
        }
        else if (totalCount > 0)
        {
            sb.Append("Потребление: нет");
        }

        return sb.ToString().TrimEnd();
    }

    private string BuildGoldTooltip()
    {
        int houses = 0, full = 0, partial = 0;
        int actualIncome = 0;
        if (BuildingRegistry.Instance != null)
        {
            foreach (var b in BuildingRegistry.Instance.AllBuildings)
            {
                var h = b.GetNodeOrNull<HouseLevelComponent>("HouseLevelComponent");
                if (h == null) continue;
                houses++;
                if (h.Satisfaction == HouseLevelComponent.SatisfactionState.Full)    { full++;    actualIncome += 100; }
                else if (h.Satisfaction == HouseLevelComponent.SatisfactionState.Partial) { partial++; actualIncome += 50; }
            }
        }
        if (houses == 0) return "Нет домов.\nСтройте дома для дохода.";
        string balStr = actualIncome > 0 ? $"+{actualIncome} 🪙/мин" : "0 🪙/мин  ⚠ дома голодают";
        return $"Доход сейчас:     {balStr}\n" +
               $"Сыты полностью:   {full} / {houses}\n" +
               $"Частично сыты:    {partial} / {houses}\n" +
               $"(полные: +100🪙/мин, частичные: +50🪙/мин)";
    }
}
