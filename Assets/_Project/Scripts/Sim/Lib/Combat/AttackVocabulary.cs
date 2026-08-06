using System.Collections.Generic;
using Wassup.Sim.Effects;
using Wassup.Sim.Movement;

namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 폭탄맨 발사 상태. 구 `BombLauncherState` 이식.
    ///
    /// ⚠ **`rng` 가 상태 해시에 실린다.** 캐스터별 독립 스트림이고 시드는
    /// `max(1, bombSeedBase ^ cellHash)` 로 파생된다(비0 보장) — 그 파생은 소비 지점이 한다.
    /// xorshift 한 draw 라도 어긋나면 그 뒤 모든 확률 판정이 갈린다.
    ///
    /// 3변종(데미지/수면/스턴)이 정확히 3종이라 배열이 아니라 인라인 필드다(제약 8).
    /// </summary>
    public struct BombLauncherState
    {
        /// 방향(<see cref="DeployedFacing"/>)으로 몇 칸 앞에 착지하는가.
        public int landingTiles;
        /// 발사→착지 **고정** 시간(거리 무관 — 속도로 유도하지 않는다).
        public float travelSec;
        /// 착지→폭발 고정 시간.
        public float fuseSec;
        /// 착지 셀 기준 폭발 반경(Chebyshev).
        public int aoeTileRange;
        /// 가까운 순 최대 타격 수. 0 = 무제한.
        public int aoeTargetCap;
        /// 구르기 아치(≈0 = 지면). 뷰 전용 높이.
        public float arcHeight;
        public float dmgBombDamage;
        public float sleepSec;
        public float stunSec;

        public SimRandom rng;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 상시 적용 공격 모디파이어의 종류.
    /// 구 `Wassup.Data.DcAttackModKind` 이식(저작 복제 — <see cref="DcTriggerKind"/> 와 같은 사정).
    /// ⚠ append-only.
    /// </summary>
    public enum DcAttackModKind { None, ProjectileBounce, FrontmostTarget }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — **트리거 없는** 공격 모디파이어 슬롯.
    /// 구 `DcAttackModSlot` 이식.
    ///
    /// ⚠ <see cref="DcTriggerSlot"/> 과 달리 **카운터가 없다** — 발동 사건이 아니라 상시 적용이다.
    /// 공격 루프가 기본 공격의 스폰 요청에 이 값들을 집계해 얹는다(읽기 전용 소비).
    ///
    /// 필드 의미가 kind 마다 다르다:
    /// `ProjectileBounce` = `count` 홉 수 · `tileRange` 재조준 반경 · `damageMul` 홉당 감쇠.
    /// `FrontmostTarget` = `damageMul` 만(주 타겟 배율), 나머지는 미사용(사거리는 기본 공격 것).
    /// </summary>
    public struct DcAttackModSlot
    {
        /// ⚠ `DcTriggerSlot.instanceId` 와 **같은 할당자**를 쓴다(별개 네임스페이스가 아니다).
        public int instanceId;
        public DcAttackModKind kind;
        public int count;
        public int tileRange;
        public float damageMul;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 방어유닛 저작 CC 값의 사본. 구 `DefenderCcData` 이식.
    /// 전 필드 기본 0 = 기존 동작 무변경.
    ///
    /// ⚠ <see cref="sleepOnHitSec"/>(주 타겟 1체)과 <see cref="knockupOnHitSec"/>(히트한 **전 대상**)은
    /// **스코프가 다르다** — 하나로 합치면 다중 대상 유닛이 깨진다.
    ///
    /// ⚠ <see cref="knockupVisualHeight"/> 는 sim 이 **읽지도 쓰지도 않고** 실어 보내기만 한다.
    /// 순수 뷰 값이 sim 을 경유하는 형태이고 `ProjectileRef.visualScale` 선례와 같다 —
    /// 값 자체가 아키텍처를 모르고 뷰만 해석한다.
    /// </summary>
    public struct DefenderCcData
    {
        public float knockbackDistance;
        public float knockbackDuration;
        public float onPlacePushDistance;
        public float onPlacePushDuration;
        public float onPlacePushRadius;
        /// RESOLVE 주 타겟에게 Sleep N초. 0 = 비활성.
        public float sleepOnHitSec;
        /// 히트한 **전 대상**에게 Stun N초 — 공중 띄우기의 sim 실체.
        public float knockupOnHitSec;
        /// 뷰 전용(sim 은 나르기만 한다).
        public float knockupVisualHeight;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 배치 시 고른 **영구** 공격 방향. 구 `DeployedFacing` 이식.
    ///
    /// ⚠ 쓰기는 **활성화 시점 1회**뿐이고 이후 불변이다. 소유는 Units(배치)이고 Combat 은 읽어서
    /// 레인 타겟팅을 걸고 일제사격 방향을 잡는다.
    /// </summary>
    public struct DeployedFacing
    {
        /// 타일 격자 위의 **기본 방위 단위 벡터**.
        public SimInt2 value;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 공격 1회분 "최전방" 잠금. 구 `FrontmostAttackLock` 이식.
    ///
    /// ⚠ **부착/제거를 공격마다 하지 않는다**(구조 변경 없음) — 값만 START/RESOLVE 에서 갱신한다.
    /// 수명이 방어유닛과 같아 별도 정리 경로가 없다.
    ///
    /// 모델(**strict lapse**): START 에서 현재 최전방 후보를 골라 잠그고, 준비 동작 중 순위 변동은
    /// 무시한다. RESOLVE 는 잠근 대상이 **살아 있고 사거리 안**이면 그것을 쓰고, 아니면 그 공격은
    /// **불발**한다(사망·소멸·사거리 이탈·유출에 재선택 없음). 성공이든 불발이든 잠금은 리셋된다.
    ///
    /// ⚠ <see cref="damageMulSnapshot"/> 이 START 에서 배율을 얼린다 — 공격 도중 카드가 바뀌어도
    /// 진행 중인 공격은 영향받지 않는다.
    /// </summary>
    public struct FrontmostAttackLock
    {
        public bool active;
        /// `Null` = 없음.
        public SimEntityId target;
        /// START 시점 활성 슬롯들의 `damageMul` 곱. 1 = 없음.
        public float damageMulSnapshot;
        /// 잠근 대상이 배율 수령자인가(폴백/최근접으로 잡혔으면 false).
        public bool targetIsPriority;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 소환사 상태. 구 `SummonerState` 이식.
    ///
    /// ⚠ **자체 쿨다운이 없다** — `AttackState.cooldownRemaining` 이 곧 소환 주기다(폭탄맨 선례).
    /// <see cref="current"/> 가 유효한 동안은 소환을 건너뛰고 쿨다운만 돈다.
    ///
    /// ⚠ <see cref="hasSummonedOnce"/> 의 writer 는 **순찰병이 실제로 생성된 시점** 하나다.
    /// 요청을 stage 할 때 켜면 스냅 실패로 소환이 취소된 경우에도 게이트가 소비된다.
    /// 한 번 켜지면 이후 재소환은 적 유무를 보지 않는다 — 순찰병이 죽는 이유가 곧 적이 있다는
    /// 뜻이라 재게이트는 같은 사실을 두 번 묻는 셈이고, 교전 중 적이 잠깐 구역을 벗어난 프레임에
    /// 재소환이 끊기면 순환이 덜컥거린다.
    /// </summary>
    public struct SummonerState
    {
        /// 저작 레지스트리 index(SO 는 sim 에 담을 수 없다).
        public int patrolDataIndex;
        public int leashTileRadius;
        /// 살아 있는 순찰병. `Null` = 없음.
        public SimEntityId current;
        public bool hasSummonedOnce;
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 반경 내 **최근접** 대상 선정. 구 `NearestTargeting` 이식.
    ///
    /// 최전방/최저체력/어그로 계열과 같은 순수 랭킹 유틸이고 특정 카드의 소유물이 아니다.
    ///
    /// ⚠ **호출부 책임**: 진영·상태 필터(사망/미배치/유출 등)를 적용해 통과분만 `eligible` 로
    /// 넘긴다. **어떤 진영을 고르는지는 호출 맥락의 결정**이지 이 유틸의 결정이 아니다.
    /// </summary>
    public static class NearestTargeting
    {
        public struct Candidate
        {
            public bool eligible;
            /// Chebyshev 타일 거리(반경 판정).
            public int tileDist;
            /// XZ 제곱 거리(랭킹).
            public float sqDist;
            public int simId;
        }

        /// 최근접 우선, **동거리는 낮은 simId**. 후보 배열 순서가 흔들려도 같은 대상이 뽑힌다.
        public static bool RanksBefore(in Candidate a, in Candidate b)
        {
            if (a.sqDist != b.sqDist) return a.sqDist < b.sqDist;
            return a.simId < b.simId;
        }

        /// <summary>
        /// 반경 안에서 가장 앞선 후보의 index(없으면 -1).
        ///
        /// ⚠ 형제 유틸들은 반경 필터도 호출부에 맡기는데 **여기만 안에 둔다** —
        /// `tileRange &lt;= 0 = 선정 없음`이 이 함수의 계약이고, 그 해석이 호출처마다 갈리면
        /// 안 되기 때문이다(0 을 "자기 셀만 검색" 으로 읽는 구현이 섞이면 조용히 엉뚱한 대상이 뽑힌다).
        /// 그 0 이 기능 비활성인지 데이터 누락인지는 호출 맥락이 해석한다.
        /// </summary>
        public static int SelectNearest(List<Candidate> candidates, int tileRange)
        {
            if (tileRange <= 0) return -1;

            int best = -1;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!c.eligible) continue;
                if (c.tileDist > tileRange) continue;
                if (best < 0 || RanksBefore(c, candidates[best])) best = i;
            }
            return best;
        }
    }

    /// <summary>
    /// battle-sim-extraction unit 18-I/2 — 공격 루프가 쓰는 순수 판정 조각들.
    /// 구 `AttackSystem` 의 private static 헬퍼 이식.
    /// </summary>
    public static class AttackMath
    {
        /// 남은 시간이 있는 CC 가 하나라도 있는가.
        public static bool AnyActiveCc(List<CcEffect> buf)
        {
            if (buf == null) return false;
            for (int i = 0; i < buf.Count; i++)
                if (buf[i].remainingTime > 0f) return true;
            return false;
        }

        /// <summary>
        /// 공격자→대상 **XZ** 제곱 거리. 대상이 여러 셀을 점유하는 차단 해저드면 **가장 가까운 셀**을
        /// 쓴다 — 안 그러면 큰 구조물의 중심만 재서 옆면에 붙어도 사거리 밖으로 판정된다.
        ///
        /// `nearestTargetPos` 로 그 셀의 월드 좌표가 나간다(발사 원점 조준에 쓴다).
        /// </summary>
        public static float DistanceSqToTarget(
            SimVec3 attackerPos,
            SimEntityId target,
            SimVec3 fallbackTargetPos,
            List<BlockingHazardCellsBuffer> hazardCells,
            bool hasFlowField,
            FlowFieldSingleton flowField,
            out SimVec3 nearestTargetPos)
        {
            nearestTargetPos = fallbackTargetPos;
            SimVec3 diff = fallbackTargetPos - attackerPos;
            float bestSq = diff.x * diff.x + diff.z * diff.z;

            if (!hasFlowField || hazardCells == null) return bestSq;

            for (int i = 0; i < hazardCells.Count; i++)
            {
                SimVec3 cellWorld = GridMath.CellToWorldCenter(
                    hazardCells[i].cell, flowField.tileSize, fallbackTargetPos.y, flowField.origin);
                diff = cellWorld - attackerPos;
                float d2 = diff.x * diff.x + diff.z * diff.z;
                if (d2 >= bestSq) continue;
                bestSq = d2;
                nearestTargetPos = cellWorld;
            }

            return bestSq;
        }
    }
}
