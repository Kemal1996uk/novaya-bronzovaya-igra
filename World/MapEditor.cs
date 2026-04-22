using Godot;
using System.Collections.Generic;

/// <summary>
/// Редактор карт.
/// Позволяет вручную расставлять тайлы и сохранять карту в user://maps/*.json.
///
/// Управление:
///   ЛКМ / держать — рисовать выбранный тайл
///   ПКМ           — стереть (Grass)
///   WASD / стрелки — камера
///   Колёсико       — зум
///   Save           — сохранить карту (диалог имени)
///   Back           — в главное меню
/// </summary>
public partial class MapEditor : Node2D
{
    private IsoTileMap _tileMap;
    private Camera2D   _camera;

    private TileType _paintType = TileType.Grass;
    private bool     _isPainting;
    private bool     _isErasing;

    private const float CamSpeed  = 400f;
    private const float ZoomMin   = 0.15f;
    private const float ZoomMax   = 2.0f;

    // Кнопки тайлов (для подсветки выбранного)
    private readonly Dictionary<TileType, Button> _tileButtons = new();

    // ─── Инициализация ────────────────────────────────────────────────────────

    public override void _Ready()
    {
        // Создаём тайл-карту (IsoTileMap._Ready читает GameManager.EditorMapMode)
        _tileMap      = new IsoTileMap { Name = "TileMap_Ground" };
        _tileMap.ZIndex = -1;
        AddChild(_tileMap);

        // Камера
        _camera          = new Camera2D { Name = "EditorCamera" };
        _camera.Zoom     = new Vector2(0.5f, 0.5f);
        AddChild(_camera);
        _camera.MakeCurrent();

        // Центрируем на карту
        var center = new Vector2I(_tileMap.MapSize.X / 2, _tileMap.MapSize.Y / 2);
        _camera.Position = _tileMap.GetTileWorldCenter(center);

        BuildUI();
    }

    // ─── Построение UI ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        var ui = new CanvasLayer { Name = "EditorUI" };
        AddChild(ui);

        // ── Верхняя панель ───────────────────────────────────────────────────
        var topPanel = new PanelContainer();
        topPanel.AnchorLeft   = 0f; topPanel.AnchorRight  = 1f;
        topPanel.AnchorTop    = 0f; topPanel.AnchorBottom = 0f;
        topPanel.OffsetBottom = 60f;
        ui.AddChild(topPanel);

        var topHBox = new HBoxContainer();
        topHBox.AddThemeConstantOverride("separation", 12);
        topPanel.AddChild(topHBox);

        // Заголовок
        var titleLbl = new Label
        {
            Text = $"РЕДАКТОР КАРТ  {_tileMap.MapSize.X}×{_tileMap.MapSize.Y}",
            SizeFlagsHorizontal = Control.SizeFlags.Expand | Control.SizeFlags.Fill,
            VerticalAlignment = VerticalAlignment.Center,
        };
        titleLbl.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        titleLbl.AddThemeFontSizeOverride("font_size", 20);
        topHBox.AddChild(titleLbl);

        // Кнопка Сохранить
        var btnSave = new Button
        {
            Text              = "💾 Сохранить",
            CustomMinimumSize = new Vector2(160, 0),
        };
        btnSave.AddThemeFontSizeOverride("font_size", 18);
        btnSave.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        btnSave.Pressed += ShowSaveDialog;
        topHBox.AddChild(btnSave);

