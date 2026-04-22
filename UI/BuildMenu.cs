using Godot;

/// <summary>
/// Меню строительства — правая панель.
/// Кнопки зданий + кнопки «Дорога» и «Снос».
/// Показывает стоимость (Драхмы/Дерево/Камень) и серит кнопку если не хватает.
/// </summary>
public partial class BuildMenu : CanvasLayer
{
	private bool _inPlacementMode;
	private bool _inRoadMode;
	private bool _inDemolishMode;
	private bool _inCanalMode;

	private readonly System.Collections.Generic.List<(Button btn, BuildingData data)> _buttons = new();
	private Button _roadButton;
	private Button _canalButton;
	private Button _demolishButton;

	public override void _Ready()
	{
		EventBus.Instance.PlacementModeEntered += _ => { _inPlacementMode = true;  _inRoadMode = false; _inDemolishMode = false; _inCanalMode = false; UpdateModeButtons(); };
		EventBus.Instance.PlacementModeExited  += () => { _inPlacementMode = false; UpdateModeButtons(); };
		EventBus.Instance.RoadModeEntered      += () => { _inRoadMode = true; _inPlacementMode = false; _inDemolishMode = false; _inCanalMode = false; UpdateModeButtons(); };
		EventBus.Instance.RoadModeExited       += () => { _inRoadMode = false; UpdateModeButtons(); };
		EventBus.Instance.DemolishModeEntered  += () => { _inDemolishMode = true; _inPlacementMode = false; _inRoadMode = false; _inCanalMode = false; UpdateModeButtons(); };
		EventBus.Instance.DemolishModeExited   += () => { _inDemolishMode = false; UpdateModeButtons(); };
		EventBus.Instance.CanalModeEntered     += () => { _inCanalMode = true; _inPlacementMode = false; _inRoadMode = false; _inDemolishMode = false; UpdateModeButtons(); };
		EventBus.Instance.CanalModeExited      += () => { _inCanalMode = false; UpdateModeButtons(); };
		EventBus.Instance.StockpileChanged     += UpdateButtonStates;

		BuildUI();
	}

