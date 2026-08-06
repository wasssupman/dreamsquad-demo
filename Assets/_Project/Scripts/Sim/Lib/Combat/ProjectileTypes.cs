using System.Collections.Generic;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 투사체의 **궤적 축**. 구 `MovementKind` 이식.
    ///
    /// <see cref="PayloadKind"/>(착탄 축)와 **직교**한다. 이 분해가 이 조각의 설계 전부다 —
    /// 새 궤적은 enum 하나 + 위치 순수함수 하나 + 이동 시스템의 arm 하나이고, 새 시스템도
    /// 새 드레인도 새 태그도 필요 없다. ⚠ append-only.
    ///
    /// **기본값 0 = `HomingToEntity`** 인 것이 계약이다 — 기존 스폰이 아무 변경 없이 레거시
    /// 호밍으로 남는다.
    /// </summary>
    public enum MovementKind : byte
    {
        /// 대상의 live 위치를 추적, `hitThreshold` 안에 들면 도착. **대상 소실 = 파괴**(레거시).
        HomingToEntity = 0,

        /// <summary>
        /// XZ 를 셀 고정 착탄점까지 보간하고 Y 에 사인 아치를 얹는다. `elapsed >= flightTime` 에 도착.
        /// 대상 엔티티가 없으므로 **비행 중 대상 사망/이동이 무관**하다(착탄점이 발사 시 고정).
        /// </summary>
        BallisticArcToPoint = 1,

        /// <summary>
        /// 셀 고정 착탄점에서 `flightTime` 동안 **대기**한 뒤 도착(메테오 텔레그래프).
        /// ⚠ sim 위치는 이동하지 않는다 — 떨어지는 그림은 뷰 공간 전용이다.
        /// 이동 거리가 0 이라 `flightTime` 을 속도로 유도할 수 없어 **요청이 싣고 온다**.
        /// </summary>
        SkyFall = 2,

        /// <summary>
        /// 발사 시 고정된 방향으로 `maxDistance` 만큼 직선 비행 후 소멸. 대상도 지점 도착도 없고,
        /// 명중은 비행 중 <see cref="PayloadKind.PathHit"/> 스윕으로 일어난다.
        /// </summary>
        DirectionalLinear = 3,

        /// <summary>
        /// 셀 고정 착탄점까지 굴러가(요청이 실은 `flightTime`, 속도 유도 아님) `fuseSec` 동안
        /// 머문 뒤 `flightTime + fuseSec` 에 도착. 구르기는 아치 높이 ≈ 0 인 궤적 재사용.
        /// </summary>
        GrenadeToCell = 4,

        /// <summary>
        /// 곡선으로 날며 대상을 추적. `HomingToEntity`(직진·추적)와 `BallisticArcToPoint`
        /// (곡선·셀고정)가 배타적이라 표현할 수 없던 조합이다.
        ///
        /// ⚠ **대상 소실 = 파괴이고 재조준을 지원하지 않는다.** `t = elapsed/flightTime` 로
        /// 진행하므로 `t≈1` 에서 재조준하면 새 대상으로 순간이동한 뒤 즉시 착탄한다. 곡선을 다시
        /// 그리려면 저작 파라미터가 필요한데 sim 은 그걸 읽지 않는다.
        /// </summary>
        BezierHomingToEntity = 5,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 투사체의 **착탄 축**. 구 `PayloadKind` 이식.
    /// 기본값 0 = `SingleSplash`(레거시). ⚠ append-only.
    /// </summary>
    public enum PayloadKind : byte
    {
        /// 직격 대상 + 스플래시 보너스. 레거시 착탄.
        SingleSplash = 0,
        /// 착탄 셀 기준 Chebyshev `impactTileRange` 안 전원에게 flat 피해. 직격 대상도 감쇠도 없다.
        TileAoe = 1,
        /// <summary>
        /// 매 프레임 이전→현재 선분을 스윕해 경로 위 대상을 때린다. **대상당 최대 1회**
        /// (히트 기록 버퍼)이고 관통 예산이 떨어질 때까지 계속한다. 지점 도착 해결이 없다.
        /// </summary>
        PathHit = 2,
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — TileAoe 착탄이 때릴 진영. 구 `ProjectileTargetFaction` 이식.
    /// ⚠ **0 = Enemy** 라 기존 스폰이 전부 레거시 적 풀을 유지한다. 다른 payload 는 이 값을 무시한다.
    /// </summary>
    public enum ProjectileTargetFaction : byte { Enemy = 0, Defender = 1 }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 온-히트 효과 선택자. 구 `Wassup.Data.OnHitEffectType` 이식.
    /// 저작 계층이 원본이므로 <see cref="DcTriggerKind"/> 와 같은 이유로 **복제**다. ⚠ append-only.
    /// </summary>
    /// ⚠ 기반 타입은 구와 같은 **int**(무표기)다 — 18-M 스윕이 `: byte` 좁히기를 잡았다.
    /// 값 렌더(`Convert.ToInt64`)에는 무해하지만, 이식 충실성의 기본은 "다르게 하지 않는 것"이다.
    public enum OnHitEffectType { None = 0, Poison = 1, Fire = 2, Splash = 3, Slow = 4 }

    /// 비행 중인 투사체 표식. 구 `ProjectileTag` 이식.
    public struct ProjectileTag { }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 투사체를 쏘는 유닛에 붙는 저작 사본.
    /// 구 `ProjectileRef` 이식. `dataIndex` 는 뷰 쪽 캐시의 index 다(sim 은 뜻을 모른다).
    /// </summary>
    public struct ProjectileRef
    {
        public int dataIndex;
        public float speed;
        public float hitThreshold;
        public float visualScale;
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;
        public MovementKind movement;
        public PayloadKind payload;
        public float arcHeight;
        public int impactTileRange;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 요청 전용 캐리어 표식. 구 `ProjectileRequestCarrier` 이식.
    ///
    /// **왜 있나**: `ProjectileSpawnRequest` 는 엔티티당 하나라, 쏘는 유닛이 같은 프레임에 자기
    /// 공격을 이미 올려둔 경우 드림캐쳐 발사가 얹힐 자리가 없다. 그래서 요청 하나만 들고 있는
    /// 엔티티를 따로 만든다. 드레인은 이 캐리어를 **통째로 파괴**한다(컴포넌트 제거가 아니다).
    /// </summary>
    public struct ProjectileRequestCarrier { }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 이미 때린 대상 기록. 구 `PathHitRecord` 이식.
    ///
    /// ⚠ **이게 없으면 느린 관통탄이 같은 적을 매 프레임 다시 때린다** — 경로 스윕은 반경 안에
    /// 머무는 대상을 프레임마다 다시 맞히기 때문이다.
    /// </summary>
    public struct PathHitRecord
    {
        public SimEntityId value;

        public static bool Contains(List<PathHitRecord> records, SimEntityId victim)
        {
            if (records == null) return false;
            for (int i = 0; i < records.Count; i++)
                if (records[i].value == victim) return true;
            return false;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 비행 중 투사체의 상태. 구 `ProjectileState` 이식.
    ///
    /// 필드가 많은 것은 **두 직교 축(궤적 × 착탄)의 파라미터가 한 struct 에 공존**하기 때문이다.
    /// 축마다 자기 슬롯만 읽고, 안 쓰는 슬롯은 기본값 그대로 남는다.
    ///
    /// ⚠ `damage` 는 **발사 시점 스냅샷**이다(이미 시전자의 damageMul 이 곱해진 값). 비행 중
    /// 버프가 꺼져도 변하지 않는다.
    ///
    /// ⚠ `impactReached` 는 **궤적이 쓰고 착탄이 읽는다.** 도착 조건은 궤적마다 다르므로
    /// 이동 쪽이 소유한다 — 착탄 시스템은 "왜 도착했는지" 를 묻지 않는다.
    /// </summary>
    public struct ProjectileState
    {
        // ── 축 판별자 ─────────────────────────────────────────────────────────
        public MovementKind movement;
        public PayloadKind payload;

        // ── 공통 ──────────────────────────────────────────────────────────────
        public float damage;
        /// 히트 VFX 라우팅 키. sim 은 뜻을 모르고 나르기만 한다.
        public int dataIndex;
        /// 궤적이 끝점에 닿았다. **쓰기는 이동, 읽기는 착탄.**
        public bool impactReached;

        // ── 호밍 ──────────────────────────────────────────────────────────────
        public SimEntityId target;
        public float speed;
        public float hitThreshold;

        // ── 아치/스카이폴/수류탄 (셀 고정 착탄점) ────────────────────────────
        public SimVec3 origin;
        public SimVec3 impact;
        public float flightTime;
        public float elapsed;
        public float arcHeight;

        // ── 베지어 호밍 ───────────────────────────────────────────────────────
        /// 발사 시 결정론 산출. P0 = `origin`, P3 = 대상 live 위치라 여기 없다.
        public SimVec3 control1;
        public SimVec3 control2;

        /// <summary>
        /// 수류탄 신관 — 이동이 끝난 뒤(`elapsed >= flightTime`) 추가로 머무는 시간.
        /// 도착은 `flightTime + fuseSec` 에 난다. ⚠ **타이밍은 이동 소유** — 착탄은 이 값을 모른다.
        /// </summary>
        public float fuseSec;

        // ── 직선 방향 ─────────────────────────────────────────────────────────
        /// 발사 시 고정된 단위 방향(sim 평면). 비행 중 바뀌지 않는다.
        public SimVec2 direction;
        /// 월드 거리 상한. 여기 닿으면 `impactReached` = "비행 종료" 다(도착이 아니라).
        public float maxDistance;
        /// 지난 프레임 위치 — 착탄이 **방금 지난 선분**을 스윕할 수 있게 이동이 써 준다.
        public SimVec3 prevPos;

        // ── 경로 명중 ─────────────────────────────────────────────────────────
        /// 남은 관통 수(1 = 첫 대상에서 멈춤). 발사 후 쓰기는 착탄 시스템 단독.
        public int pierceRemaining;

        // ── 단일/스플래시 ─────────────────────────────────────────────────────
        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        // ── 타일 AoE ──────────────────────────────────────────────────────────
        public int impactTileRange;
        /// 최근접 N 명 cap. **0 = 무제한**(레거시 메테오/스킬/보스 경로).
        public int aoeTargetCap;
        public byte ccKind;
        public float ccDuration;
        /// 뷰 변종 인덱스 — sim 무해 캐리어다(프레젠테이션이 색으로 해석).
        public byte bombType;

        // ── 바운스 ────────────────────────────────────────────────────────────
        /// <summary>
        /// 착탄 **후** 생존: 남은 홉이 있고 재조준 후보가 있으면 파괴 대신 다시 호밍한다.
        /// 발사 후 쓰기는 착탄 시스템 단독. 기본 0 = 레거시 파괴 경로.
        /// </summary>
        public int bounceRemaining;
        public int bounceTileRange;
        /// 홉마다 적용되는 감쇠.
        public float bounceDamageMul;

        /// <summary>
        /// 대상이 사라졌을 때 **파괴 대신 재조준**할 반경(Chebyshev 타일). 0 = 비활성.
        ///
        /// ⚠ `bounceRemaining` 과 **다른 축이다** — 저건 "맞히고 나서 남은 홉"(소비형·감쇠 있음)이고
        /// 이건 "맞히기도 전에 대상이 사라진 경우"(비소비형·감쇠 없음)다. 한 투사체가 둘 다 가질 수
        /// 있어 합치지 않는다.
        ///
        /// ⚠ **원점이 다르다**: 저작의 tileRange 는 시전자 기준인데 이 값은 **투사체 현재 위치**
        /// 기준으로 재해석된다 — 같은 숫자라도 날아간 만큼 실효 범위가 늘어난다.
        /// </summary>
        public int retargetTileRange;

        /// <summary>
        /// 쏜 주체. 위협 귀속에 쓴다. **브리지 캐스트 스킬은 `Null`** 이다(의도적 미귀속).
        /// 바운스 재호밍을 넘어 살아남는다.
        /// </summary>
        public SimEntityId owner;

        /// TileAoe 피해자 진영. 기본 `Enemy`.
        public ProjectileTargetFaction targetFaction;

        /// <summary>
        /// 최전방 우선 피해 — **직격 대상이 이 엔티티일 때만** 배율이 붙는다.
        /// 피해와 위협 양쪽에 같이 적용된다(desync 금지). 바운스를 넘어 살아남되 조건은 매번 재평가.
        /// </summary>
        public SimEntityId priorityTarget;
        public float priorityDamageMul;

        /// <summary>
        /// 강공 배율 — `priorityDamageMul`(한 명)과 달리 이 샷의 **모든** 피해 대상에 곱한다
        /// (cleave/splash/bounce 포함). 기본 0 = inert(실적용은 `mul > 0 ? mul : 1`).
        /// </summary>
        public float heavyDamageMul;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 투사체 스폰 요청. 구 `ProjectileSpawnRequest` 이식.
    ///
    /// 두 축의 파라미터를 다 싣고, 소비자가 `movement`/`payload` 에 맞는 부분집합만
    /// <see cref="ProjectileState"/> 로 복사한다.
    ///
    /// ⚠ 일부 값은 **소비 시점에 유도된다**(아치의 `flightTime` = 거리/속도, 베지어 제어점 =
    /// `swingIndex` + 저작 파라미터). 그래서 발사 주체는 저작 데이터를 몰라도 되고, 이 struct 가
    /// 궤적마다 비대해지지 않는다.
    /// </summary>
    public struct ProjectileSpawnRequest
    {
        public MovementKind movement;
        public PayloadKind payload;

        public SimVec3 origin;
        public float damage;
        /// 호밍은 비행 속도, 아치는 여기서 `flightTime` 을 유도한다.
        public float speed;
        public float visualScale;
        public int dataIndex;

        public SimEntityId target;
        public float hitThreshold;

        public SimVec3 impact;
        public float arcHeight;

        /// 베지어 스윙 소스 — 제어점 자체는 소비자가 저작값과 함께 산출한다. 기본 0 = 중앙.
        public int swingIndex;

        /// 스카이폴 전용: 이동 거리가 0 이라 속도로 유도할 수 없어 **요청이 싣고 온다**.
        public float flightTime;
        public float fuseSec;

        public SimVec2 direction;
        public float maxDistance;

        public OnHitEffectType onHitEffect;
        public float splashRadius;
        public float splashDamageMul;

        public int impactTileRange;
        public int aoeTargetCap;
        public byte ccKind;
        public float ccDuration;
        public byte bombType;

        public int bounceRemaining;
        public int bounceTileRange;
        public float bounceDamageMul;
        public int retargetTileRange;

        public SimEntityId owner;
        public ProjectileTargetFaction targetFaction;

        public SimEntityId priorityTarget;
        public float priorityDamageMul;
        public float heavyDamageMul;
    }

    /// 요청에 딸린 출력 목록(피해/힐/CC…). 구 `ProjectileSpawnOutputElement` 이식.
    public struct ProjectileSpawnOutputElement
    {
        public AttackOutput value;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-H/1 — 착탄 원샷. 구 `ProjectileHitEvent` 이식.
    ///
    /// ⚠ **샷 하나당 하나**다 — 스플래시 2차 피해자는 여기 실리지 않는다(연출은 착탄 1회).
    /// `source` 는 소멸 직전 스냅샷으로, 뷰가 "이 착탄이 그 텔레그래프의 착탄인가" 를 정확히
    /// 판별하는 데 쓴다. 판별과 시각 라우팅을 분리해 둔 덕에 둘이 서로를 깨뜨리지 않는다.
    ///
    /// `radiusWorld` 는 저작 상수가 아니라 **캐스트마다 다른** 값이라 해결 시점에 스냅샷한다.
    /// </summary>
    public struct ProjectileHitEvent
    {
        public SimVec3 position;
        public int dataIndex;
        public SimEntityId source;
        public PayloadKind payload;
        public float radiusWorld;
    }
}
