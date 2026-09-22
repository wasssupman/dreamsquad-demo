using Unity.Mathematics;
using Wassup.Battle.Units;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 1 — 판 위의 개체. plain class 다.
    //
    // 왜 struct 가 아닌가: 부분 상태(체력·실드·모디파이어·CC·공격·이동…)가 unit 2~6 에서
    // 계속 붙는데, 그것들이 nullable 부분(UML §2 의 `o--`)이기 때문이다. struct 였으면
    // 「있나 없나」가 전부 값 복사 + 플래그가 되고, 담당자가 개체를 «고치는» 코드가
    // 전부 write-back 이 된다.
    //
    // 풀 대여 — `BattleWorld` 가 죽은 개체를 되돌려 받아 `Reset` 하고 다시 빌려준다.
    // 그래서 **필드 추가 시 `Reset` 도 같이 고친다**. 안 고치면 전 판의 값이 새 개체에
    // 새어 들어오고, 그 버그는 결정론 테스트에서만 «가끔» 보인다.
    //
    // 이 unit 의 범위는 `Id·Kind·Faction·Position·HitRadius·Health·Dead·Deploying` 까지다.
    public sealed class Unit
    {
        public SimEntityId Id;
        public UnitKind Kind;
        public Faction Faction;

        /// <summary>정의표(`MatchDefinition.Units` / `.Enemies`)의 인덱스. -1 = 정의 없음(디버그 스폰).</summary>
        public int DefIndex;

        public float3 Position;

        /// <summary>몸 반경(타일). 도달 판정의 «대상의 몸» 항 — 제약 13.</summary>
        public float HitRadius;

        public float Health;
        public float MaxHealth;

        /// <summary>사망 표시. 소멸은 아니다 — 실제 제거는 `BattleWorld.Destroy` 한 곳이다.</summary>
        public bool Dead;

        /// <summary>배치 모션 중. 전투 코어가 소유한 페이즈이고 그동안 표적이 되지 않는다.</summary>
        public bool Deploying;

        // ── unit 2 부분(nullable) ──────────────────────────────────────────────
        // 「있나 없나」가 곧 아키타입이다. 예: `Detection == null` = 감지 0 = 오늘과 같은 경로.

        /// <summary>이동체만. 방어유닛·거점에는 없다.</summary>
        public MoveState Move;

        /// <summary>감지하는 적만. 저작 반경 0 이면 붙지 않는다.</summary>
        public Detection Detection;

        /// <summary>가디언(수용량) 또는 끌려간 적(대상). 둘 다 이 한 타입을 쓴다.</summary>
        public Aggro Aggro;

        /// <summary>순찰 소환물만.</summary>
        public Patrol Patrol;

        /// <summary>배치 유닛만. 다칸 점유가 라이브다(2×2 · 캐논 2×3).</summary>
        public Footprint Footprint;

        internal void Reset()
        {
            Id = SimEntityId.None;
            Kind = UnitKind.None;
            Faction = Faction.None;
            DefIndex = -1;
            Position = float3.zero;
            HitRadius = 0f;
            Health = 0f;
            MaxHealth = 0f;
            Dead = false;
            Deploying = false;

            // 부분은 **버리지 않고 비운다** — 배열을 든 부분(추격판)은 풀이 따로 회수하고,
            // 나머지는 다음 대여 때 그대로 재사용된다(틱 중 할당 0).
            Move?.Reset();
            Detection?.Reset();
            Aggro?.Reset();
            Patrol?.Reset();
            Footprint?.Reset();
            Move = null;
            Detection = null;
            Aggro = null;
            Patrol = null;
            Footprint = null;
        }

        /// <summary>
        /// 표적이 될 수 있나. 옛 전투가 `WithNone` 14곳에 흩어 놓았던 제외 조건의 단일화 —
        /// 새 제외 조건이 생기면 **여기 한 줄**로 들어온다(UML §2).
        /// </summary>
        public bool IsTargetable() => !Dead && !Deploying;
    }
}
