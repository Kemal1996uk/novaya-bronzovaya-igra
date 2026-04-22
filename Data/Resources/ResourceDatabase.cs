/// <summary>
/// Все типы ресурсов определены здесь в коде.
/// </summary>
public static class ResourceDatabase
{
    public static readonly ResourceTypeData Grain = new()
    {
        ResourceId   = "grain",
        DisplayName  = "Зерно",
        MaxStack     = 50,
        IsPerishable = true,
    };

    public static readonly ResourceTypeData Wood = new()
    {
        ResourceId   = "wood",
        DisplayName  = "Дерево",
        MaxStack     = 100,
        IsPerishable = false,
    };

    public static readonly ResourceTypeData Stone = new()
    {
        ResourceId   = "stone",
        DisplayName  = "Камень",
        MaxStack     = 100,
        IsPerishable = false,
    };

    public static readonly ResourceTypeData Bronze = new()
    {
        ResourceId   = "bronze",
        DisplayName  = "Бронза",
        MaxStack     = 50,
        IsPerishable = false,
    };

    public static ResourceTypeData[] All => new[] { Grain, Wood, Stone, Bronze };
}
