using Godot;

/// <summary>
/// Статические вспомогательные методы для конвертации координат тайловой карты.
///
/// В изометрической карте есть три системы координат:
///   1. Tile-координаты  — Vector2I(col, row), целые числа, логическая сетка
///   2. Local-координаты — Vector2, пикселей, относительно TileMapLayer
///   3. Global-координаты — Vector2, пикселей, мировое пространство
///
/// TileMapLayer.MapToLocal / LocalToMap — базовые методы Godot.
/// Этот класс добавляет удобные обёртки и расчёт границ.
/// </summary>
public static class TileCoordHelper
{
    /// <summary>Tile-координата → центр тайла в local-пространстве TileMapLayer.</summary>
    public static Vector2 TileToLocal(TileMapLayer tileMap, Vector2I tileCoord)
        => tileMap.MapToLocal(tileCoord);

    /// <summary>Local-позиция → ближайшая tile-координата.</summary>
    public static Vector2I LocalToTile(TileMapLayer tileMap, Vector2 localPos)
        => tileMap.LocalToMap(localPos);

    /// <summary>
    /// Глобальная позиция мыши → tile-координата.
    /// Используй GetGlobalMousePosition() из Node2D и передавай сюда.
    /// </summary>
    public static Vector2I GlobalToTile(TileMapLayer tileMap, Vector2 globalPos)
    {
        var localPos = tileMap.ToLocal(globalPos);
        return tileMap.LocalToMap(localPos);
    }

    /// <summary>Tile-координата → центр тайла в глобальном пространстве.</summary>
    public static Vector2 TileToGlobal(TileMapLayer tileMap, Vector2I tileCoord)
    {
        var localPos = tileMap.MapToLocal(tileCoord);
        return tileMap.ToGlobal(localPos);
    }

    /// <summary>Проверить, что тайл находится в границах карты.</summary>
    public static bool IsInBounds(Vector2I tileCoord, Vector2I mapSize)
        => tileCoord.X >= 0 && tileCoord.Y >= 0
        && tileCoord.X < mapSize.X && tileCoord.Y < mapSize.Y;
}
