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

        // ── 모디파이어가 결정하는 값 ──────────────────────────────────────────
        // unit 6 의 모디파이어 집계가 이 둘을 쓴다. 지금은 기본값에 머무르고, **소비처는
        // 이미 살아 있다** — 그래야 unit 6 이 값을 넣는 순간 규칙이 저절로 선다.
        // ⚠ 재생은 **피해 인박스 유무와 무관하다**(C24). 옛 전투는 한 단계가 성격이 다른
        // 일을 겸직해 「피해 그릇이 하나도 없으면 재생도 멈추는」 결합이 있었다.

        /// <summary>초당 재생. 회복 인박스를 타지 않고 직접 더해진다(펄스 연출도 없다).</summary>
        public float RegenPerSec;

        /// <summary>받는 피해 배율. 피해 합에 곱해지고 **실드 흡수는 그 뒤**다.</summary>
        public float DamageTakenMul = 1f;

        /// <summary>사망 표시. 소멸은 아니다 — 실제 제거는 `BattleWorld.Destroy` 한 곳이다.</summary>
        public bool Dead;

        /// <summary>
        /// 사망을 표시한 틱. **표시 틱 ≠ 소멸 틱**이 계약이라(unit 3 구현 9) 소멸 단계가
        /// 이 값을 보고 「이번 틱에 죽은 것」을 한 틱 남겨 둔다 — 그 창이 시체 폭발·사직서
        /// 드랍·순찰 연쇄가 자기 자리를 읽을 수 있는 유일한 구간이다.
        /// </summary>
        public int DeathTick = -1;

        /// <summary>배치 모션 중. 전투 코어가 소유한 페이즈이고 그동안 표적이 되지 않는다.</summary>
        public bool Deploying;

        /// <summary>
        /// 지금은 표적이 아니다. **옛 `CoreShielded` 의 후계**이고, 오늘의 유일한 생산자는
        /// `HeartMeter`(본능이 살아 있는 동안 마음을 뺀다)다.
        ///
        /// 「배치 중」·「도약 중」과 같은 축에 두는 이유: 제외 조건이 흩어지면 우선순위 함수가
        /// 그중 몇 개만 아는 상태가 생긴다 — 옛 전투가 `WithNone` 14곳으로 겪은 일이다.
        /// </summary>
        public bool Untargetable;

        /// <summary>
        /// **이 개체의 체력은 다른 담당자가 든다.** 피해 단계가 체력을 안 건드리고 인박스도
        /// 안 비운다 — 그 둘 다 플래그를 세운 쪽의 일이다.
        ///
        /// 오늘의 유일한 생산자는 `HeartMeter`(마음 타워)다. 마음의 체력은 개체가 아니라
        /// 담당자가 들어야 「마음 N개가 저수지 하나를 공유」로 여는 날 이사 비용이 0 이다(X29).
        /// ⚠ 이 플래그를 세우고 **안 비우면 피해가 무한히 쌓인다** — 세운 쪽이 드레인을 진다.
        /// </summary>
        public bool HealthExternal;

        /// <summary>거점인가. 상태이상·모디파이어 **전면 면역**의 술어다(F3).</summary>
        public bool IsStructure => Kind == UnitKind.Structure;

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

        // ── unit 3 부분 ───────────────────────────────────────────────────────

        /// <summary>공격자만. 없으면 이 개체는 아무도 때리지 않는다(거점·길막이 그렇다).</summary>
        public AttackState Attack;

        /// <summary>진행형 상태(도약·치명 타이머·충전). 쓰는 개체만.</summary>
        public ProgressiveStates Progressive;

        /// <summary>
        /// 행동 상태. **결정은 `Wassup.UnitAi`, 저장은 여기**다 — 공격 루프와 이동이 같은
        /// 술어를 보게 만드는 자리다(둘이 갈리면 「락은 있는데 Marching」 데드락이 난다).
        /// </summary>
        public AiStatus Ai;

        // ⚠ 아래 둘은 **항상 있다**(UML §2 의 `*--`). 풀에서 빌려올 때 한 번 만들고
        // `Reset` 이 비우기만 한다 — 틱 중 할당이 0 이어야 하기 때문이다.

        /// <summary>출처별 실드 슬롯. 같은 출처는 max, 다른 출처는 합, 소모는 FIFO.</summary>
        public readonly ShieldSlots Shield = new ShieldSlots();

        /// <summary>이번 틱에 들어온 피해·회복·실드. 비우는 시점이 셋이 같지 않다(C17).</summary>
        public readonly Inbox Inbox = new Inbox();

        /// <summary>
        /// 풀에 돌아가기 전 비우기. **부분은 버리지 않고 `UnitPartPool` 이 회수한다**(F4) —
        /// 틱 중 `new` 가 그대로 쓰레기가 되던 자리다.
        /// ⚠ 부분이 늘면 `UnitPartPool.Reclaim` 도 같이 고친다.
        /// </summary>
        internal void Reset(UnitPartPool parts)
        {
            Id = SimEntityId.None;
            Kind = UnitKind.None;
            Faction = Faction.None;
            DefIndex = -1;
            Position = float3.zero;
            HitRadius = 0f;
            Health = 0f;
            MaxHealth = 0f;
            RegenPerSec = 0f;
            DamageTakenMul = 1f;
            Dead = false;
            DeathTick = -1;
            Deploying = false;
            Untargetable = false;
            HealthExternal = false;

            parts.Reclaim(this);

            Ai.Reset();
            Shield.Reset();
            Inbox.Reset();
        }

        /// <summary>
        /// 표적이 될 수 있나. 옛 전투가 `WithNone` 14곳에 흩어 놓았던 제외 조건의 단일화 —
        /// 새 제외 조건이 생기면 **여기 한 줄**로 들어온다(UML §2).
        ///
        /// 제외 4종: **사망 대기 · 배치 중 · 궁극기로 판 밖에 나간 자**(C16) ·
        /// **표적 제외 선언**(마음 방패 — 본능이 살아 있는 동안의 마음).
        /// ⚠ 일반 도약(보스)은 **비행 중에도 맞는다** — 그쪽은 즉시 순간이동이고 뷰만 난다.
        /// ⚠ 옛 전투는 이 셋을 쿼리(`WithNone`)가 걸러서 우선순위 함수가 둘을 **인자로 안
        /// 받았다.** 쿼리가 사라진 지금 그 표가 조용히 3단으로 줄지 않게 하는 것이 이 술어다.
        /// </summary>
        public bool IsTargetable()
            => !Dead && !Deploying && !Untargetable
               && !(Progressive != null && Progressive.LeapActive);

        /// <summary>
        /// 행동을 시작할 수 있나의 **잠금 축**(START 만 막는다 — 이미 시작한 스윙은 완료된다).
        /// 오늘의 출처는 도약 비행뿐이다. 군중 제어 잠금은 unit 6 이 여기에 OR 로 합류한다.
        /// </summary>
        public bool ActionLocked
            => (Progressive != null && Progressive.LeapActive) || (Move != null && Move.Locked);
    }
}
