namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-F/3 — 방어유닛 클래스. 구 `Wassup.Data.DefenderClass` 이식.
    /// ⚠ **순서가 계약이다** — `EnemyTargetFilter.classMask` 의 비트가 `1 &lt;&lt; (int)value` 다.
    /// 재정렬하면 저작된 마스크가 전부 다른 클래스를 가리킨다. append-only.
    /// </summary>
    public enum DefenderClass
    {
        None,
        Ranger,
        Guardian,
        Fighter,
        Caster,
        Support,
    }

    /// <summary>
    /// 클래스 태그. **없는 대상(해저드 등)은 필터를 우회한다** — 마스크 판정이
    /// "태그가 있고 그 비트가 꺼져 있을 때만" 거른다.
    /// </summary>
    public struct DefenderClassTag
    {
        public DefenderClass value;
    }
}
