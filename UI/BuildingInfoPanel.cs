using Godot;

/// <summary>
/// Нижняя информационная панель. Показывается при клике на здание.
/// Рабочие теперь берутся из глобального пула — кнопок ручного назначения нет.
/// Вместо этого показан статус из WorkerManager.
/// </summary>
public partial class BuildingInfoPanel : CanvasLayer
{
    private PanelContainer _panel;
    private Label          _nameLabel;
    private Label          _statusLabel;
    private Label          _detailLabel;
    private Label          _workerLabel;
    private Button         _upgradeHouseButton;
    private Button         _addFieldButton;
    private Button         _demolishButton;
    private Button         _closeButton;

    private Building _selected;

    public override void _Ready()
    {
        Layer = 10;
        BuildUI();
        EventBus.Instance.BuildingClicked      += OnBuildingClicked;
        EventBus.Instance.PlacementModeEntered += _ => Close();
        EventBus.Instance.DemolishModeEntered  += Close;
        EventBus.Instance.RoadModeEntered      += Close;
        SetProcess(false);
        _panel.Visible = false;
    }

    private void BuildUI()
    {
        _panel = new PanelContainer { Name = "BottomPanel" };
        _panel.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        _panel.CustomMinimumSize = new Vector2(0, 160);
        _panel.GrowVertical      = Control.GrowDirection.Begin;
        _panel.MouseFilter       = Control.MouseFilterEnum.Stop;
        AddChild(_panel);

        var hbox = new HBoxContainer();
        hbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        hbox.AddThemeConstantOverride("separation", 16);
        _panel.AddChild(hbox);

        // ── Левая: имя + статус ─────────────────────────────────────────────
        var leftVbox = new VBoxContainer();
        leftVbox.CustomMinimumSize   = new Vector2(220, 0);
        leftVbox.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        leftVbox.AddThemeConstantOverride("separation", 6);

        _nameLabel = new Label { Text = "Здание" };
        _nameLabel.AddThemeFontSizeOverride("font_size", 18);

        _statusLabel = new Label { Text = "" };
        _statusLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);

        leftVbox.AddChild(_nameLabel);
        leftVbox.AddChild(new HSeparator());
        leftVbox.AddChild(_statusLabel);
        hbox.AddChild(leftVbox);

        // ── Средняя: детали ──────────────────────────────────────────────────
        var midVbox = new VBoxContainer();
        midVbox.SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        midVbox.AddThemeConstantOverride("separation", 6);

        _detailLabel = new Label { Text = "" };
        _detailLabel.AutowrapMode = TextServer.AutowrapMode.Word;
        _detailLabel.AddThemeFontSizeOverride("font_size", 14);

        _workerLabel = new Label { Text = "" };
        _workerLabel.AddThemeFontSizeOverride("font_size", 14);

        midVbox.AddChild(_detailLabel);
        midVbox.AddChild(_workerLabel);
        hbox.AddChild(midVbox);

        // ── Правая: кнопки ───────────────────────────────────────────────────
        var btnVbox = new VBoxContainer();
        btnVbox.CustomMinimumSize   = new Vector2(200, 0);
        btnVbox.SizeFlagsHorizontal = Control.SizeFlags.Fill;
        btnVbox.Alignment           = BoxContainer.AlignmentMode.Center;
        btnVbox.AddThemeConstantOverride("separation", 6);

        _upgradeHouseButton = new Button { Text = "⬆ Улучшить дом\n[🪵2 + 🪨2]", Visible = false };
        _upgradeHouseButton.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        _upgradeHouseButton.AddThemeFontSizeOverride("font_size", 14);
        _upgradeHouseButton.Pressed += OnUpgradeHouse;

        _addFieldButton = new Button { Text = "+ Добавить поле", Visible = false };
        _addFieldButton.Pressed += OnAddField;
        _addFieldButton.AddThemeFontSizeOverride("font_size", 14);

