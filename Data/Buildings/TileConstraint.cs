/// <summary>
/// Требование к тайлу при размещении здания.
/// None          = любой buildable тайл (Grass/Sand)
/// NearForest    = хотя бы один adjacent тайл = Forest
/// NearRock      = хотя бы один adjacent тайл = Rock
/// NearWater     = хотя бы один adjacent тайл = Water
/// NearCopperOre = рядом должен быть тайл CopperOre (для Медной шахты)
/// NearTinOre    = рядом должен быть тайл TinOre    (для Оловянной шахты)
/// </summary>
public enum TileConstraint
{
    None,
    NearForest,
    NearRock,
    NearWater,
    NearCopperOre,
    NearTinOre,
}
