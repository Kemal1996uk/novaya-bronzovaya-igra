using Godot;

/// <summary>
/// Главное меню игры.
///
/// Вкладки:
///   [НОВАЯ ИГРА]             — процедурная карта (seed=42), запускает World.tscn
///   [РЕДАКТОР КАРТ]          — спрашивает размер карты, запускает MapEditor.tscn
///   [ИГРАТЬ НА СВОЕЙ КАРТЕ] — показывает список сохранённых карт (user://maps/)
/// </summary>
public partial class MainMenu : Control
{
    private VBoxContainer   _mapListBox;    // список карт (показывается по кнопке)
    private ScrollContainer _mapScroll;    // контейнер списка (скрыт/показан)
    private Label           _statusLabel;  // сообщение об ошибке / статус

    public override void _Ready()
    {
        // Сбрасываем флаги GameManager при возврате в меню
        GameManager.Instance.MapFilePath   = null;
        GameManager.Instance.EditorMapMode = false;

        AnchorRight  = 1f;
        AnchorBottom = 1f;

        BuildUI();
    }

    // ─── Построение UI ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Фон
        var bg = new ColorRect
        {
            Color             = new Color(0.12f, 0.09f, 0.05f),
            AnchorRight       = 1f,
            AnchorBottom      = 1f,
        };
        AddChild(bg);

        // Центральная колонка
        var center = new VBoxContainer();
        center.SetAnchorsPreset(LayoutPreset.Center);
        center.CustomMinimumSize = new Vector2(440, 0);
        center.AddThemeConstantOverride("separation", 20);
        AddChild(center);

        // Смещение вверх чуть
        center.Position = new Vector2(-220, -260);

        // Заголовок
        var title = new Label
        {
            Text                = "НОВАЯ\nБРОНЗОВАЯ ИГРА",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        title.AddThemeFontSizeOverride("font_size", 42);
        center.AddChild(title);

        center.AddChild(new HSeparator());

        // [НОВАЯ ИГРА]
        var btnNew = MakeMenuButton("⚔  НОВАЯ ИГРА", new Color(0.4f, 1f, 0.4f));
        btnNew.Pressed += StartNewGame;
        center.AddChild(btnNew);

        // [РЕДАКТОР КАРТ]
        var btnEditor = MakeMenuButton("🗺  РЕДАКТОР КАРТ", new Color(0.9f, 0.75f, 0.4f));
        btnEditor.Pressed += ShowEditorDialog;
        center.AddChild(btnEditor);

        // [ИГРАТЬ НА СВОЕЙ КАРТЕ]
        var btnCustom = MakeMenuButton("📂  ИГРАТЬ НА СВОЕЙ КАРТЕ", new Color(0.5f, 0.85f, 1.0f));
        btnCustom.Pressed += ToggleMapList;
        center.AddChild(btnCustom);

        // Список карт (скрыт по умолчанию)
        _mapScroll  = new ScrollContainer { CustomMinimumSize = new Vector2(0, 200) };
        _mapListBox = new VBoxContainer();
        _mapListBox.AddThemeConstantOverride("separation", 6);
        _mapScroll.AddChild(_mapListBox);
        _mapScroll.Visible = false;
        center.AddChild(_mapScroll);

        // Статус / подсказка
        _statusLabel = new Label
        {
            Text                = "Версия 0.1  —  Бронзовый Век",
            HorizontalAlignment = HorizontalAlignment.Center,
            Modulate            = new Color(0.5f, 0.5f, 0.5f),
        };
        _statusLabel.AddThemeFontSizeOverride("font_size", 14);
        center.AddChild(_statusLabel);
    }

    private static Button MakeMenuButton(string text, Color color)
    {
        var btn = new Button
        {
            Text              = text,
            Alignment         = HorizontalAlignment.Center,
            CustomMinimumSize = new Vector2(440, 72),
        };
        btn.AddThemeColorOverride("font_color", color);
        btn.AddThemeFontSizeOverride("font_size", 22);
        return btn;
    }

    // ─── Действия кнопок ─────────────────────────────────────────────────────

