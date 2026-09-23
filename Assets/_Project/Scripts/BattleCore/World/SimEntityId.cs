using System;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 0 항목 9 / unit 1 — 코어의 유일한 개체 손잡이.
    //
    // 센티널(unit 0 에서 확정):
    //   · `Match` = **0** — 판 자신이 host 인 사건(매치 시작/종료, 기믹, 플레이어 시전).
    //   · 유닛·투사체 = **1 부터** 스폰 순번. 한 판 안에서 재사용하지 않는다.
    //   · `None` = **-1** — 「없는 게 맞다」. 정렬에 절대 안 들어간다.
    //
    // ⚠ 옛 전투는 미발급을 `int.MaxValue` 로 썼다(`SkillEntityId.UnassignedValue`).
    // 값이 다르므로 **옛 골든과 id 로 대조하지 않는다** — 대조 축은 순서다(계약 3).
    // -1 을 고른 이유: 오름차순 순회에서 미발급이 «맨 앞»이 아니라 «범위 밖»이 되어,
    // 실수로 목록에 섞이면 0 번 host 를 밀어내는 대신 곧바로 눈에 띈다.
    public readonly struct SimEntityId : IEquatable<SimEntityId>, IComparable<SimEntityId>
    {
        public const int MatchValue = 0;
        public const int NoneValue = -1;
        public const int FirstSpawnValue = 1;

        /// <summary>판 자신. 「누구의 사건도 아닌 사건」의 host.</summary>
        public static readonly SimEntityId Match = new SimEntityId(MatchValue);

        /// <summary>없는 게 맞다. 미발급이 아니라 부재.</summary>
        public static readonly SimEntityId None = new SimEntityId(NoneValue);

        public readonly int Value;

        public SimEntityId(int value) => Value = value;

        /// <summary>스폰된 개체인가(판 host 도 부재도 아닌가).</summary>
        public bool IsEntity => Value >= FirstSpawnValue;

        public bool IsNone => Value == NoneValue;

        public int CompareTo(SimEntityId other) => Value.CompareTo(other.Value);
        public bool Equals(SimEntityId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is SimEntityId o && Equals(o);
        public override int GetHashCode() => Value;
        public override string ToString() => IsNone ? "none" : Value.ToString();

        public static bool operator ==(SimEntityId a, SimEntityId b) => a.Value == b.Value;
        public static bool operator !=(SimEntityId a, SimEntityId b) => a.Value != b.Value;
    }
}
