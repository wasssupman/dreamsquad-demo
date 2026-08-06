using System.Collections.Generic;
using Wassup.Core;

namespace Wassup.Bridge
{
    /// <summary>
    /// battle-sim-extraction unit 18-K/5c — **그림자 sim 의 무장 지점.**
    ///
    /// ## 여기가 I1 의 예외다
    ///
    /// 18-A~18-J 의 모든 커밋은 `Scripts/Battle/**`·`Scripts/Bridge/**` 를 수정하지 않았고,
    /// **그것이 골든 byte diff 0 의 실제 근거**였다("돌려봤더니 같더라" 가 아니라 "건드린 파일이
    /// 없다"). 이 파일이 그 논증을 끝낸다 — 이후로는 골든이 진짜 증인이다.
    ///
    /// 그래서 **파일을 따로 둔다.** `BattleBridge.cs` 본체는 여기 정의된 메서드를 부르기만 하고
    /// `Wassup.Sim.*` 를 한 글자도 적지 않는다 ⇒ I2 검출기의 예외 목록이 **이 파일 하나**로
    /// 유지된다. 예외 목록이 길어지는 것은 I2 를 그만큼 포기하는 것이다.
    ///
    /// ## 수명: 만들기는 항상, 돌리기는 하네스일 때만
    ///
    /// 만들기는 <see cref="BeginPlacement"/> 끝이다 — `_simEntityIdCounter = 0` 과 **같은 매치
    /// 경계**이고(배치 페이즈가 defender 를 먼저 낳는다), 그 시점엔 `EnsureQueriesAndQueues`
    /// (스택 임계 레지스트리)와 `BuildMapForBattle`(기믹 config)이 **둘 다 끝나** 저작이 완성돼 있다.
    ///
    /// ⚠ **`HarnessActive` 로 생성을 막으면 안 된다.** 하네스는 `BeginHarness` 에서 켜지는데
    /// 그건 `BeginPlacement`·`StartBattle` **뒤**다(`RestartHarnessMatch` 참조) — 생성을 그
    /// 플래그에 걸면 첫 매치의 그림자가 조용히 안 만들어진다. 생성은 매치당 1회라 무시할 만한
    /// 비용이고, 실제로 비싼 **틱**만 하네스로 막는다.
    ///
    /// ## 지금은 무엇을 하지 않나
    ///
    /// 이 조각은 **config 주입과 수명**까지다. 스폰 미러링(③)·맵 싱글턴(④)·A/B 비교(⑤)는
    /// 뒤따르는 조각이다. 그래서 지금 그림자는 **빈 판을 돌린다** — 관측 가능한 부수효과가 0 이고,
    /// 골든은 byte 단위로 그대로여야 한다. 그 사실 자체가 이 커밋의 완료 기준이다.
    /// </summary>
    public partial class BattleBridge
    {
        private Wassup.Sim.SimRuntime _shadow;

        /// 진단·후속 조각용. 무장 전이거나 매치 밖이면 `null`.
        internal Wassup.Sim.SimRuntime Shadow => _shadow;

        /// <summary>
        /// 매치 경계. <see cref="BeginPlacement"/> 의 **끝**에서 부른다 — 저작이 완성된 뒤이고
        /// 첫 defender 배치보다 앞이다.
        /// </summary>
        private void ShadowBeginMatch()
        {
            _shadow = new Wassup.Sim.SimRuntime(BuildSimConfig());
            ShadowMirrorMapSingletons();
        }

        // ── 맵 싱글턴 미러링 (분류 C 가 기다리던 것) ───────────────────────────

