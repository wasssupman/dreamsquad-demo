using Unity.Mathematics;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 2 — 판 위에 깔린 «장(場)». 유닛이 아니라서 `BattleWorld.Units`
    // 밖에 산다(UML §2 의 `FieldCarrier`).
    //
    // 이 unit 이 여는 것은 **이동이 소비하는 두 종류**뿐이다: 포탈과 당김. 수명·생성은 효과
    // 레이어(unit 6)가 갖고, 여기서는 이동이 매 틱 읽기만 한다. 아군 버프 장은 종류만 예약한다.
    //
    // ⚠ **당김은 이동을 «대체»하지 않는다.** 이동 뒤에 더해지는 가산 변위이고, 그래서 벽과
    // 장애물에 막힌다. 대체하게 만들면 회오리가 유닛을 벽 안으로 끌고 들어간다.
    public enum FieldKind : byte
    {
        None = 0,
        AllyBuff = 1,
        Pull = 2,
        Portal = 3,
    }

    public sealed class FieldCarrier
    {
        /// <summary>unit 6b — 사건의 키. 발급은 `BattleWorld.SpawnField` 한 곳이다.</summary>
        public SimEntityId Id = SimEntityId.None;

        public FieldKind Kind;

        /// <summary>Portal = 입구 · Pull = 중심 · AllyBuff = 중심.</summary>
        public float3 Center;

        /// <summary>Portal = 출구. 다른 종류에서는 쓰지 않는다.</summary>
        public float3 Exit;

        /// <summary>반경(칸, 자리형 — 원점 항 칸 반폭은 판정 진입점이 붙인다). Portal = 0(입구 칸 자체) · Pull · AllyBuff.</summary>
        public float Range;

        /// <summary>Pull = 초당 당기는 거리.</summary>
        public float Speed;

        /// <summary>남은 시간. 0 이하 = 무기한(효과 레이어가 정한다).</summary>
        public float Duration;

        /// <summary>진영(발동자). 당김·포탈은 진영을 보지 않는다 — 순찰 아군도 밀린다.</summary>
        public int Faction;

        // ── unit 6b — 아군 버프 장 ────────────────────────────────────────────
        //
        // ⚠ **멤버십이 스냅샷이 아니다.** 안에 선 아군에게 매 틱 짧은 모디파이어를 재발행하고,
        // 그래서 이탈·만료·사망이 전부 «재발행이 멈춘다»로 처리된다(회수 원시연산 불요).
        // 그 「짧은」의 값은 `Effects.FieldRefresh` 가 틱 길이에서 산출한다 — 밸런스가 아니라
        // 프레임워크 상한이고, 옛 0.5초의 근거(엔진 최대 프레임 델타)는 고정 틱에서 사라졌다(F36).

        /// <summary>깐 쪽. 트레이스·귀속용이고 모디파이어 병합 키가 아니다.</summary>
        public SimEntityId Source = SimEntityId.None;

        /// <summary>AllyBuff = 움직일 스탯(`Effects.StatKind`).</summary>
        public Effects.StatKind Stat;

        /// <summary>
        /// AllyBuff = 저작 **배율 그대로**(×2.0). 버킷 분류는 `ModifierAuthoring.FromMultiplier`
        /// 가 단독으로 하고, 여기 실리는 것은 분류 전 값이다.
        /// </summary>
        public float Magnitude;

        /// <summary>
        /// AllyBuff = 재발행이 거는 모디파이어의 지속(초). 0 이면 `FieldRefresh.Seconds(dt)`.
        ///
        /// ⚠ **장 수명(`Duration`)을 여기 넣지 말 것.** 한 번이라도 긴 값으로 걸면 갱신이
        /// 그 값을 내릴 수 없고(갱신은 «긴 쪽»이 이긴다) 장을 벗어나도 버프가 남는다 —
        /// 장판화가 없애려던 스냅샷 동작으로 그대로 회귀한다.
        /// </summary>
        public float RefreshSeconds;
    }
}