        _demolishButton = new Button { Text = "🔨 Снести здание" };
        _demolishButton.AddThemeColorOverride("font_color", new Color(1f, 0.4f, 0.4f));
        _demolishButton.Pressed += OnDemolish;
        _demolishButton.AddThemeFontSizeOverride("font_size", 14);

        _closeButton = new Button { Text = "✕ Закрыть" };
        _closeButton.Pressed += Close;
        _closeButton.AddThemeFontSizeOverride("font_size", 14);

        btnVbox.AddChild(_upgradeHouseButton);
        btnVbox.AddChild(_addFieldButton);
        btnVbox.AddChild(_demolishButton);
        btnVbox.AddChild(_closeButton);
        hbox.AddChild(btnVbox);
    }

    // ─── События ──────────────────────────────────────────────────────────────

    private void OnBuildingClicked(Node node)
    {
        if (node is Building building)
        {
            _selected      = building;
            _panel.Visible = true;
            SetProcess(true);
            Refresh();
        }
    }

    private void Close()
    {
        _panel.Visible = false;
        _selected      = null;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_selected == null || !IsInstanceValid(_selected)) { Close(); return; }
        Refresh();
    }

    // ─── Обновление ───────────────────────────────────────────────────────────

    private void Refresh()
    {
        if (_selected?.Data == null) return;
        var data = _selected.Data;

        _nameLabel.Text = data.DisplayName;

        // ── Производство ──────────────────────────────────────────────────────
        var prod = _selected.GetNodeOrNull<ProductionCycleComponent>("ProductionCycleComponent");
        if (prod != null)
        {
            if (prod.IsProducing)
            {
                float perMin = (data.OutputAmount / data.ProductionCycleSec) * 60f;
                _statusLabel.Text = $"✓ Производит: {data.OutputResourceId}  +{perMin:F1}/мин";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
            }
            else
            {
                _statusLabel.Text = $"⚠ Стоит: {prod.IdleReason}";
                _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.2f));
            }
            _detailLabel.Text = $"Цикл: {data.ProductionCycleSec}с  |  Выход: {data.OutputAmount} ед.";
        }

        // ── Плавильня ─────────────────────────────────────────────────────────
        var smelt = _selected.GetNodeOrNull<SmelterComponent>("SmelterComponent");
        if (smelt != null)
        {
            if (smelt.IsProducing)
            {
                _statusLabel.Text = "✓ Плавит бронзу  +2/мин";
                _statusLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
            }
            else
            {
                _statusLabel.Text = $"⚠ Стоит: {smelt.IdleReason}";
                _statusLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.2f));
            }
            _detailLabel.Text = "🪵2 + 🟤2 + 🔘2 / мин → 🥉+2/мин";
        }

        // ── Инвентарь ─────────────────────────────────────────────────────────
        var inv = _selected.GetNodeOrNull<InventoryComponent>("InventoryComponent");
        if (inv != null && prod != null || inv != null && smelt != null)
        {
            var (resId, amt) = inv.FirstResource();
            string invText = resId != null
                ? $"Буфер: {resId} {amt}/{inv.MaxCapacity}"
                : $"Буфер: пусто 0/{inv.MaxCapacity}";
            _detailLabel.Text = (_detailLabel.Text.Length > 0 ? _detailLabel.Text + "\n" : "") + invText;
        }

        // ── Рабочие (глобальный пул) ──────────────────────────────────────────
        _workerLabel.Visible = false;
        if (data.WorkerCost > 0)
        {
            bool acquired = (prod != null && prod.HasWorkers) || (smelt != null && smelt.HasWorkers);
            string kind   = data.RequiresCitizens ? "граждан" : "иммигрантов";
            string icon   = data.RequiresCitizens ? "🏛" : "👤";
            if (acquired)
            {
                _workerLabel.Text = $"{icon} {kind}: {data.WorkerCost} заняты ✓";
                _workerLabel.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
            }
            else
            {
                int avail = data.RequiresCitizens
                    ? WorkerManager.Instance.AvailableCitizens
                    : WorkerManager.Instance.AvailableWorkers;
                _workerLabel.Text = $"{icon} Нет {kind}  (нужно {data.WorkerCost}, доступно {avail})";
                _workerLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.2f));
            }
            _workerLabel.Visible = true;
        }

        // ── Ферма ─────────────────────────────────────────────────────────────
        var farmField = _selected.GetNodeOrNull<FarmFieldComponent>("FarmFieldComponent");
        if (farmField != null)
        {
            _detailLabel.Text   += $"\nПоля: {farmField.FieldCount}/4";
            _addFieldButton.Visible  = true;
            _addFieldButton.Text     = $"+ Добавить поле  [{farmField.FieldCount}/4]";
            _addFieldButton.Disabled = !farmField.CanAddField();
        }
        else
        {
            _addFieldButton.Visible = false;
        }

        // ── Дом ───────────────────────────────────────────────────────────────
        var house = _selected.GetNodeOrNull<HouseLevelComponent>("HouseLevelComponent");
        if (house != null)
        {
            _statusLabel.RemoveThemeColorOverride("font_color");
            int consPct = Mathf.RoundToInt(house.ConsumeProgress * 100);
            string residentType = house.Level >= 2 ? "граждан" : "иммигрантов";
            (string icon, string text, string income) = house.Satisfaction switch
            {
                HouseLevelComponent.SatisfactionState.Full    => ("✓", "Сыты полностью",
                    house.Level >= 2 ? "+200 🪙/мин" : "+100 🪙/мин"),
                HouseLevelComponent.SatisfactionState.Partial => ("◑", "Частично сыты",
                    house.Level >= 2 ? "+100 🪙/мин" : "+50 🪙/мин"),
                _                                              => ("✗", "Голодают", "0 🪙/мин"),
            };
            _statusLabel.Text = $"{icon} {text}  |  Ур.{house.Level} ({residentType})";
            _statusLabel.AddThemeColorOverride("font_color", house.Satisfaction switch
            {
                HouseLevelComponent.SatisfactionState.Full    => new Color(0.4f, 1f, 0.4f),
                HouseLevelComponent.SatisfactionState.Partial => new Color(1f, 0.9f, 0.3f),
                _                                             => new Color(1f, 0.4f, 0.4f),
            });
            _detailLabel.Text = $"Потребляет: 1🐟 + 1🌾 / мин\nДоход: {income}  |  Цикл: {consPct}%";

            // Кнопка апгрейда
            bool canUp = house.CanUpgrade();
            _upgradeHouseButton.Visible  = house.Level == 1;
            _upgradeHouseButton.Disabled = !canUp;
            if (!canUp && house.Level == 1)
            {
                string hint = house.Satisfaction != HouseLevelComponent.SatisfactionState.Full
                    ? " (нужно Full)"
                    : !ResourceManager.Instance.CanAfford(0, 2, 2)
                        ? " (нет ресурсов)"
                        : "";
                _upgradeHouseButton.Text = $"⬆ Улучшить дом\n[🪵2 + 🪨2]{hint}";
            }
            else
            {
                _upgradeHouseButton.Text = "⬆ Улучшить дом\n[🪵2 + 🪨2]";
            }
        }
        else
        {
            _upgradeHouseButton.Visible = false;
            if (prod == null && smelt == null)
            {
                _statusLabel.RemoveThemeColorOverride("font_color");
                _statusLabel.Text = "";
                _detailLabel.Text = "";
            }
        }
    }

    // ─── Кнопки ───────────────────────────────────────────────────────────────

    private void OnUpgradeHouse()
    {
        var house = _selected?.GetNodeOrNull<HouseLevelComponent>("HouseLevelComponent");
        house?.Upgrade();
        Refresh();
    }

    private void OnAddField()
    {
        if (_selected == null) return;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeEntered, BuildingDatabase.Field);
    }

    private void OnDemolish()
    {
        if (_selected == null) return;
        var toDestroy = _selected;
        Close();
        BuildingRegistry.Instance.DemolishBuilding(toDestroy);
    }
}
