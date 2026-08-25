namespace Wassup.Skills
{
    // skill-layer-foundation unit 2a — 도메인이 쓰는 유일한 엔티티 핸들.
    //
    // `Wassup.Battle.Units.SimEntityId` 를 그대로 쓸 수 없는 이유는 **그것이
    // `IComponentData`** 라서다. 이 어셈블리는 `Unity.Entities` 를 참조하지 않으므로
    // (그것이 계약 1 의 컴파일 게이트다) 그 타입을 이름조차 부를 수 없다.
    //
    // 그래서 값은 같고 타입만 다르다 — 변환은 **어댑터 한 곳**에서만 일어난다.
    // 도메인은 이 번호가 어디서 왔는지 모르고, 알 필요도 없다.
    //
    // 계약(`SimEntityId` 에서 그대로 승계):
    //   · 매치 안에서 유일하고 재사용되지 않는다. 스폰 순서대로 발급.
    //   · `Unassigned` 는 **맨 뒤로 밀린다** — 0 을 폴백으로 쓰면 미발급끼리가 아니라
    //     «0번 유닛» 과 충돌해 조용히 순위를 훔친다.
    public readonly struct SkillEntityId : System.IEquatable<SkillEntityId>
    {
        // 미발급. `SimEntityId.Unassigned` 와 같은 값이어야 한다.
        public const int UnassignedValue = int.MaxValue;

        public readonly int Value;

        public SkillEntityId(int value) => Value = value;

        // 시전 주체가 **엔티티가 아닌** 경우를 표현한다. 액티브 스킬(플레이어 시전)이
        // 그렇다 — `ThreatTable` 이 "bridge-cast skills (player Meteor, owner == Null)"
        // 라고 적어 둔 그 경로다. `Unassigned` 와 같은 값을 쓰되 이름을 나눠 두는 이유는
        // 읽는 쪽의 의도가 다르기 때문이다: 미발급은 «아직 없다», None 은 «없는 게 맞다».
        public static readonly SkillEntityId None = new SkillEntityId(UnassignedValue);

        public bool IsValid => Value != UnassignedValue;

        public bool Equals(SkillEntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SkillEntityId o && Equals(o);
        public override int GetHashCode() => Value;
        public override string ToString() => IsValid ? Value.ToString() : "none";

        public static bool operator ==(SkillEntityId a, SkillEntityId b) => a.Value == b.Value;
        public static bool operator !=(SkillEntityId a, SkillEntityId b) => a.Value != b.Value;
    }
}
