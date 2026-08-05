using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Sim.Match
{
    /// <summary>
    /// battle-sim-extraction unit 14 — 웨이브 스케줄과 스폰 대기열을 소유한다.
    ///
    /// **부작용 없음**: 로그·Debug 출력·엔티티 생성을 하지 않는다. "무엇을 언제 스폰할지" 만
    /// 결정하고, 실제 스폰과 서술(로그)은 호출자(Bridge)가 한다. 새로 큐잉된 웨이브는
    /// <see cref="QueuedWaveNotice"/> 로 돌려주므로 호출자가 그것으로 로그를 만든다.
    ///
    /// **엔진 의존 잔재**: `Wassup.Data`(`WavePatternGenerator`·`GeneratedWavePlan`·`SpawnEntry`)를
    /// 참조하고 그 어셈블리는 `UnityEngine` 을 쓴다. 이 unit 의 범위는 **규칙의 이사**이고 데이터
    /// 계층의 엔진 분리는 unit 18(context port)이 맡는다. **이 파일은** `UnityEngine` 을 직접
    /// `using` 하지 않는 선까지 지킨다.
    ///
    /// ⚠ 폴더 전체가 그렇지는 않다 — `MatchPlacementRules` 는 `Vector2Int` 때문에
    /// `using UnityEngine` 을 갖는다. 허용 목록과 게이트는 `SimEngineIndependenceTests` 가 소유한다.
    ///
    /// SO 해석(어떤 플랜을 쓸지)은 이 타입이 하지 않는다. Bridge 가 authored/seed/legacy 를 골라
    /// 이미 해결된 <see cref="GeneratedWavePlan"/> 을 <see cref="Initialize"/> 로 넘긴다 — SO 를
    /// 규칙 안에 끌고 들어오지 않기 위한 분리다.
    /// </summary>
    public sealed class MatchWaveSchedule
    {
        /// 스폰 대기 항목. 원래 `BattleBridge` 의 private nested struct 였다.
        public struct PendingSpawnEntry
        {
            public SpawnEntry entry;
            public int deckIndex;
        }

        /// 새로 큐잉된 웨이브 1건의 서술용 정보. 호출자가 로그/기록으로 소비한다.
        public readonly struct QueuedWaveNotice
        {
            public readonly GeneratedWave wave;
            public readonly int spawnCount;
            public readonly bool forced;
            public readonly float elapsedSec;

            public QueuedWaveNotice(GeneratedWave wave, int spawnCount, bool forced, float elapsedSec)
            {
                this.wave = wave;
                this.spawnCount = spawnCount;
                this.forced = forced;
                this.elapsedSec = elapsedSec;
            }
        }

        private readonly List<PendingSpawnEntry> _pending = new();
        private GeneratedWavePlan _plan;
        private bool _usesGeneratedWaves;
        private bool _usesAuthoredPlan;
        private int _nextWaveIndex;
        private float _waveTimeShift;
        private bool _clearReady;

        // spawn-point-alert unit 3 — **마지막으로 큐잉된 웨이브**의 lane 별 첫 스폰 절대 시각.
        // 미래 웨이브 예측이 아니라 QueueWave 가 큐잉 시점에 실제 스폰 base 로 1회 계산해 넣는다 —
        // 실스폰과 어긋날 여지가 없고, 자동/강제/Wave 1 이 모두 같은 경로라 리드인만큼의 창을
        // 똑같이 얻는다.
        private float[] _spawnAlertForecast;

        public GeneratedWavePlan Plan => _plan;
        public bool UsesGeneratedWaves => _usesGeneratedWaves;
        public bool UsesAuthoredPlan => _usesAuthoredPlan;
        public int NextWaveIndex => _nextWaveIndex;
        public int PendingCount => _pending.Count;
        public int WaveNumber => _nextWaveIndex + 1;
        public bool ClearReady => _clearReady;

        private int WaveCount => _plan.waves != null ? _plan.waves.Count : 0;
        public bool HasPlan => _usesGeneratedWaves && _plan.waves != null;

        /// <summary>
        /// 전멸 승리의 전제 — 덱의 모든 웨이브가 이미 큐잉되었는가. 생성 웨이브를 쓰지 않는
        /// (legacy 스폰) 판은 큐잉 개념이 없으므로 항상 true 다.
        /// </summary>
        public bool AllWavesQueued => !HasPlan || _nextWaveIndex >= WaveCount;

        /// wave-pattern unit 11 — 트리거와 첫 적 등장 사이의 리드인. **스폰 base 에만** 더한다.
        /// `ScheduledWaveTime`(트리거 그리드)·`_waveTimeShift` 산식에는 절대 넣지 않는다 —
        /// 섞으면 강제 호출 연타마다 리드인이 누적 왜곡된다.
        public float SpawnLeadInSec => _plan.spawnLeadInSec;

        /// 매치 경계 — 플랜·대기열·오프셋·예고 전부 소멸. 이전 판 예고 이월을 막는다.
        public void Reset()
        {
            _pending.Clear();
            _plan = default;
            _usesGeneratedWaves = false;
            _usesAuthoredPlan = false;
            _nextWaveIndex = 0;
            _waveTimeShift = 0f;
            _clearReady = false;
            _spawnAlertForecast = null;
        }

        /// <summary>
        /// battle-score-formula 계약 9 / wave-pattern unit 9 — **전투 시계와 짝**인 상태만 되돌린다
        /// (teardown 없이 `StartBattle` 이 다시 불리는 경로가 있다). 플랜과 인덱스는 유지한다.
        /// </summary>
        public void ResetClockPairedState()
        {
            _waveTimeShift = 0f;
            _spawnAlertForecast = null;
            _clearReady = false;
        }

        /// 클리어 강조 래치만 내린다(전투 teardown — 스케줄 자체는 건드리지 않는다).
        public void ClearReadyOff() => _clearReady = false;

        /// 이미 해결된 플랜을 받는다. `authored` 는 타이머 출처 결정에만 쓰인다.
        public void Initialize(GeneratedWavePlan plan, bool authored)
        {
            _plan = plan;
            _usesAuthoredPlan = authored;
            _usesGeneratedWaves = plan.waves != null && plan.waves.Count > 0;
            _nextWaveIndex = 0;
            _waveTimeShift = 0f;
            _spawnAlertForecast = null;
        }

        /// 생성 웨이브를 못 쓰는 판의 legacy 스폰 주입(덱의 spawns 를 그대로 대기열에).
        public void SeedLegacySpawns(IReadOnlyList<SpawnEntry> spawns)
        {
            if (spawns == null) return;
            for (int i = 0; i < spawns.Count; i++)
                _pending.Add(new PendingSpawnEntry { entry = spawns[i], deckIndex = i });
        }

        /// 작성 플랜은 자기 타이머를 갖고, 그 외에는 덱 타이머를 쓴다.
        public float ResolveTimerDurationSec(float deckTimerDurationSec)
            => _usesAuthoredPlan ? _plan.timerDurationSec : deckTimerDurationSec;

        /// wave-pattern unit 9 — 런타임 예정 시각 = 플랜 시각 + 강제 호출 누적 오프셋.
        /// 스케줄을 읽는 모든 지점(자동 큐잉·강제 호출)이 이 창구를 쓴다.
        ///
        /// **private 이다**(리뷰 반영): 가드 없이 `_plan.waves[i]` 를 인덱싱하므로 플랜 없는 상태의
        /// 외부 호출은 예외가 된다. 스케줄은 `QueueDueWaves`/`TryForceNextWave` 로만 진행시킨다.
        private float ScheduledWaveTime(int waveIndex)
            => _plan.waves[waveIndex].triggerTimeSec + _waveTimeShift;

        /// <summary>
        /// 예정 시각이 지난 웨이브를 순서대로 큐잉한다. 큐잉된 것은 `notices` 에 append 된다
        /// (null 이면 서술을 버린다).
        /// </summary>
        public void QueueDueWaves(float elapsedSec, int laneCount, List<QueuedWaveNotice> notices)
        {
            if (!HasPlan) return;
            while (_nextWaveIndex < WaveCount &&
                   elapsedSec + 0.0001f >= ScheduledWaveTime(_nextWaveIndex))
            {
                QueueWave(_plan.waves[_nextWaveIndex],
                    ScheduledWaveTime(_nextWaveIndex) + SpawnLeadInSec, false, elapsedSec,
                    laneCount, notices);
                _nextWaveIndex++;
            }
        }

        /// <summary>
        /// 다음 웨이브 강제 호출. 받아들여지면 true.
        ///
        /// **비멱등**: `_waveTimeShift` 가 누적 재기준되므로 연타 순서가 결과를 바꾼다(기존 계약).
        /// wave-pattern unit 9 — 앞당긴 만큼 남은 웨이브 전체를 같이 민다. 오프셋이 균일해 웨이브
        /// 간 간격이 보존되므로 다음 웨이브는 "지금 + 원래 간격" 에 나온다. **인덱스 증가 전에**
        /// 계산해야 한다(밀 대상 = 지금 강제 호출하는 웨이브).
        /// </summary>
        public bool TryForceNextWave(float elapsedSec, int laneCount, List<QueuedWaveNotice> notices)
        {
            if (!HasPlan) return false;
            if (_nextWaveIndex >= WaveCount) return false;

            GeneratedWave wave = _plan.waves[_nextWaveIndex];
            _waveTimeShift -= ScheduledWaveTime(_nextWaveIndex) - elapsedSec;
            // unit 11 — 강제 호출도 리드인을 따른다(당긴 웨이브의 첫 적도 리드인 뒤에 나온다).
            QueueWave(wave, elapsedSec + SpawnLeadInSec, true, elapsedSec, laneCount, notices);
            _nextWaveIndex++;
            return true;
        }

        private void QueueWave(GeneratedWave wave, float baseTriggerTimeSec, bool forced,
            float elapsedSec, int laneCount, List<QueuedWaveNotice> notices)
        {
            // 자동/강제 모두 같은 진입점. UI Update 순서와 무관하게 이전 클리어 강조를 즉시 내리고,
            // pending/live 가 다시 빌 때만 호출자가 RefreshClearReady 로 재활성한다.
            _clearReady = false;
            List<SpawnEntry> entries = WavePatternGenerator.ExpandWave(
                wave, baseTriggerTimeSec, laneCount, _plan.intraWaveSpacingSec);
            int baseDeckIndex = wave.waveIndex * WavePatternGenerator.DeckIndexStride;
            for (int i = 0; i < entries.Count; i++)
                _pending.Add(new PendingSpawnEntry { entry = entries[i], deckIndex = baseDeckIndex + i });

            // 예고는 **이 웨이브의 실제 스폰 base** 로 계산한다(예측 아님).
            _spawnAlertForecast = WavePatternGenerator.FirstSpawnTimesPerLane(
                wave, baseTriggerTimeSec, laneCount, _plan.intraWaveSpacingSec);

            notices?.Add(new QueuedWaveNotice(wave, entries.Count, forced, elapsedSec));
        }

        /// <summary>
        /// 트리거 시각이 지난 대기 스폰을 대기열에서 빼서 `into` 에 담는다.
        ///
        /// **역순 순회를 유지해야 한다** — 이것이 프레임 내 스폰 순서를 정하고, 스폰 순서는
        /// 엔티티 생성 순서를 통해 sim 결과에 들어간다(골든이 이 순서를 고정하고 있다).
        /// </summary>
        public void TakeDueSpawns(float clockSec, List<PendingSpawnEntry> into)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                if (clockSec < _pending[i].entry.triggerTimeSec) continue;
                into.Add(_pending[i]);
                _pending.RemoveAt(i);
            }
        }

        /// <summary>
        /// nextwave-clear-attention unit 0 — 최종 승리와 웨이브 사이 클리어가 공유하는 "비어 있음"
        /// 의 정본. 대기열(호출됐지만 아직 안 나온 적)은 이 타입이 알고, 필드의 생존 적은 ECS 질의
        /// 소유자만 알므로 `noAliveAttackers` 로 받는다.
        /// </summary>
        public bool NoQueuedAttackersRemain(bool noAliveAttackers)
            => PendingEmpty && noAliveAttackers;

        /// 호출자가 ECS 질의 **전에** 단축 평가할 수 있게 대기열 상태만 따로 노출한다(리뷰 M2).
        public bool PendingEmpty => _pending.Count == 0;

        public void RefreshClearReady(bool running, bool noAliveAttackers)
            => _clearReady = NextWaveAvailable(running) && _nextWaveIndex < WaveCount
                && _nextWaveIndex > 0 && NoQueuedAttackersRemain(noAliveAttackers);

        public bool NextWaveAvailable(bool running) => running && HasPlan;
        public bool NextWaveHasNext(bool running) => NextWaveAvailable(running) && _nextWaveIndex < WaveCount;

        /// UI 가 읽는 클리어 강조. 래치(`_clearReady`)에 **현재도 다음 웨이브가 있는지**를 곱한다.
        public bool ClearReadyForUi(bool running)
            => NextWaveHasNext(running) && _nextWaveIndex > 0 && _clearReady;

        /// <summary>
        /// 마지막 큐잉 웨이브의 lane 별 첫 스폰 시각. 미래 스폰이 남아 있는 동안만 서빙한다 —
        /// 웨이브의 뒷 lane 들은 늦게 나오므로 **마지막** lane 스폰까지 유지해야 뒷 lane 예고가
        /// 자기 유닛보다 먼저 사라지지 않는다. 반환 배열은 캐시 참조라 수정 금지.
        /// </summary>
        public bool TryGetSpawnAlertForecast(float clockSec, out float[] laneFirstSpawnSec)
        {
            laneFirstSpawnSec = null;
            if (_spawnAlertForecast == null) return false;
            if (LastSpawnSec(_spawnAlertForecast) <= clockSec) return false;
            laneFirstSpawnSec = _spawnAlertForecast;
            return true;
        }

        private static float LastSpawnSec(float[] laneFirstSpawnSec)
        {
            float last = -1f;
            for (int i = 0; i < laneFirstSpawnSec.Length; i++)
                if (laneFirstSpawnSec[i] > last) last = laneFirstSpawnSec[i];
            return last;
        }
    }
}