        /// <summary>
        /// 라이브 월드 싱글턴을 그림자로 옮긴다. **재구축이 아니라 복사**다 —
        /// BFS·후보 셀 수집을 신 sim 에서 다시 구현하면 그 구현 자체가 새 오차원이 된다.
        /// 이미 지어진 결과를 베끼면 데이터가 같다는 것이 자명하다.
        ///
        /// ⚠ **홀더는 비추적 엔티티**(`CreateInternal`)여야 한다. 구 sim 에서 이 싱글턴들은
        /// `SimEntityId` 를 받지 않았고(`AppendUnkeyedComponents` 가 그 사실에 의존한다),
        /// 추적 공간에서 뽑으면 그 뒤 모든 유닛의 번호가 밀린다.
        ///
        /// ⚠ 무엇을 베끼고 무엇을 비워 두는지가 갈린다:
        /// <list type="bullet">
        /// <item>`FlowField`·`DefenderField`·`PickupSpawnState` = **Bridge 가 맵에서 지은 것** → 복사</item>
        /// <item>`Hazard`·`Obstacle` = **sim 이 매 틱 재빌드**(#2·#6) → 빈 홀더만</item>
        /// </list>
        /// `DefenderField` 의 `flow`/`dist` 도 매 틱 재빌드(#7)지만 배열은 **할당돼 있어야**
        /// 하므로(그 시스템은 크기를 신뢰한다) 함께 베낀다.
        /// </summary>
        private void ShadowMirrorMapSingletons()
        {
            if (_shadow == null || !HasLiveEntityManager()) return;
            Wassup.Sim.SimWorld w = _shadow.World;

            if (TryGetLiveSingleton(out Wassup.Battle.Effects.FlowFieldSingleton ff) && ff.IsCreated)
            {
                w.Set(w.CreateInternal(), new Wassup.Sim.Effects.FlowFieldSingleton
                {
                    flow = ToSimVec2(ff.flow),
                    dist = ToIntArray(ff.dist),
                    gridSize = ToSimInt2(ff.gridSize),
                    goalCell = ToSimInt2(ff.goalCell),
                    goals = ToSimInt2Array(ff.goals),
                    tileSize = ff.tileSize,
                    origin = ToSimVec3(ff.origin),
                    version = ff.version,
                });
            }

            if (TryGetLiveSingleton(out Wassup.Battle.Effects.DefenderFieldSingleton df) && df.IsCreated)
            {
                w.Set(w.CreateInternal(), new Wassup.Sim.Effects.DefenderFieldSingleton
                {
                    walkMask = ToByteArray(df.walkMask),
                    flow = ToSimVec2(df.flow),
                    dist = ToIntArray(df.dist),
                    gridSize = ToSimInt2(df.gridSize),
                    tileSize = df.tileSize,
                    origin = ToSimVec3(df.origin),
                });
            }

            // ⚠ 없으면 만들지 않는다 — 부재가 곧 "레드불 기믹 비활성" 이다(분류 B 와 같은 결).
            if (TryGetLiveSingleton(out Wassup.Battle.Effects.PickupSpawnState ps) && ps.IsCreated)
            {
                w.Set(w.CreateInternal(), new Wassup.Sim.Effects.PickupSpawnState
                {
                    candidateCells = ToSimInt2Array(ps.candidateCells),
                    elapsed = ps.elapsed,
                    // ⚠ `state` 를 그대로 옮긴다 — 생성자에 시드를 다시 먹이면 두 난수열이 갈린다
                    //   (`SimRandom(seed)` 와 `Unity.Mathematics.Random(seed)` 의 섞는 방식이 다르다).
                    rng = new Wassup.Sim.SimRandom { state = ps.rng.state },
                });
            }

            // 매 틱 재빌드분 — 홀더만 세운다. 구 sim 도 브리지가 컨테이너만 만들고 내용은
            // #2·#6 이 채웠다(그 게이트가 분류 C 다).
            w.Set(w.CreateInternal(), new Wassup.Sim.Effects.HazardSingleton
            {
                cellToEffects = new Wassup.Sim.Effects.HazardCellIndex(),
            });
            w.Set(w.CreateInternal(), new Wassup.Sim.Effects.ObstacleSingleton
            {
                blockedCells = new HashSet<Wassup.Sim.SimInt2>(),
            });
        }

        private bool TryGetLiveSingleton<T>(out T value) where T : unmanaged, Unity.Entities.IComponentData
        {
            using Unity.Entities.EntityQuery q =
                _em.CreateEntityQuery(Unity.Entities.ComponentType.ReadOnly<T>());
            if (q.IsEmptyIgnoreFilter) { value = default; return false; }
            value = q.GetSingleton<T>();
            return true;
        }

        // ── 엔진 타입 → sim 타입 (복사) ───────────────────────────────────────

        private static Wassup.Sim.SimInt2 ToSimInt2(Unity.Mathematics.int2 v)
            => new Wassup.Sim.SimInt2(v.x, v.y);

        private static Wassup.Sim.SimVec3 ToSimVec3(Unity.Mathematics.float3 v)
            => new Wassup.Sim.SimVec3(v.x, v.y, v.z);

