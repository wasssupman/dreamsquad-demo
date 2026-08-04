using System.Collections.Generic;
using Wassup.Data;

namespace Wassup.Battle.Effects
{
    // battle-sim-extraction unit 11(선행 머지 2) — 스택 임계 규칙의 sim 소유 레지스트리.
    //
    // 이전에는 StackModifierTickSystem 이 `BattleBridge.GetStackThresholds(kind)` 를 직접 불렀다.
    // 그것이 **sim → Bridge 프로덕션 결합의 유일한 지점**이었고, asmdef 의존 방향
    // (sim 은 Unity/Bridge 를 모른다)의 마지막 위반이었다. 방향을 뒤집어 규칙을 sim 이 소유하고
    // Bridge 는 저작 SO(`stackModifierAuthoring`)를 매치 시작 시 **등록만** 한다.
    //
    // 관리 Dictionary 를 유지하므로 소비 시스템의 non-Burst 제약은 그대로다 — 값 타입 blob 화는
    // M1 의 MatchConfig 물질화 몫이고(청사진 ② config-singleton 규칙), 이 머지의 목적은
    // **결합 방향**이지 자료구조 교체가 아니다(행동 변화 0).
    public static class StackThresholdRegistry
    {
        private static readonly Dictionary<StackKind, ThresholdRule[]> Rules = new();

        // 매치 경계에서 Bridge 가 호출(재등록 전 초기화).
        public static void Clear() => Rules.Clear();

        public static void Register(StackKind kind, ThresholdRule[] rules)
            => Rules[kind] = rules ?? System.Array.Empty<ThresholdRule>();

        // 미등록 kind 는 빈 배열 — 호출부(DispatchThresholds)가 "규칙 없음 = 임계 미발동"으로 읽는다.
        public static ThresholdRule[] Get(StackKind kind)
            => Rules.TryGetValue(kind, out var rules) ? rules : System.Array.Empty<ThresholdRule>();
    }
}
