using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Core;   // GameManager(배틀 로그 기록)
using Wassup.Data;

namespace Wassup.Bridge
{
    // bonus-wave-pull units 4~6 — 보너스 당기기.
    //
    // 일반 당김 옆(위)에 조건부로 뜨는 두 번째 버튼이 만드는 웨이브다. 기존 웨이브 생성과
    // **코드 경로를 공유하지 않는다**(README 계약 1): WavePatternGenerator·AttackDeck·
    // _wavePlan·_pending 무접촉이고, 여기가 자기 큐(_bonusPending)와 자기 타임라인을 갖는다.
    //
    // 이 partial 로 분리한 이유는 BossLeap 과 같다 — BattleBridge.cs 는 여러 세션이 동시
    // 편집한다. 공유 파일에는 상태 선언·리셋 호출·펌프 호출만 남기고 나머지는 전부 여기 둔다.
    public partial class BattleBridge
    {
        [Header("Bonus wave portal (bonus-wave-pull unit 6)")]
        [Tooltip("전투 중 열리는 보너스 포탈 프리팹. 미할당이면 포탈 없이 스폰만 일어난다.")]
        [SerializeField] private GameObject bonusPortalPrefab;

        // 포탈 뷰는 풀이 아니라 보너스 웨이브 수명이다(거점 뷰 SpawnStructureViews 선례).
        private readonly List<GameObject> _bonusPortalViews = new();

        // unit 11 — 온보딩 판 억제. 판 시작 **전에** 밖에서 주입되는 설정이라
        // ResetBonusWaveState() 에서 지우지 않는다(그 리셋은 BeginPlacement·StartBattle
        // 양쪽에서 불려서, 여기 넣으면 GameManager 가 켠 억제가 판 시작에 지워진다).
        private bool _bonusPullSuppressed;

        // 온보딩 판은 배치·철수·부착을 가르치는 자리다. 조건부 두 번째 버튼이 얹히면
        // 배우는 축이 하나 늘어난다. 호출은 GameManager 가 매 판 무조건 한다(그래야
        // 온보딩 이후 판이 true 를 물려받지 않는다).
        public void SetBonusPullSuppressed(bool suppressed) => _bonusPullSuppressed = suppressed;

        // ── 상태 리셋 ────────────────────────────────────────────────────────────
        // _pending.Clear() 가 있는 **두 곳**(BeginPlacement·StartBattle) 양쪽에서 불린다.
        // 한쪽만 걸면 재시작 시 옛 보너스 웨이브가 잔존한다.
        private void ResetBonusWaveState()
        {
            _bonusPending.Clear();
            _bonusWaveActive = false;
            _bonusPortalCloseAtSec = 0f;
            _bonusPortalOpenAtSec = 0f;
            _bonusPortalOpened = false;
            _normalKillCount = 0;
            _bonusConsumedKillMark = 0;
            _bonusOfferLatched = false;
            ClearBonusPortalViews();
        }

        // ── 읽기 창구 ────────────────────────────────────────────────────────────
        private bool BonusWaveAuthored =>
            bonusWaveData != null
            && bonusWaveData.enemyUnit != null
            && _generatedMap.IsCreated
            && _generatedMap.bonusSpawns.IsCreated
            && _generatedMap.bonusSpawns.Length > 0;

        // heart-stress-axis 연동 — 마음 체력을 «차오르는 스트레스»로 읽는다. 산식의 정본은
        // StressMath 하나이고 여기는 브리지 미러(_goalStability, SyncGoalStability 가 매 프레임
        // 갱신)를 그 함수에 넘길 뿐이다. 마음 미저작 맵은 max 0 → 스트레스 0(fail-open).
        public float CurrentStress =>
            Wassup.Core.StressMath.FromHealth(GoalStabilityCurrent, GoalStabilityMax);

        // unit 9 — 래치. 「스트레스 30 이하에서 등장」은 **등장 조건이지 유지 조건이 아니다**.
        // 매 프레임 재평가하면 문턱 근처에서 버튼이 떨린다(맞으면 오르고 잡으면 내려가는 값이다).
        // 전이는 순수 함수가 소유하고 여기는 그 결과를 들고만 있는다.
        private bool _bonusOfferLatched;

        // 계약 12 — 크레딧은 **일반 적 처치 수**로만 찬다. 보너스 적을 세면 실효 임계가
        // (N − enemyCount) 로 내려가고 N ≤ enemyCount 에서는 발산한다.
        // 계약 13 — 진행 중에는 뜨지 않는다(크레딧은 계속 쌓인다).
        public bool BonusPullAvailable =>
            _running
            && !_bonusPullSuppressed
            && BonusWaveAuthored
            && !_bonusWaveActive
            && _bonusOfferLatched;