        private static byte[] ToByteArray(Unity.Collections.NativeArray<byte> src)
        {
            if (!src.IsCreated) return null;
            var dst = new byte[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = src[i];
            return dst;
        }

        private static int[] ToIntArray(Unity.Collections.NativeArray<int> src)
        {
            if (!src.IsCreated) return null;
            var dst = new int[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = src[i];
            return dst;
        }

        private static Wassup.Sim.SimVec2[] ToSimVec2(Unity.Collections.NativeArray<Unity.Mathematics.float2> src)
        {
            if (!src.IsCreated) return null;
            var dst = new Wassup.Sim.SimVec2[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = new Wassup.Sim.SimVec2(src[i].x, src[i].y);
            return dst;
        }

        private static Wassup.Sim.SimInt2[] ToSimInt2Array(Unity.Collections.NativeArray<Unity.Mathematics.int2> src)
        {
            if (!src.IsCreated) return null;
            var dst = new Wassup.Sim.SimInt2[src.Length];
            for (int i = 0; i < src.Length; i++) dst[i] = ToSimInt2(src[i]);
            return dst;
        }

        /// <summary>
        /// 하네스 틱과 lockstep. ⚠ 라이브 sim 그룹과 **같은 dt** 여야 한다 — 시계가 갈리면
        /// 이후 모든 비교가 무의미하다.
        /// </summary>
        private void ShadowStepOneTick(float fixedDt)
        {
            if (_shadow == null || !TestModeContext.HarnessActive) return;
            _shadow.StepOneTick(fixedDt);
        }

        private void ShadowEndMatch() => _shadow = null;

        // ── 저작 스냅샷 주입 ──────────────────────────────────────────────────

        /// <summary>
        /// 라이브 저작 → `SimConfig`. **대응표를 소유하는 것이 이 지점의 역할**이다
        /// (`SimConfig` 주석: *"Battle enum 은 여기 못 온다 — 저작 계층이 옮겨 담는다"*).
        ///
        /// enum 은 `(byte)` 캐스트로 옮긴다. 지금 모든 쌍이 멤버 순서까지 같기 때문인데,
        /// 그건 **가정이 아니라 단정**이다 — `SimEnumParityTests` 가 지킨다. 그 테스트 없이
        /// 캐스트하면 누가 한쪽에 멤버를 끼워 넣는 순간 골든만 조용히 갈린다.
        /// </summary>
        private Wassup.Sim.SimConfig BuildSimConfig()
        {
            return new Wassup.Sim.SimConfig(
                pickupSeed: (uint)MatchSeed.DerivePickupSeed(_matchSeed),
                // ⚠ **기저**만 준다 — 실제 시드는 캐스터별로 `max(1, base ^ cellHash)` 다.
                //   그 파생은 소비 지점(#33)이 하고, 여기서 미리 섞으면 캐스터별 독립성이 깨진다.
                bombSeedBase: (uint)MatchSeed.DeriveBombSeed(_matchSeed),
                stackThresholds: BuildSimStackThresholds(),
                clockOut: BuildSimClockOutConfig());
        }

        /// <summary>
        /// 구 `BuildStackThresholdRegistry` 와 **같은 의미**로 평탄화한다.
        ///
        /// ⚠ **kind 중복은 마지막이 이긴다.** 구 `StackThresholdRegistry.Register` 가
        /// `Rules[kind] = rules` 로 **덮어쓰기**인데 `SimConfig` 는 kind 별로 **이어붙이므로**,
        /// 그냥 흘려보내면 같은 kind 를 저작한 SO 가 둘일 때 신 sim 만 규칙이 두 배가 된다.
        /// 여기서 먼저 덮어써서 의미를 맞춘다.
        ///
        /// ⚠ kind 안의 **저작 순서는 보존**한다 — 발화 루프가 `atStack` 오름차순을 신뢰하고
        /// Consume 모드는 발화 도중 스택을 깎는다.
        /// </summary>
        private List<Wassup.Sim.Effects.StackThresholdRule> BuildSimStackThresholds()
        {
            var lastWins = new Dictionary<Wassup.Battle.Effects.StackKind, Wassup.Data.ThresholdRule[]>();
            var order = new List<Wassup.Battle.Effects.StackKind>();
            if (stackModifierAuthoring != null)
            {
                foreach (Wassup.Data.StackModifierSO so in stackModifierAuthoring)
                {
                    if (so == null) continue;
                    if (!lastWins.ContainsKey(so.kind)) order.Add(so.kind);
                    lastWins[so.kind] = so.thresholds ?? System.Array.Empty<Wassup.Data.ThresholdRule>();
                }
            }

            var flat = new List<Wassup.Sim.Effects.StackThresholdRule>();
            foreach (Wassup.Battle.Effects.StackKind kind in order)
            {
                foreach (Wassup.Data.ThresholdRule r in lastWins[kind])
                {
                    flat.Add(new Wassup.Sim.Effects.StackThresholdRule
                    {
                        kind = (Wassup.Sim.Effects.StackKind)(byte)kind,
                        atStack = r.atStack,
                        mode = (Wassup.Sim.Effects.ThresholdMode)(byte)r.mode,
                        derivedKind = (Wassup.Sim.Effects.DerivedEffectKind)(byte)r.derivedKind,
                        magnitude = r.magnitude,
                        duration = r.duration,
                        stat = (Wassup.Sim.Effects.StatKind)(byte)r.stat,
                        op = (Wassup.Sim.Effects.CombineOp)(byte)r.op,
                        tickInterval = r.tickInterval,
                    });
                }
            }
            return flat;
        }

        /// <summary>
        /// ⚠ **`null` 이 곧 "기믹 비활성"** 이다 — 구 `RequireForUpdate&lt;ClockOutGimmickConfig&gt;`
        /// (분류 B 게이트)가 하던 일이 이 자리로 이사했다. 기믹을 끈 판과 켠 판은 다른 판이므로
        /// 여기서 기본값을 채워 넣으면 안 된다.
        /// </summary>
        private Wassup.Sim.Effects.ClockOutConfig BuildSimClockOutConfig()
        {
            if (!(_assignedGimmick is Wassup.Data.ClockOutGimmickData cd)) return null;
            return new Wassup.Sim.Effects.ClockOutConfig(
                cd.resignationThreshold, cd.meteorCount, cd.meteorDamage,
                cd.meteorTileRange, cd.meteorWarningSec, cd.meteorStaggerSec);
        }
    }
}
