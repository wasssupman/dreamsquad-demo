namespace Wassup.Data
{
    // enemy-class-system Unit 0 — enemy archetype. Parallel to DefenderClass
    // (defenders keep their own role). Drives future behavior branches
    // (Runner taunt-only attack, Bruiser/Shooter attack-move pause, Shooter
    // kiting vs stop-and-shoot). Values are serialized in AttackUnitData assets
    // — do not reorder.
    public enum EnemyClass
    {
        None,    // 0
        Tanker,  // 1
        Runner,  // 2
        Bruiser, // 3
        Shooter, // 4
    }
}
