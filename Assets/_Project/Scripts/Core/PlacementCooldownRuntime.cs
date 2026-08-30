using System.Collections.Generic;
using UnityEngine;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Core
{
    // defender-placement-cooldown 0 — 배치 성공 후 유닛 타입별 재배치 대기(초)를 소유·틱하는
    // 런타임. CostRuntime 를 미러한다: MonoBehaviour(비싱글턴), Battle 도메인 시계로 self-tick
    // (슬로모 감속·메뉴 정지 동결). GameManager.CooldownRuntime 로 도달. 쿨타임은 순수 Mono/UI
    // 관심사라 ECS/BattleBridge 로 내려가지 않는다(맥락 경계).
    //
    // "0 = inert": placementCooldown==0 유닛은 StartCooldown 이 no-op 이라 등록조차 안 되고,
    // 전 유닛이 쿨타임 밖이면 Update 가 즉시 조기 반환한다(분기 안 탐).
    public class PlacementCooldownRuntime : MonoBehaviour
    {
        private struct Entry
        {
            public float remaining;
            public float total;
        }

        private readonly Dictionary<DefenderUnitData, Entry> _map = new();
        // Tick 중 Dictionary 수정을 위한 키 스냅샷 버퍼. 매 tick Clear 후 재사용(새 할당 금지).
        private readonly List<DefenderUnitData> _tickKeys = new();

        // 활성 쿨타임이 하나라도 있는가 — 트레이가 프레임 순회를 스킵하는 가드.
        public bool AnyActive => _map.Count > 0;

        // 배치 성공 시 호출. unit==null 또는 seconds<=0 이면 no-op(등록 안 함) → "0 = inert".
        // 같은 유닛 재배치 시 full 리셋(overwrite).
        public void StartCooldown(DefenderUnitData unit, float seconds)
        {
            if (unit == null || seconds <= 0f) return;
            _map[unit] = new Entry { remaining = seconds, total = seconds };
        }

        // 남은 초(없으면 0).
        public float RemainingFor(DefenderUnitData unit)
        {
            return unit != null && _map.TryGetValue(unit, out var e) ? e.remaining : 0f;
        }

        public bool IsReady(DefenderUnitData unit) => RemainingFor(unit) <= 0f;

        // 남은 비율 1→0(오버레이 레이디얼 fillAmount 용). 없거나 total<=0 이면 0.
        public float Fraction(DefenderUnitData unit)
        {
            if (unit != null && _map.TryGetValue(unit, out var e) && e.total > 0f)
                return Mathf.Clamp01(e.remaining / e.total);
            return 0f;
        }

        // 전부 소거(배치 페이즈 진입 / 매치 teardown 경계).
        public void ResetAll() => _map.Clear();

        // defender-footprint unit 5 — 배치 취소 유예의 되감기. 실수 복구가 목적이라 재시도가
        // 쿨타임에 막히면 안 된다. ⚠ 같은 map 을 사망·퇴근 쿨이 공유하므로 무조건 지우면 안 된다 —
        // maxOnBoard ≥ 2 에서 «X#2 배치 → X#1 사망(같은 키 덮어씀) → 취소»가 사망 쿨을 지운다
        // (리뷰 H-1). 현재 항목의 total(건 순간의 길이)이 배치 쿨 이하일 때만 지운다 — total 은
        // «어느 쿨인가»의 근사 식별자다. 값이 우연히 같아 오식별해도 비용은 쿨 하나가 일찍 풀리는 것뿐.
        public void ClearCooldownUpTo(Wassup.Data.DefenderUnitData unit, float totalSecondsAtMost)
        {
            if (unit == null) return;
            if (_map.TryGetValue(unit, out var e) && e.total <= totalSecondsAtMost + 0.001f)
                _map.Remove(unit);
        }

        // 각 entry remaining 감소, <=0 은 제거. Update 에서 추출 — EditMode 가 직접 구동
        // (MonoBehaviour.Update 는 EditMode 미실행). Entry 가 struct 라 재대입으로 갱신한다.
        public void Tick(float dt)
        {
            if (_map.Count == 0) return;
            _tickKeys.Clear();
            foreach (var k in _map.Keys) _tickKeys.Add(k);
            for (int i = 0; i < _tickKeys.Count; i++)
            {
                var k = _tickKeys[i];
                var e = _map[k];
                e.remaining -= dt;
                if (e.remaining <= 0f) _map.Remove(k);
                else _map[k] = e;
            }
        }

        // Battle 도메인 시계로 self-tick(CostRuntime 와 동일). _map 비면 즉시 반환해
        // 쿨타임 전무 시 ScaleOf 루프·빈 dict 순회조차 돌지 않는다("0 = inert").
        private void Update()
        {
            // battle-sim-extraction M0 unit 2 — 하네스에서는 스텝이 부른다(CostRuntime 와 동일 이유:
            // 이 쿨타임도 배치 입력을 게이트하는 시뮬 상태다).
            if (SimHarnessClock.Active) return;
            if (_map.Count == 0) return;
            Tick(TimeManager.Instance.DeltaTime(TimeDomain.Battle));
        }
    }
}