    private void StartNewGame()
    {
        GameManager.Instance.MapFilePath   = null;
        GameManager.Instance.EditorMapMode = false;
        GetTree().ChangeSceneToFile("res://World/World.tscn");
    }

    private void ShowEditorDialog()
    {
        // Диалог выбора размера карты
        var dlg = new Window
        {
            Title       = "Размер карты для редактора",
            Size        = new Vector2I(380, 220),
            Unresizable = true,
        };

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        vbox.SetAnchorsPreset(LayoutPreset.CenterTop);
        vbox.Position = new Vector2(20, 20);
        vbox.AddThemeConstantOverride("separation", 14);
        dlg.AddChild(vbox);

        var lbl = new Label { Text = "Введите размер карты (тайлы):" };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(lbl);

        // Ширина
        var rowW = new HBoxContainer();
        var lblW = new Label { Text = "Ширина:", CustomMinimumSize = new Vector2(100, 0) };
        var spinW = new SpinBox { MinValue = 20, MaxValue = 300, Value = 80, Step = 10 };
        rowW.AddChild(lblW);
        rowW.AddChild(spinW);
        vbox.AddChild(rowW);

        // Высота
        var rowH = new HBoxContainer();
        var lblH = new Label { Text = "Высота:", CustomMinimumSize = new Vector2(100, 0) };
        var spinH = new SpinBox { MinValue = 20, MaxValue = 300, Value = 80, Step = 10 };
        rowH.AddChild(lblH);
        rowH.AddChild(spinH);
        vbox.AddChild(rowH);

        var btnStart = new Button
        {
            Text              = "Открыть редактор",
            CustomMinimumSize = new Vector2(0, 48),
        };
        btnStart.AddThemeFontSizeOverride("font_size", 18);
        btnStart.Pressed += () =>
        {
            GameManager.Instance.EditorMapMode  = true;
            GameManager.Instance.EditorMapWidth  = (int)spinW.Value;
            GameManager.Instance.EditorMapHeight = (int)spinH.Value;
            dlg.QueueFree();
            GetTree().ChangeSceneToFile("res://World/MapEditor.tscn");
        };
        vbox.AddChild(btnStart);

        AddChild(dlg);
        dlg.PopupCentered();
    }

    private void ToggleMapList()
    {
        if (_mapScroll == null) return;
        _mapScroll.Visible = !_mapScroll.Visible;
        if (_mapScroll.Visible) RefreshMapList();
    }

    private void RefreshMapList()
    {
        // Найти _mapListBox
        if (_mapListBox == null) return;
        foreach (Node child in _mapListBox.GetChildren())
            child.QueueFree();

        if (!DirAccess.DirExistsAbsolute("user://maps"))
        {
            var empty = new Label { Text = "Сохранённых карт нет.\nСоздайте карту в редакторе!" };
            empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _mapListBox.AddChild(empty);
            return;
        }

        using var dir = DirAccess.Open("user://maps");
        if (dir == null) return;

        dir.ListDirBegin();
        bool any = false;
        string fileName = dir.GetNext();
        while (fileName != "")
        {
            if (!dir.CurrentIsDir() && fileName.EndsWith(".json"))
            {
                string path     = $"user://maps/{fileName}";
                string mapName  = fileName.Replace(".json", "");
                var btn = new Button
                {
                    Text              = $"🗺 {mapName}",
                    Alignment         = HorizontalAlignment.Left,
                    CustomMinimumSize = new Vector2(0, 52),
                };
                btn.AddThemeFontSizeOverride("font_size", 18);
                var capturedPath = path;
                btn.Pressed += () => LaunchCustomMap(capturedPath);
                _mapListBox.AddChild(btn);
                any = true;
            }
            fileName = dir.GetNext();
        }
        dir.ListDirEnd();

        if (!any)
        {
            var empty = new Label { Text = "Сохранённых карт нет." };
            empty.AddThemeColorOverride("font_color", new Color(0.6f, 0.6f, 0.6f));
            _mapListBox.AddChild(empty);
        }
    }

    private void LaunchCustomMap(string path)
    {
        GameManager.Instance.MapFilePath   = path;
        GameManager.Instance.EditorMapMode = false;
        GetTree().ChangeSceneToFile("res://World/World.tscn");
    }
}