	private void BuildUI()
	{
		var panel = new PanelContainer();
		panel.CustomMinimumSize = new Vector2(340, 0);
		panel.AnchorLeft   = 1.0f; panel.AnchorRight  = 1.0f;
		panel.AnchorTop    = 0.0f; panel.AnchorBottom = 0.0f;
		panel.OffsetLeft   = -352f; panel.OffsetTop    = 68f;
		panel.OffsetRight  = -8f;   panel.OffsetBottom = 780f;
		AddChild(panel);

		var scroll = new ScrollContainer();
		scroll.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
		panel.AddChild(scroll);

		var vbox = new VBoxContainer();
		vbox.CustomMinimumSize = new Vector2(316, 0);
		vbox.AddThemeConstantOverride("separation", 6);
		scroll.AddChild(vbox);

		// Заголовок
		var title = new Label { Text = "ПОСТРОЙКИ", HorizontalAlignment = HorizontalAlignment.Center };
		title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.4f));
		title.AddThemeFontSizeOverride("font_size", 20);
		vbox.AddChild(title);
		vbox.AddChild(new HSeparator());

		// Кнопки зданий
		foreach (var data in BuildingDatabase.All)
		{
			var btn = MakeBuildingButton(data);
			vbox.AddChild(btn);
			_buttons.Add((btn, data));
		}

		vbox.AddChild(new HSeparator());

		// Кнопка «Дорога»
		_roadButton = new Button
		{
			Text      = "🛤  Дорога\n[100 Д / тайл]",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0, 72),
		};
		_roadButton.AddThemeColorOverride("font_color", new Color(0.9f, 0.75f, 0.4f));
		_roadButton.AddThemeFontSizeOverride("font_size", 18);
		_roadButton.Pressed += ToggleRoadMode;
		vbox.AddChild(_roadButton);

		// Кнопка «Канал»
		_canalButton = new Button
		{
			Text      = "💧  Канал\n[100 Д / тайл]",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0, 72),
		};
		_canalButton.AddThemeColorOverride("font_color", new Color(0.3f, 0.65f, 1.0f));
		_canalButton.AddThemeFontSizeOverride("font_size", 18);
		_canalButton.Pressed += ToggleCanalMode;
		vbox.AddChild(_canalButton);

		// Кнопка «Снос»
		_demolishButton = new Button
		{
			Text      = "🔨  Снос здания\n[возврат 50% Д]",
			Alignment = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0, 72),
		};
		_demolishButton.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.35f));
		_demolishButton.AddThemeFontSizeOverride("font_size", 18);
		_demolishButton.Pressed += ToggleDemolishMode;
		vbox.AddChild(_demolishButton);

		vbox.AddChild(new HSeparator());

		var hint = new Label { Text = "ПКМ / ESC — отмена", HorizontalAlignment = HorizontalAlignment.Center };
		hint.AddThemeColorOverride("font_color", new Color(0.55f, 0.55f, 0.55f));
		hint.AddThemeFontSizeOverride("font_size", 15);
		vbox.AddChild(hint);

		UpdateButtonStates();
		UpdateModeButtons();
	}

	private Button MakeBuildingButton(BuildingData data)
	{
		string cost = BuildCostText(data);
		var btn = new Button
		{
			Text        = $"{data.DisplayName}  {data.FootprintSize.X}×{data.FootprintSize.Y}\n{cost}",
			TooltipText = data.Description,
			Alignment   = HorizontalAlignment.Left,
			CustomMinimumSize = new Vector2(0, 72),
		};
		btn.AddThemeColorOverride("font_color", data.PlaceholderColor.Lightened(0.3f));
		btn.AddThemeFontSizeOverride("font_size", 18);

		var captured = data;
		btn.Pressed += () =>
		{
			if (!_inPlacementMode && !_inRoadMode && !_inDemolishMode)
				EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeEntered, captured);
		};
		return btn;
	}

	private static string BuildCostText(BuildingData data)
	{
		var parts = new System.Collections.Generic.List<string>();
		if (data.GoldCost  > 0) parts.Add($"{data.GoldCost} Д");
		if (data.WoodCost  > 0) parts.Add($"{data.WoodCost} 🪵");
		if (data.StoneCost > 0) parts.Add($"{data.StoneCost} 🪨");
		return parts.Count > 0 ? string.Join(" + ", parts) : "бесплатно";
	}

	private void UpdateButtonStates()
	{
		var rm = ResourceManager.Instance;
		if (rm == null) return;

		foreach (var (btn, data) in _buttons)
		{
			bool canAfford = rm.CanAfford(data.GoldCost, data.WoodCost, data.StoneCost);
			btn.Modulate = canAfford ? Colors.White : new Color(0.55f, 0.55f, 0.55f, 1f);
		}
	}

	private void UpdateModeButtons()
	{
		if (_roadButton == null || _demolishButton == null || _canalButton == null) return;
		_roadButton.Modulate     = _inRoadMode    ? new Color(0.4f, 1f, 0.4f)    : Colors.White;
		_canalButton.Modulate    = _inCanalMode   ? new Color(0.4f, 0.8f, 1.0f)  : Colors.White;
		_demolishButton.Modulate = _inDemolishMode ? new Color(1f, 0.4f, 0.4f)   : Colors.White;
	}

	private void ToggleRoadMode()
	{
		if (_inRoadMode)
			EventBus.Instance.EmitSignal(EventBus.SignalName.RoadModeExited);
		else
		{
			if (_inDemolishMode) EventBus.Instance.EmitSignal(EventBus.SignalName.DemolishModeExited);
			if (_inCanalMode)    EventBus.Instance.EmitSignal(EventBus.SignalName.CanalModeExited);
			EventBus.Instance.EmitSignal(EventBus.SignalName.RoadModeEntered);
		}
	}

	private void ToggleCanalMode()
	{
		if (_inCanalMode)
			EventBus.Instance.EmitSignal(EventBus.SignalName.CanalModeExited);
		else
		{
			if (_inRoadMode)    EventBus.Instance.EmitSignal(EventBus.SignalName.RoadModeExited);
			if (_inDemolishMode) EventBus.Instance.EmitSignal(EventBus.SignalName.DemolishModeExited);
			EventBus.Instance.EmitSignal(EventBus.SignalName.CanalModeEntered);
		}
	}

	private void ToggleDemolishMode()
	{
		if (_inDemolishMode)
			EventBus.Instance.EmitSignal(EventBus.SignalName.DemolishModeExited);
		else
		{
			if (_inRoadMode)   EventBus.Instance.EmitSignal(EventBus.SignalName.RoadModeExited);
			if (_inCanalMode)  EventBus.Instance.EmitSignal(EventBus.SignalName.CanalModeExited);
			EventBus.Instance.EmitSignal(EventBus.SignalName.DemolishModeEntered);
		}
	}
}