        // 래치 갱신 — TickBattleFrame 안에서 매 프레임. 스트레스가 프레임 값이라 여기서 본다.
        private void TickBonusPullOffer()
        {
            // 진행 중에는 래치를 **켜지 않는다.** 안 그러면 웨이브 도는 동안 스트레스가
            // 잠깐 내려간 것만으로 래치가 서고, 웨이브가 끝나는 순간 스트레스가 80 이어도
            // 버튼이 뜬다 — 「등장 시점의 스트레스로 판정한다」가 거짓이 된다.
            if (!BonusWaveAuthored || _bonusWaveActive) { _bonusOfferLatched = false; return; }
            _bonusOfferLatched = BonusPullTrigger.NextLatched(
                _bonusOfferLatched,
                _normalKillCount, _bonusConsumedKillMark, bonusWaveData.killThreshold,
                CurrentStress, bonusWaveData.maxStressToOffer);
        }

        // 크레딧은 찼는데 스트레스 때문에 막혀 있나 — 「왜 안 뜨지」에 답할 수 있는 유일한 신호.
        // unit 11 — 억제된 판에서는 이 신호도 꺼진다. 켜두면 「스트레스 때문에 막혔다」는
        // 거짓 진단이 된다 — 그 판은 스트레스와 무관하게 기능 자체가 없다.
        public bool BonusPullBlockedByStress =>
            _running && !_bonusPullSuppressed && BonusWaveAuthored && !_bonusWaveActive && !_bonusOfferLatched
            && BonusPullTrigger.HasCredit(
                _normalKillCount, _bonusConsumedKillMark, bonusWaveData.killThreshold);

        // ── 규칙 층 / 기제 층 ────────────────────────────────────────────────────
        // 일반 당김(TryPullNextWave / ForceNextWave)과 같은 2층 구조다.
        // **플레이어 경로는 TryBonusPull 하나뿐이다** — UI 에서 ForceBonusWave 를 직접
        // 부르면 트리거와 동시 1벌 규칙이 함께 우회된다.
        public bool TryBonusPull()
        {
            if (!BonusPullAvailable) return false;
            // ★기제가 실패하면 **크레딧을 쓰지 않는다.** void 로 두면 스케줄이 비어 아무것도
            // 안 나왔는데 30킬이 사라지고 로그만 남는다(그리고 진행 플래그가 안 서서 다음
            // 프레임에 버튼이 다시 뜬다 → 크레딧이 찰 때마다 같은 일이 반복된다).
            if (!ForceBonusWave()) return false;
            // ★**한 회분만 소비한다.** `= _normalKillCount` 로 두면 스트레스에 막혀 쌓인
            // 초과 크레딧이 통째로 증발한다(스트레스 높은 채 90킬 → 3회분이 1회로). 크레딧은
            // 자원이고 스트레스는 그것을 쓰는 창일 뿐이라, 창이 닫혀 있었다고 자원이 사라지면 안 된다.
            _bonusConsumedKillMark += bonusWaveData.killThreshold;
            // 크레딧이 남아 있으면 다음 프레임의 TickBonusPullOffer 가 다시 래치한다.
            _bonusOfferLatched = false;
            // H2 — 배틀 로그에 흔적을 남긴다. 안 남기면 랭킹 점수의 일부가 어디서 왔는지
            // 사후 판독이 불가능하다(일반 당김의 wave_forced 와 같은 자리).
            GameManager.Instance?.Logger?.RecordWaveEvent(
                "bonus_pull", NextWaveNumber, (float)_battleClock, forced: true);
            return true;
        }

        // 기제. 트리거·동시 1벌을 보지 않는다(테스트·디버그 진입점) — 규칙은 위 층 소유.
        // 반환값 = 「실제로 웨이브를 열었나」. 규칙 층이 이걸 봐야 실패한 당김이 크레딧을 먹지 않는다.
        public bool ForceBonusWave()
        {
            if (!_running || !BonusWaveAuthored) return false;

            int portalCount = _generatedMap.bonusSpawns.Length;
            var schedule = BonusWaveSchedule.Build(
                portalCount,
                bonusWaveData.enemyCount,
                bonusWaveData.FirstSpawnAtSec,
                bonusWaveData.spawnIntervalSec);
            if (schedule.Length == 0)
            {
                // enemyCount 가 0 인 손상된 저작 — [Min(1)] 은 인스펙터 입력만 막는다.
                Debug.LogWarning("[BattleBridge] 보너스 웨이브 스케줄이 비었다 — " +
                                 "BonusWaveData.enemyCount 를 확인하라. 크레딧은 소비하지 않는다.");
                return false;
            }

            float now = (float)_battleClock;
            _bonusPending.Clear();
            for (int i = 0; i < schedule.Length; i++)
            {
                var e = schedule[i];
                _bonusPending.Add(new PendingBonusSpawn
                {
                    spawnAtSec = now + e.spawnAtSec,
                    cell = _generatedMap.bonusSpawns[e.portalIndex],
                    ringIndex = e.ringIndex,
                    ringCount = math.max(1, e.ringCount),
                });
            }

            _bonusWaveActive = true;
            _bonusPortalCloseAtSec =
                now + schedule[schedule.Length - 1].spawnAtSec + bonusWaveData.portalLingerSec;

            // 포탈은 스폰보다 먼저 열린다. 코루틴 대신 시각 비교로 여는 것은 배틀 도메인
            // 시계(정지·슬로우모 반영)를 쓰기 위해서다 — Time 기반이면 두 시계가 갈린다.
            _bonusPortalOpenAtSec = now + bonusWaveData.portalAppearDelaySec;
            _bonusPortalOpened = false;

            Debug.Log($"[BattleBridge] BONUS PULL — 포탈 {portalCount}개 · " +
                      $"{bonusWaveData.enemyCount}기 (t={now:F1}s)");
            return true;
        }

