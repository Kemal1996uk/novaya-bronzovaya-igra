using Godot;

/// <summary>
/// Камера изометрического мира.
///
/// Управление:
///   Средняя кнопка мыши (зажать + тянуть) — панорамирование
///   Колесо мыши                            — зум
///   W/A/S/D или стрелки                    — панорамирование (клавиатура)
///   Мышь к краю экрана                     — автоскролл (edge scroll)
/// </summary>
public partial class WorldCamera : Camera2D
{
    [Export] public float PanSpeed       { get; set; } = 600f;
    [Export] public float ZoomStep       { get; set; } = 0.12f;
    [Export] public float MinZoom        { get; set; } = 0.25f;
    [Export] public float MaxZoom        { get; set; } = 2.5f;
    [Export] public float EdgeMargin     { get; set; } = 24f;
    [Export] public bool  EdgeScrollOn   { get; set; } = true;

    private bool    _isDragging;
    private Vector2 _dragStartMouse;
    private Vector2 _dragStartPos;
    private float   _targetZoom = 1.0f;

    public override void _Ready()
    {
        _targetZoom = Zoom.X;
    }

    public override void _Process(double delta)
    {
        // Пауза — камера всё равно должна двигаться
        if (GameManager.Instance.IsPaused) return;
        HandleKeyboardPan((float)delta);
        HandleEdgeScroll((float)delta);
        SmoothZoom((float)delta);
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventMouseButton btn)
        {
            switch (btn.ButtonIndex)
            {
                // Зажать среднюю кнопку — начать перетаскивание
                case MouseButton.Middle:
                    _isDragging   = btn.Pressed;
                    _dragStartMouse = GetViewport().GetMousePosition();
                    _dragStartPos   = Position;
                    GetViewport().SetInputAsHandled();
                    break;

                // Колесо вверх — приблизить
                case MouseButton.WheelUp when btn.Pressed:
                    _targetZoom = Mathf.Clamp(_targetZoom + ZoomStep, MinZoom, MaxZoom);
                    GetViewport().SetInputAsHandled();
                    break;

                // Колесо вниз — отдалить
                case MouseButton.WheelDown when btn.Pressed:
                    _targetZoom = Mathf.Clamp(_targetZoom - ZoomStep, MinZoom, MaxZoom);
                    GetViewport().SetInputAsHandled();
                    break;
            }
        }

        // Перемещение мыши при зажатой средней кнопке
        if (@event is InputEventMouseMotion motion && _isDragging)
        {
            var delta2 = GetViewport().GetMousePosition() - _dragStartMouse;
            Position = _dragStartPos - delta2 / Zoom.X;
            GetViewport().SetInputAsHandled();
        }
    }

    // ─── Приватные методы ──────────────────────────────────────────────────────

    private void HandleKeyboardPan(float delta)
    {
        var move = Vector2.Zero;

        if (Input.IsKeyPressed(Key.A) || Input.IsKeyPressed(Key.Left))  move.X -= 1;
        if (Input.IsKeyPressed(Key.D) || Input.IsKeyPressed(Key.Right)) move.X += 1;
        if (Input.IsKeyPressed(Key.W) || Input.IsKeyPressed(Key.Up))    move.Y -= 1;
        if (Input.IsKeyPressed(Key.S) || Input.IsKeyPressed(Key.Down))  move.Y += 1;

        if (move != Vector2.Zero)
            Position += move.Normalized() * PanSpeed * delta / Zoom.X;
    }

    private void HandleEdgeScroll(float delta)
    {
        if (!EdgeScrollOn) return;

        var mouse    = GetViewport().GetMousePosition();
        var viewSize = GetViewportRect().Size;
        var move     = Vector2.Zero;

        if (mouse.X < EdgeMargin)              move.X -= 1;
        if (mouse.X > viewSize.X - EdgeMargin) move.X += 1;
        if (mouse.Y < EdgeMargin)              move.Y -= 1;
        if (mouse.Y > viewSize.Y - EdgeMargin) move.Y += 1;

        if (move != Vector2.Zero)
            Position += move.Normalized() * PanSpeed * delta / Zoom.X;
    }

    /// <summary>Плавное сглаживание зума (lerp к целевому значению).</summary>
    private void SmoothZoom(float delta)
    {
        var current = Zoom.X;
        var smooth  = Mathf.Lerp(current, _targetZoom, delta * 12f);
        Zoom = new Vector2(smooth, smooth);
    }
}
