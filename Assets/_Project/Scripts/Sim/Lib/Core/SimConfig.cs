using System;
using System.Collections.Generic;

namespace Wassup.Sim
{
    /// <summary>
    /// battle-sim-extraction unit 18-A — sim 이 매치 시작에 받는 **저작 스냅샷**.
    ///
    /// **왜 18-A 에 있나**(critic M5): 이게 없으면 `StackModifierTick`(18-C, S2)이 임계 규칙을
    /// 조회할 곳이 없어 **6세션 동안 조용히 no-op** 이 된다. 구 sim 의
    /// `StackThresholdRegistry.Get()` 은 미등록 kind 에 빈 배열을 돌려주고 그건 "규칙 없음 =
    /// 임계 미발동" 이라는 **정상 상태**다 — 즉 배선이 빠진 것과 규칙이 없는 것이 **구분되지
    /// 않는다**. 그래서 그림자 sim 이 내내 초록인 채로 아무 임계도 안 터진다.
    ///
    /// **처방은 내용이 아니라 강제다.** `SimConfig` 없이는 sim 을 만들 수 없게 한다 —
    /// 18-C 가 조각을 얹을 때 config 를 관통시키지 않으면 **컴파일이 안 된다**. 내용은 조각이
    /// 자기 규칙을 옮길 때 채운다(지금 118 타입의 저작 입력을 다 설계하면 아무도 안 읽는 표가 된다).
    ///
    /// **엔진 타입을 담지 않는다** — `StackKind` 같은 `Wassup.Battle.Effects` enum 은 여기 못 온다.
    /// 저작 계층이 int 로 풀어서 넘기고, 그 대응표는 18-K 의 주입 지점이 소유한다.
    /// </summary>
    public sealed class SimConfig
    {
        /// <summary>스택 임계 1행. `kind` 는 저작 enum 을 푼 정수다(sim 은 그 enum 을 모른다).</summary>
        public readonly struct StackThreshold
        {
            public readonly int Kind;
            public readonly byte Count;      // 이 개수를 **넘는 순간** 파생(엣지 교차)
            public readonly int DerivedId;   // 파생 효과 식별자 — 해석은 조각이 소유
            public StackThreshold(int kind, byte count, int derivedId)
            { Kind = kind; Count = count; DerivedId = derivedId; }
        }

        private readonly Dictionary<int, StackThreshold[]> _stackThresholds;

        /// <summary>`PickupSpawnState.rng` 초기 시드 — `DerivePickupSeed(matchSeed)`(청사진 ③ §4).</summary>
        public uint PickupSeed { get; }

        /// <summary>
        /// `BombLauncherState.rng` 의 **기저**. 실제 시드는 `max(1, base ^ cellHash)` 로
        /// **캐스터별로 갈린다** — 그 파생은 소비 지점(18-I)이 하고 여기서는 기저만 준다.
        /// </summary>
        public uint BombSeedBase { get; }

        public SimConfig(uint pickupSeed, uint bombSeedBase,
                         IReadOnlyList<StackThreshold> stackThresholds = null)
        {
            PickupSeed = pickupSeed;
            BombSeedBase = bombSeedBase;
            _stackThresholds = new Dictionary<int, StackThreshold[]>();
            if (stackThresholds == null) return;

            var byKind = new Dictionary<int, List<StackThreshold>>();
            for (int i = 0; i < stackThresholds.Count; i++)
            {
                var t = stackThresholds[i];
                if (!byKind.TryGetValue(t.Kind, out var l)) byKind[t.Kind] = l = new List<StackThreshold>();
                l.Add(t);
            }
            foreach (var kv in byKind) _stackThresholds[kv.Key] = kv.Value.ToArray();
        }

        /// <summary>
        /// 그 kind 의 임계 목록. **없으면 빈 배열** — 구 `StackThresholdRegistry.Get()` 과 같은
        /// 계약이다("규칙 없음 = 임계 미발동" 은 정상 상태이므로 여기서 던지지 않는다).
        ///
        /// 배선 누락과 규칙 부재의 구분은 **이 조회가 아니라 생성자가** 진다 — config 없이는
        /// sim 이 만들어지지 않는다.
        /// </summary>
        public IReadOnlyList<StackThreshold> StackThresholdsFor(int kind)
            => _stackThresholds.TryGetValue(kind, out var a) ? a : Array.Empty<StackThreshold>();

        public int StackKindCount => _stackThresholds.Count;
    }
}
