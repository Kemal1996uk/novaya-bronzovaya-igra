using Godot;

/// <summary>
/// Описание типа ресурса (данные, без логики).
/// Создавай .tres файлы в Godot: ПКМ в FileSystem → New Resource → ResourceTypeData.
///
/// Пример использования: var name = grainData.DisplayName;
/// </summary>
[GlobalClass]
public partial class ResourceTypeData : Resource
{
    /// <summary>Уникальный ID для поиска в словарях (snake_case, например "grain").</summary>
    [Export] public string ResourceId   { get; set; } = "";

    /// <summary>Отображаемое имя в интерфейсе.</summary>
    [Export] public string DisplayName  { get; set; } = "Ресурс";

    /// <summary>Иконка ресурса (null = заглушка).</summary>
    [Export] public Texture2D Icon      { get; set; }

    /// <summary>Максимальное количество в одном стеке/слоте.</summary>
    [Export] public int MaxStack        { get; set; } = 100;

    /// <summary>Портится со временем? (еда — да, камень — нет)</summary>
    [Export] public bool IsPerishable   { get; set; } = false;
}