        // Кнопка Назад
        var btnBack = new Button
        {
            Text              = "← Меню",
            CustomMinimumSize = new Vector2(130, 0),
        };
        btnBack.AddThemeFontSizeOverride("font_size", 18);
        btnBack.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.4f));
        btnBack.Pressed += GoToMenu;
        topHBox.AddChild(btnBack);

        // ── Левая панель (выбор тайлов) ──────────────────────────────────────
        var leftPanel = new PanelContainer();
        leftPanel.AnchorLeft   = 0f; leftPanel.AnchorRight  = 0f;
        leftPanel.AnchorTop    = 0f; leftPanel.AnchorBottom = 1f;
        leftPanel.OffsetLeft   = 0f; leftPanel.OffsetRight  = 180f;
        leftPanel.OffsetTop    = 62f;
        ui.AddChild(leftPanel);

        var leftScroll = new ScrollContainer();
        leftScroll.SizeFlagsVertical = Control.SizeFlags.Expand | Control.SizeFlags.Fill;
        leftPanel.AddChild(leftScroll);

        var leftVBox = new VBoxContainer();
        leftVBox.CustomMinimumSize = new Vector2(156, 0);
        leftVBox.AddThemeConstantOverride("separation", 4);
        leftScroll.AddChild(leftVBox);

        var lbl = new Label { Text = "ТАЙЛЫ", HorizontalAlignment = HorizontalAlignment.Center };
        lbl.AddThemeColorOverride("font_color", new Color(1f, 0.85f, 0.3f));
        lbl.AddThemeFontSizeOverride("font_size", 16);
        leftVBox.AddChild(lbl);
        leftVBox.AddChild(new HSeparator());

        // Тайлы для рисования (без Road и Canal — их через инструменты)
        var tileDefs = new (TileType type, string label, Color color)[]
        {
            (TileType.Grass,     "🌿 Трава",       new Color(0.35f, 0.7f, 0.15f)),
            (TileType.Sand,      "🏖 Песок",        new Color(0.85f, 0.75f, 0.45f)),
            (TileType.Water,     "🌊 Вода",         new Color(0.2f,  0.55f, 0.9f)),
            (TileType.Forest,    "🌲 Лес",          new Color(0.15f, 0.45f, 0.08f)),
            (TileType.Rock,      "🪨 Скалы",        new Color(0.55f, 0.55f, 0.55f)),
            (TileType.CopperOre, "🟠 Медь",         new Color(0.8f,  0.45f, 0.15f)),
            (TileType.TinOre,    "⚪ Олово",        new Color(0.6f,  0.6f,  0.75f)),
            (TileType.Canal,     "💧 Канал",        new Color(0.25f, 0.6f,  1.0f)),
        };

        foreach (var (type, label, color) in tileDefs)
        {
            var btn = new Button
            {
                Text              = label,
                Alignment         = HorizontalAlignment.Left,
                CustomMinimumSize = new Vector2(0, 48),
                ToggleMode        = true,
            };
            btn.AddThemeColorOverride("font_color", color);
            btn.AddThemeFontSizeOverride("font_size", 15);
            var capturedType = type;
            btn.Pressed += () => SelectTile(capturedType);
            leftVBox.AddChild(btn);
            _tileButtons[type] = btn;
        }

        // Подсветить Grass по умолчанию
        UpdateTileButtonHighlight();

        // Подсказка управления
        leftVBox.AddChild(new HSeparator());
        var hint = new Label
        {
            Text             = "WASD — камера\nКолёсо — зум\nЛКМ — рисовать\nПКМ — стереть",
            AutowrapMode     = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(0, 80),
        };
        hint.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.5f));
        hint.AddThemeFontSizeOverride("font_size", 13);
        leftVBox.AddChild(hint);
    }

    // ─── Выбор тайла ─────────────────────────────────────────────────────────

    private void SelectTile(TileType type)
    {
        _paintType = type;
        UpdateTileButtonHighlight();
    }

    private void UpdateTileButtonHighlight()
    {
        foreach (var (type, btn) in _tileButtons)
            btn.ButtonPressed = (type == _paintType);
    }

    // ─── Ввод ─────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        // Рисование мышью
        if (@event is InputEventMouseButton mb)
        {
            if (mb.ButtonIndex == MouseButton.Left)
            {
                _isPainting = mb.Pressed;
                _isErasing  = false;
                if (_isPainting) PaintAtMouse();
            }
            else if (mb.ButtonIndex == MouseButton.Right)
            {
                _isErasing  = mb.Pressed;
                _isPainting = false;
                if (_isErasing) EraseAtMouse();
            }
            // Зум колёсиком
            else if (mb.ButtonIndex == MouseButton.WheelUp && mb.Pressed)
            {
                float z = Mathf.Clamp(_camera.Zoom.X * 1.1f, ZoomMin, ZoomMax);
                _camera.Zoom = new Vector2(z, z);
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown && mb.Pressed)
            {
                float z = Mathf.Clamp(_camera.Zoom.X * 0.9f, ZoomMin, ZoomMax);
                _camera.Zoom = new Vector2(z, z);
            }
        }

        if (@event is InputEventMouseMotion)
        {
            if (_isPainting) PaintAtMouse();
            if (_isErasing)  EraseAtMouse();
        }
    }

    private void PaintAtMouse()
    {
        var tile = _tileMap.GlobalToTile(GetGlobalMousePosition());
        if (_tileMap.IsValidTile(tile))
            _tileMap.PaintTile(tile, _paintType);
    }

    private void EraseAtMouse()
    {
        var tile = _tileMap.GlobalToTile(GetGlobalMousePosition());
        if (_tileMap.IsValidTile(tile))
            _tileMap.PaintTile(tile, TileType.Grass);
    }

    // ─── Обновление (камера) ──────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        float dt    = (float)delta;
        float speed = CamSpeed / _camera.Zoom.X;
        var   move  = Vector2.Zero;

        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  move.Y += 1;
        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;

        if (move != Vector2.Zero)
            _camera.Position += move.Normalized() * speed * dt;
    }

    // ─── Диалог сохранения ────────────────────────────────────────────────────

    private void ShowSaveDialog()
    {
        var dlg = new Window
        {
            Title       = "Сохранить карту",
            Size        = new Vector2I(380, 180),
            Unresizable = true,
        };

        var vbox = new VBoxContainer { CustomMinimumSize = new Vector2(340, 0) };
        vbox.Position = new Vector2(20, 20);
        vbox.AddThemeConstantOverride("separation", 14);
        dlg.AddChild(vbox);

        var lbl = new Label { Text = "Введите имя карты:" };
        lbl.AddThemeFontSizeOverride("font_size", 16);
        vbox.AddChild(lbl);

        var lineEdit = new LineEdit { Text = "MyMap", CustomMinimumSize = new Vector2(340, 44) };
        lineEdit.AddThemeFontSizeOverride("font_size", 18);
        vbox.AddChild(lineEdit);

        var btnSave = new Button
        {
            Text              = "Сохранить",
            CustomMinimumSize = new Vector2(0, 48),
        };
        btnSave.AddThemeFontSizeOverride("font_size", 18);
        btnSave.AddThemeColorOverride("font_color", new Color(0.4f, 1f, 0.4f));
        btnSave.Pressed += () =>
        {
            string name = lineEdit.Text.Trim();
            if (string.IsNullOrEmpty(name)) name = "MyMap";
            // Убрать недопустимые символы
            foreach (char c in System.IO.Path.GetInvalidFileNameChars())
                name = name.Replace(c.ToString(), "");
            if (string.IsNullOrEmpty(name)) name = "MyMap";

            string path = $"user://maps/{name}.json";
            _tileMap.SaveMapToFile(path);
            GD.Print($"[MapEditor] Карта сохранена: {path}");
            dlg.QueueFree();
        };
        vbox.AddChild(btnSave);

        AddChild(dlg);
        dlg.PopupCentered();
    }

    // ─── Переход в меню ───────────────────────────────────────────────────────

    private void GoToMenu()
    {
        GameManager.Instance.EditorMapMode = false;
        GetTree().ChangeSceneToFile("res://UI/MainMenu.tscn");
    }
}
