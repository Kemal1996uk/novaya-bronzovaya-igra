using Godot;

/// <summary>
/// «Призрак» здания: следует за мышью и показывает, куда будет поставлено здание.
/// Зелёный = можно строить, Красный = нельзя (занято / за границей карты).
///
/// Активируется через EventBus.PlacementModeEntered(data).
/// Левый клик = построить, ПКМ / Escape = отмена.
///
/// Добавить в World.tscn как дочернюю ноду Node2D с этим скриптом.
/// В World._Ready() вызвать: _ghost.SetTileMap(_tileMap);
/// </summary>
public partial class BuildPlacementGhost : Node2D
{
    private BuildingData _currentData;
    private IsoTileMap   _tileMap;
    private Vector2I     _currentTile  = new(-9999, -9999);
    private bool         _isValid;
    private Vector2[]    _tileOffsets;   // пиксельные офсеты тайлов footprint-а
    private Sprite2D     _ghostSprite;   // прозрачный спрайт-призрак (если есть текстура)

    // Шаблон ромба для тайла 128×64
    private static readonly Vector2[] Diamond =
    {
        new(  0, -32),
        new( 64,   0),
        new(  0,  32),
        new(-64,   0),
    };

    // ─── Готовность ───────────────────────────────────────────────────────────

    public override void _Ready()
    {
        Visible = false;
        EventBus.Instance.PlacementModeEntered += OnPlacementModeEntered;
        EventBus.Instance.PlacementModeExited  += OnPlacementModeExited;
    }

    public override void _ExitTree()
    {
        EventBus.Instance.PlacementModeEntered -= OnPlacementModeEntered;
        EventBus.Instance.PlacementModeExited  -= OnPlacementModeExited;
    }

    public void SetTileMap(IsoTileMap tileMap) => _tileMap = tileMap;

    // ─── Обновление позиции ───────────────────────────────────────────────────

    public override void _Process(double delta)
    {
        if (!Visible || _currentData == null || _tileMap == null) return;

        var tileCoord = _tileMap.GlobalToTile(GetGlobalMousePosition());
        if (tileCoord == _currentTile) return;

        _currentTile   = tileCoord;
        _isValid       = BuildingRegistry.Instance.CanPlace(_currentData, _currentTile);
        GlobalPosition = _tileMap.GetTileWorldCenter(_currentTile);

        // Обновить цвет спрайта-призрака
        if (_ghostSprite != null)
            _ghostSprite.Modulate = _isValid
                ? new Color(0.7f, 1.0f, 0.7f, 0.65f)   // зелёный оттенок
                : new Color(1.0f, 0.4f, 0.4f, 0.65f);  // красный оттенок

        QueueRedraw();
    }

    // ─── Отрисовка ────────────────────────────────────────────────────────────

    public override void _Draw()
    {
        if (_currentData == null || _tileOffsets == null) return;

        Color fill   = _isValid ? new Color(0.25f, 0.88f, 0.25f, 0.35f) : new Color(0.90f, 0.20f, 0.20f, 0.35f);
        Color border = _isValid ? new Color(0.10f, 0.70f, 0.10f, 1.00f) : new Color(0.75f, 0.10f, 0.10f, 1.00f);

        foreach (var offset in _tileOffsets)
        {
            var pts = new Vector2[4];
            for (int i = 0; i < 4; i++)
                pts[i] = Diamond[i] + offset;

            // Если есть спрайт-призрак — только контур тайлов (без заливки)
            if (_ghostSprite == null)
                DrawColoredPolygon(pts, fill);

            DrawPolyline(new[] { pts[0], pts[1], pts[2], pts[3], pts[0] }, border, 2f);
        }
    }

    // ─── Ввод ────────────────────────────────────────────────────────────────

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!Visible || _currentData == null) return;

        if (@event is InputEventMouseButton btn && btn.Pressed)
        {
            if (btn.ButtonIndex == MouseButton.Left && _isValid)
            {
                BuildingRegistry.Instance.PlaceBuilding(_currentData, _currentTile);
                GetViewport().SetInputAsHandled();
                return;
            }
            if (btn.ButtonIndex == MouseButton.Right)
            {
                EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeExited);
                GetViewport().SetInputAsHandled();
                return;
            }
        }

        if (@event is InputEventKey key && key.Pressed && !key.Echo &&
            key.Keycode == Key.Escape)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.PlacementModeExited);
            GetViewport().SetInputAsHandled();
        }
    }

    // ─── Обработчики событий ─────────────────────────────────────────────────

    private void OnPlacementModeEntered(BuildingData data)
    {
        _currentData = data;
        _currentTile = new(-9999, -9999);
        ComputeTileOffsets();

        // Создать спрайт-призрак если у здания есть текстура
        _ghostSprite?.QueueFree();
        _ghostSprite = null;

        if (!string.IsNullOrEmpty(data.SpritePath))
        {
            var tex = GD.Load<Texture2D>(data.SpritePath);
            if (tex != null)
            {
                _ghostSprite         = new Sprite2D();
                _ghostSprite.Texture = tex;
                _ghostSprite.Scale   = Vector2.One * data.SpriteScale;
                _ghostSprite.Offset  = data.SpriteOffset;
                _ghostSprite.Modulate = new Color(0.7f, 1.0f, 0.7f, 0.65f);
                AddChild(_ghostSprite);
            }
        }

        Visible = true;
    }

    private void OnPlacementModeExited()
    {
        _ghostSprite?.QueueFree();
        _ghostSprite = null;
        _currentData = null;
        Visible      = false;
        QueueRedraw();
    }

    // ─── Вспомогательные ─────────────────────────────────────────────────────

    /// <summary>
    /// Вычисляет офсеты тайлов относительно якоря (0,0) через MapToLocal.
    /// Вызывается один раз при смене типа здания.
    /// </summary>
    private void ComputeTileOffsets()
    {
        if (_currentData == null || _tileMap == null) return;

        int count    = _currentData.FootprintSize.X * _currentData.FootprintSize.Y;
        _tileOffsets = new Vector2[count];

        var anchorLocal = _tileMap.MapToLocal(Vector2I.Zero);
        int i = 0;

        for (int dx = 0; dx < _currentData.FootprintSize.X; dx++)
            for (int dy = 0; dy < _currentData.FootprintSize.Y; dy++)
            {
                var tileLocal     = _tileMap.MapToLocal(new Vector2I(dx, dy));
                _tileOffsets[i++] = tileLocal - anchorLocal;
            }
    }
}