        private float _bonusPortalOpenAtSec;
        private bool _bonusPortalOpened;

        // ── 펌프 (TickBattleFrame 안에서 불린다) ─────────────────────────────────
        // ★Update 직하에 두면 sim 하네스(StepOneTick → TickBattleFrame)와 라이브가 갈린다.
        // 시각 기준은 Time 이 아니라 _battleClock 이다.
        private void TickBonusWave(float t)
        {
            if (!_bonusWaveActive) return;

            if (!_bonusPortalOpened && t >= _bonusPortalOpenAtSec)
            {
                OpenBonusPortals();
                _bonusPortalOpened = true;
            }

            for (int i = _bonusPending.Count - 1; i >= 0; i--)
            {
                if (t < _bonusPending[i].spawnAtSec) continue;
                SpawnBonusUnit(_bonusPending[i]);
                _bonusPending.RemoveAt(i);
            }

            // 마지막 스폰 + linger → 포탈을 닫고 웨이브를 종료한다. 적이 아직 살아 있어도
            // «웨이브» 는 끝난 것이다 — 계약 13 의 재진입 게이트는 여기서 풀린다.
            if (_bonusPending.Count == 0 && t >= _bonusPortalCloseAtSec)
            {
                ClearBonusPortalViews();
                _bonusWaveActive = false;
            }
        }

        private void SpawnBonusUnit(PendingBonusSpawn pending)
        {
            var unitType = bonusWaveData != null ? bonusWaveData.enemyUnit : null;
            if (unitType == null) return;

            // 겹침 오프셋 — **분열 레시피 복제**다(SpawnSplitChildren). 레인 스폰의
            // ComputeSpawnLateralOffset 은 래퍼(SpawnUnit) 전용이라 이 경로에 없고, 복제하지
            // 않으면 같은 포탈의 여러 기가 한 점에 태어나 좁은 복도에서 교착 조건에 들어간다.
            // 셀 중심 기준 + 반경 0.25 라 |오프셋| < 0.49 → 전부 같은 셀에 남는다(flow/goal 불변).
            Vector3 center = GridToWorldCenterVector(
                new Vector2Int(pending.cell.x, pending.cell.y), spawnHeight);
            float angle = (Mathf.PI * 2f * pending.ringIndex) / pending.ringCount;
            float radius = tileSize * 0.25f;
            var pos = new Vector3(
                center.x + Mathf.Cos(angle) * radius,
                center.y,
                center.z + Mathf.Sin(angle) * radius);

            // 레인·웨이포인트 없음 — 분열 자식과 같은 형태(-1, -1). 목적지는 순수하게
            // flow field 가 정하고, 방어유닛이 있으면 사냥 필드로 자동 전환된다(unit 0).
            var entity = CreateEnemyEntity(unitType, pos);
            if (entity == Unity.Entities.Entity.Null) return;
            _em.AddComponent<Wassup.Battle.Units.BonusWaveTag>(entity);
        }

        // ── 포탈 뷰 (unit 6) ─────────────────────────────────────────────────────
        // 거점 뷰(SpawnStructureViews / ClearStructureViews)와 같은 형태다 — 풀이 아니라
        // 리스트 등재 + teardown 공유. 프랍 파이프라인(MapThemeData.spawnStructureProp)을
        // 태우지 않아도 잃을 것이 없다: 그 PropData 는 billboardMode=None · visualScale=1 ·
        // visualOffset=0 이라 파이프라인이 붙여주던 것이 실질적으로 없다(2026-08-24 확인).
        private void OpenBonusPortals()
        {
            ClearBonusPortalViews();
            if (bonusPortalPrefab == null || !_generatedMap.bonusSpawns.IsCreated) return;

            for (int i = 0; i < _generatedMap.bonusSpawns.Length; i++)
            {
                int2 cell = _generatedMap.bonusSpawns[i];
                // sim 셀 → **뷰** 월드. 평면 tilemap 보드라 sim 좌표를 그대로 쓰면 어긋난다.
                Vector3 world = GridCellToViewCenter(new Vector2Int(cell.x, cell.y));
                var go = Instantiate(bonusPortalPrefab, world, Quaternion.identity, transform);
                go.name = $"BonusPortal_{cell.x}_{cell.y}";
                _bonusPortalViews.Add(go);
            }
        }

        // teardown 등재 필수 — 빠뜨리면 재시작 시 옛 포탈이 보드에 남는다(사직서·픽업 뷰 사고).
        private void ClearBonusPortalViews()
        {
            for (int i = 0; i < _bonusPortalViews.Count; i++)
                if (_bonusPortalViews[i] != null) Destroy(_bonusPortalViews[i]);
            _bonusPortalViews.Clear();
        }
    }
}
