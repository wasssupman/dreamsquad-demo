using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using UnityEngine;
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
            _shadowPendingSpawns.Clear();
            _shadowUnmappedLogged.Clear();
            _shadowMirroredCount = 0;
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

        private void ShadowEndMatch()
        {
            _shadow = null;
            _shadowPendingSpawns.Clear();
        }

        // ── 스폰 미러링 (18-N) ────────────────────────────────────────────────

        private readonly List<(Unity.Entities.Entity live, int simId)> _shadowPendingSpawns
            = new List<(Unity.Entities.Entity, int)>();
        private readonly HashSet<System.Type> _shadowUnmappedLogged = new HashSet<System.Type>();

        /// <summary>
        /// `AttachSimEntityId` 에서 부른다 — **모든 추적 스폰이 지나는 유일 지점**이라
        /// 여기서 적으면 ordinal 정렬이 **구조적으로** 보장된다(경로별 훅 7개를 두면 순서가
        /// 어긋날 자유도가 생긴다).
        ///
        /// ⚠ **여기서 복사하지 않는다.** 보스는 attach **뒤에** `BakeNightmareMechanics` 가
        /// 버퍼를 더 붙인다(`BattleBridge.cs` 그 주석 참조) — 지금 복사하면 그걸 놓친다.
        /// 복사는 P0 flush(<see cref="ShadowFlushSpawns"/>)에서 일괄로 한다.
        /// </summary>
        private void ShadowRecordSpawn(Unity.Entities.Entity entity, int simId)
        {
            if (_shadow == null || entity == Unity.Entities.Entity.Null) return;
            _shadowPendingSpawns.Add((entity, simId));
        }

        /// <summary>
        /// P0 — 라이브 프레임 준비가 끝난 뒤, sim 그룹이 보기 **직전**에 일괄 미러한다.
        ///
        /// ⚠ **죽은 엔티티도 id 를 소모한다.** 구 카운터는 이미 그 번호를 발급했으므로,
        /// 건너뛰면 뒤따르는 유닛의 ordinal 이 전부 밀려 **다른 판**이 된다.
        /// ⇒ 만들고 즉시 파괴한다(`_nextId` 는 감소하지 않으므로 번호가 보존된다).
        /// </summary>
        private void ShadowFlushSpawns()
        {
            if (_shadow == null || _shadowPendingSpawns.Count == 0) return;
            Wassup.Sim.SimWorld w = _shadow.World;

            for (int i = 0; i < _shadowPendingSpawns.Count; i++)
            {
                (Unity.Entities.Entity live, int simId) = _shadowPendingSpawns[i];
                Wassup.Sim.SimEntityId se = w.Create();

                // 순번 계약을 **여기서** 단정한다 — 어긋난 채 진행하면 골든이 갈릴 때까지 모른다.
                if (se.SpawnOrdinal != simId)
                    throw new InvalidOperationException(
                        $"[Shadow] 스폰 순번 불일치: live simId={simId} · shadow ordinal={se.SpawnOrdinal}. " +
                        "그림자가 추적 공간에서 별도로 엔티티를 만들었다는 뜻이다(CreateInternal 누락).");

                if (!HasLiveEntityManager() || !_em.Exists(live)) { w.Destroy(se); continue; }
                ShadowMirrorComponents(live, se);
                _shadowMirroredCount++;
            }
            _shadowPendingSpawns.Clear();
        }

        /// <summary>
        /// 그림자가 **실제로 채워졌는지**의 관측점. 골든 초록은 copier 가 라이브를 깨지 않았다는
        /// 증거일 뿐 — copier 가 조용히 아무것도 안 해도 라이브가 골든을 만들기 때문에 초록이다.
        /// 그 사실은 A/B 비교(18-Q)를 붙일 때까지 드러나지 않으므로, 매치당 한 줄을 남긴다.
        /// </summary>
        private void ShadowLogSummary()
        {
            if (_shadow == null) return;
            Debug.Log($"[Shadow] mirrored={_shadowMirroredCount} · ordinals={_shadow.World.SpawnedCount}" +
                      $" · alive={_shadow.World.AliveCount} · internal={_shadow.World.InternalSpawnedCount}" +
                      $" · liveCounter={_simEntityIdCounter} · pending={_shadowPendingSpawns.Count}" +
                      $" · tick={_shadow.World.Tick}");
        }

        private int _shadowMirroredCount;

        /// <summary>
        /// **presence-driven 복사.** 라이브 아키타입이 들고 있는 것을 그대로 옮긴다 —
        /// 조건부 컴포넌트가 자동으로 처리되고, 경로별 차이(적/방어/해저드/투사체)가
        /// "어떤 컴포넌트가 붙어 있나" 뿐이라 **이 함수 하나가 전 경로를 덮는다.**
        ///
        /// ⚠ **bake 가 아니라 copy 인 것이 의도다**(18-N 판정): bake 는 스폰 로직을 sim 쪽에
        /// 다시 쓰는 일이라 A/B 가 갈렸을 때 "규칙이 틀렸나 / bake 가 틀렸나" 를 구분할 수 없다.
        /// copy 는 그림자를 **정확히 같은 초기 상태**에서 출발시켜 이후 불일치를 전부
        /// **이식된 규칙**의 것으로 만든다.
        /// </summary>
        private void ShadowMirrorComponents(Unity.Entities.Entity live, Wassup.Sim.SimEntityId se)
            => ShadowMirror.MirrorEntity(_em, live, _shadow.World, se, ResolveShadowEntity,
                oldT =>
                {
                    if (_shadowUnmappedLogged.Add(oldT))
                        Debug.LogError($"[Shadow] 미매핑 컴포넌트 — 그림자에 상태가 빠진다: {oldT.FullName}. " +
                                       "18-M 스윕 장부(`SimTypeParitySweepTests`)를 먼저 갱신할 것.");
                });

        /// <summary>
        /// 라이브 `Entity` 참조 → 그림자 핸들. 구 simId 를 거쳐 간다(= `SpawnOrdinal + 1`).
        /// 해석 불가(Null·미등록)는 `Null` — 구 트레이스가 `sim:-1` 로 찍던 그 상태다.
        /// </summary>
        private Wassup.Sim.SimEntityId ResolveShadowEntity(Unity.Entities.Entity e)
            => TryGetSimId(e, out int simId) ? new Wassup.Sim.SimEntityId(simId + 1)
                                            : Wassup.Sim.SimEntityId.Null;

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

    /// <summary>
    /// battle-sim-extraction unit 18-N — **값 미러의 기계 부품.**
    ///
    /// 구 컴포넌트를 신 컴포넌트로 옮긴다. 규칙은 하나: **신 타입의 필드 이름으로 구 값을 찾는다.**
    /// 그 이름들이 같다는 것은 18-M(`SimTypeParitySweepTests`)이 155쌍에서 증명했고, **이 클래스는
    /// 그 오라클을 전제로만 성립한다** — 스윕이 빨개지면 여기부터 의심할 것.
    ///
    /// 방향이 "신 필드를 훑는다" 인 것이 계약이다: 구에만 있는 필드(`LocalTransform.Rotation`)가
    /// **자동으로 탈락**한다. 반대로 훑으면 sim 에 없는 필드에서 터진다.
    ///
    /// ⚠ 리플렉션 금지 원칙과 충돌하지 않는다 — 그 금지의 근거는 *"신 타입에 리플렉션을 걸면
    /// **타입 이름**이 신 것으로 나온다"*(트레이스 키)였고, 여기서 옮기는 것은 **값**이다.
    ///
    /// ⚠ 모르는 모양을 만나면 **던진다.** 조용히 기본값을 남기면 그림자가 다른 초기 상태에서
    /// 출발하고, 그 사실이 골든이 갈릴 때까지 드러나지 않는다.
    /// </summary>
    internal static class ShadowMirror
    {
        /// <summary>
        /// **네임스페이스를 건너는 쌍** — 동명 매칭이 못 잡는 것들. 여기 없으면 그 컴포넌트는
        /// 조용히 미러되지 않는다.
        ///
        /// ⚠ `LocalTransform` 이 그 사례이고 실제로 이 테이블 없이 한 번 빠졌다:
        /// 네임스페이스가 `Unity.Transforms` 라 Wassup 표면 스캔에 걸리지 않아 **미러된 유닛에
        /// 위치가 없었다.** 18-M 장부는 `SimTransform` 을 new-only 로 적고 *"대조는
        /// `SimLegacyTraceContractTests` 소유"* 라고만 했다 — 필드 대조의 소유자를 적은 것이지
        /// 미러 배선을 적은 것이 아니었다. 장부의 "다른 데서 본다" 는 **다른 축까지 덮지 않는다.**
        /// </summary>
        private static readonly Dictionary<Type, Type> CrossNamespacePairs = new Dictionary<Type, Type>
        {
            [typeof(Unity.Transforms.LocalTransform)] = typeof(Wassup.Sim.Movement.SimTransform),
        };

        /// 미러 대상 — Wassup 표면 + 위 명시 쌍. 그 밖(Unity.*·프레젠테이션)은 조용히 건너뛴다.
        internal static bool IsCandidate(Type t)
            => CrossNamespacePairs.ContainsKey(t)
               || (t.Namespace != null
                   && (t.Namespace == "Wassup.Data" || t.Namespace == "Wassup.Battle"
                       || t.Namespace.StartsWith("Wassup.Battle.", StringComparison.Ordinal)));

        /// <summary>
        /// 명시 비대상 — **이유가 없으면 넣지 않는다.** 여기 넣는 것은 그림자에서 그 상태를
        /// 포기하는 것이고, 포기가 정당한 경우만 있다.
        /// </summary>
        internal static readonly HashSet<Type> Skip = new HashSet<Type>
        {
            // 순번 그 자체 — 그림자에서는 `SimEntityId` 핸들이 이 정보를 담는다(18-K/2a).
            typeof(Wassup.Battle.Units.SimEntityId),
            // 시계 스케일 싱글턴. 그림자는 스케일된 dt 를 P0 에서 받는다 — 처분은 unit 19.
            typeof(Wassup.Battle.BattleTimeScale),
        };

        internal static bool IsEnableable(Type t)
            => typeof(Unity.Entities.IEnableableComponent).IsAssignableFrom(t);

        // ── 엔티티 단위 미러 (presence-driven) ────────────────────────────────

        /// <summary>
        /// 라이브 아키타입이 들고 있는 것을 그대로 옮긴다. **경로별 차이가 "어떤 컴포넌트가
        /// 붙어 있나" 뿐이라 이 함수 하나가 전 스폰 경로를 덮는다**(18-N/O/P).
        ///
        /// ⚠ 이것이 `BattleBridge` 인스턴스 메서드가 아니라 static 인 이유: 첫 구현은 인스턴스
        /// 안에 있었고, 그래서 **크기 0 태그 버그를 EditMode 가 잡을 수 없어 골든 14세션을
        /// 태웠다**(`GetComponentData<AttackUnitTag>` → `ArgumentException`). 라이브 ECS 월드는
        /// EditMode 에서도 만들 수 있으므로, 루프가 static 이면 그 등급의 버그는 여기서 걸린다.
        /// </summary>
        internal static void MirrorEntity(Unity.Entities.EntityManager em, Unity.Entities.Entity live,
                                          Wassup.Sim.SimWorld w, Wassup.Sim.SimEntityId se,
                                          Func<Unity.Entities.Entity, Wassup.Sim.SimEntityId> resolve,
                                          Action<Type> onUnmapped)
        {
            using Unity.Collections.NativeArray<Unity.Entities.ComponentType> types =
                em.GetComponentTypes(live, Unity.Collections.Allocator.Temp);

            for (int i = 0; i < types.Length; i++)
            {
                Unity.Entities.ComponentType ct = types[i];
                Type oldT = ct.GetManagedType();
                if (oldT == null || !IsCandidate(oldT) || Skip.Contains(oldT)) continue;

                if (!TypeMap.TryGetValue(oldT, out Type newT)) { onUnmapped?.Invoke(oldT); continue; }

                // ⚠ enableable 3상태 → 신 sim 2상태(존재/부재) 접힘. **비활성 = 부재**다 —
                //   스폰 시 부착+비활성인 `ModifierStatsDirty` 를 그대로 Set 하면 그림자가
                //   첫 틱에 가짜 재집계를 돈다(Battle 의 유일한 enableable).
                if (IsEnableable(oldT) && !em.IsComponentEnabled(live, ct)) continue;

                if (ct.IsBuffer)
                {
                    System.Collections.IList target = AddSimBuffer(w, se, newT);
                    if (target == null) continue;
                    foreach (object element in ReadBuffer(em, live, oldT))
                        target.Add(ConvertStruct(element, newT, resolve));
                }
                // ⚠ **크기 0 태그는 값을 읽지 않는다.** `GetComponentData<T>` 는 필드가 없는
                //   타입에 `ArgumentException` 을 던진다(패키지 문서 명시) — 첫 골든 실행이
                //   `AttackUnitTag` 에서 정확히 그렇게 죽었다. 태그는 **존재가 내용 전부**다.
                else if (ct.IsZeroSized)
                {
                    SetSimComponent(w, se, newT, Activator.CreateInstance(newT));
                }
                else
                {
                    object oldVal = ReadComponent(em, live, oldT);
                    if (oldVal != null) SetSimComponent(w, se, newT, ConvertStruct(oldVal, newT, resolve));
                }
            }
        }

        // ── 타입 맵 (18-M 과 같은 동명 매칭) ──────────────────────────────────

        private static Dictionary<Type, Type> _typeMap;

        internal static Dictionary<Type, Type> TypeMap => _typeMap ?? (_typeMap = BuildTypeMap());

        private static bool IsMirrorableStruct(Type t)
            => t.IsValueType && !t.IsEnum && !t.IsPrimitive && t.IsPublic && !t.IsNested
               && !t.Name.Contains("<")
               && !typeof(Unity.Entities.ISystem).IsAssignableFrom(t);

        private static Dictionary<Type, Type> BuildTypeMap()
        {
            var newByName = new Dictionary<string, Type>();
            foreach (Type t in typeof(Wassup.Sim.SimWorld).Assembly.GetTypes())
            {
                if (!IsMirrorableStruct(t) || t.Namespace == null) continue;
                if (!t.Namespace.StartsWith("Wassup.Sim", StringComparison.Ordinal)) continue;
                newByName[t.Name] = t;   // 동명 유일성은 18-M 이 단정한다
            }

            // 명시 쌍이 먼저 — 동명 매칭이 이것을 덮어쓸 일은 없다(이름이 다르니까).
            var map = new Dictionary<Type, Type>(CrossNamespacePairs);
            var assemblies = new HashSet<Assembly>
            {
                typeof(Wassup.Battle.Units.Health).Assembly,
                typeof(Wassup.Data.PatternSpec).Assembly,
            };
            foreach (Assembly a in assemblies)
            {
                foreach (Type t in a.GetTypes())
                {
                    if (!IsMirrorableStruct(t) || !IsCandidate(t)) continue;
                    if (newByName.TryGetValue(t.Name, out Type newT)) map[t] = newT;
                }
            }
            return map;
        }

        // ── 라이브 읽기 / 그림자 쓰기 (제네릭 메서드 캐시) ────────────────────

        private static readonly Dictionary<Type, MethodInfo> GetComponentCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> GetBufferCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> SetCache = new Dictionary<Type, MethodInfo>();
        private static readonly Dictionary<Type, MethodInfo> AddBufferCache = new Dictionary<Type, MethodInfo>();

        private static MethodInfo Closed(Dictionary<Type, MethodInfo> cache, Type key, Func<MethodInfo> open)
        {
            if (cache.TryGetValue(key, out MethodInfo m)) return m;
            return cache[key] = open().MakeGenericMethod(key);
        }

        /// <summary>
        /// ⚠ **`Invoke` 는 실제 예외를 `TargetInvocationException` 으로 감싼다.** 골든 러너는
        /// `ex.Message` 만 저장하므로 그대로 두면 *"Exception has been thrown by the target of an
        /// invocation."* 라는 **아무 정보 없는 실패**가 남는다 — 첫 실행이 정확히 그랬고, 원인
        /// (크기 0 태그)을 로그가 아니라 패키지 소스를 읽어서 찾아야 했다. 여기서 벗겨 둔다.
        /// </summary>
        private static object InvokeUnwrapped(MethodInfo mi, object target, object[] args)
        {
            try { return mi.Invoke(target, args); }
            catch (TargetInvocationException tie) when (tie.InnerException != null)
            {
                throw new InvalidOperationException(
                    $"[Shadow] {mi.Name}<{string.Join(",", mi.GetGenericArguments().Select(t => t.Name))}> 실패: " +
                    tie.InnerException.Message, tie.InnerException);
            }
        }

        internal static object ReadComponent(Unity.Entities.EntityManager em,
                                             Unity.Entities.Entity e, Type oldT)
        {
            MethodInfo mi = Closed(GetComponentCache, oldT, () => typeof(Unity.Entities.EntityManager)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(x => x.Name == "GetComponentData" && x.IsGenericMethodDefinition
                            && x.GetParameters().Length == 1
                            && x.GetParameters()[0].ParameterType == typeof(Unity.Entities.Entity)));
            return InvokeUnwrapped(mi, em, new object[] { e });
        }

        internal static IEnumerable<object> ReadBuffer(Unity.Entities.EntityManager em,
                                                       Unity.Entities.Entity e, Type oldT)
        {
            MethodInfo mi = Closed(GetBufferCache, oldT, () => typeof(Unity.Entities.EntityManager)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(x => x.Name == "GetBuffer" && x.IsGenericMethodDefinition
                            && x.GetParameters().Length == 2));
            object buffer = InvokeUnwrapped(mi, em, new object[] { e, true });
            if (buffer == null) yield break;

            Type bt = buffer.GetType();
            int length = (int)bt.GetProperty("Length").GetValue(buffer);
            PropertyInfo item = bt.GetProperty("Item");
            for (int i = 0; i < length; i++) yield return item.GetValue(buffer, new object[] { i });
        }

        internal static void SetSimComponent(Wassup.Sim.SimWorld w, Wassup.Sim.SimEntityId e,
                                             Type newT, object value)
        {
            MethodInfo mi = Closed(SetCache, newT, () => typeof(Wassup.Sim.SimWorld)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(x => x.Name == "Set" && x.IsGenericMethodDefinition));
            InvokeUnwrapped(mi, w, new object[] { e, value });
        }

        internal static System.Collections.IList AddSimBuffer(Wassup.Sim.SimWorld w,
                                                              Wassup.Sim.SimEntityId e, Type newT)
        {
            MethodInfo mi = Closed(AddBufferCache, newT, () => typeof(Wassup.Sim.SimWorld)
                .GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .First(x => x.Name == "AddBuffer" && x.IsGenericMethodDefinition));
            return InvokeUnwrapped(mi, w, new object[] { e }) as System.Collections.IList;
        }

        // ── 값 변환 ──────────────────────────────────────────────────────────

        /// 신 타입의 필드를 훑어 동명 구 필드에서 값을 끌어온다(구 전용 필드는 자동 탈락).
        internal static object ConvertStruct(object oldVal, Type newT,
                                             Func<Unity.Entities.Entity, Wassup.Sim.SimEntityId> resolve)
        {
            object box = Activator.CreateInstance(newT);
            Type oldT = oldVal.GetType();
            foreach (FieldInfo nf in newT.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                FieldInfo of = oldT.GetField(nf.Name, BindingFlags.Instance | BindingFlags.Public);
                if (of == null) continue;   // 신에만 있는 필드 — 기본값을 남긴다
                nf.SetValue(box, ConvertValue(of.GetValue(oldVal), nf.FieldType, resolve));
            }
            return box;
        }

        private static object ConvertValue(object v, Type target,
                                           Func<Unity.Entities.Entity, Wassup.Sim.SimEntityId> resolve)
        {
            if (v == null) return null;
            Type src = v.GetType();
            if (src == target) return v;

            if (target.IsEnum)
                return Enum.ToObject(target, Convert.ToInt64(v, CultureInfo.InvariantCulture));

            // ⚠ 엔티티 참조는 **구 simId 를 거쳐** 옮긴다 — 라이브 Entity 번호는 그림자에서 뜻이 없다.
            if (v is Unity.Entities.Entity ent && target == typeof(Wassup.Sim.SimEntityId))
                return resolve(ent);

            if (v is Unity.Mathematics.float3 f3 && target == typeof(Wassup.Sim.SimVec3))
                return new Wassup.Sim.SimVec3(f3.x, f3.y, f3.z);
            if (v is Unity.Mathematics.float2 f2 && target == typeof(Wassup.Sim.SimVec2))
                return new Wassup.Sim.SimVec2(f2.x, f2.y);
            if (v is Unity.Mathematics.int2 i2 && target == typeof(Wassup.Sim.SimInt2))
                return new Wassup.Sim.SimInt2(i2.x, i2.y);

            // 컨테이너 → 관리 배열. `Length` + 인덱서만 쓰므로 NativeArray·FixedList 를 함께 덮는다.
            if (target.IsArray && src.IsGenericType)
            {
                PropertyInfo lengthProp = src.GetProperty("Length");
                PropertyInfo item = src.GetProperty("Item");
                if (lengthProp == null || item == null)
                    throw new InvalidOperationException($"[Shadow] 열거 불가 컨테이너: {src.Name} → {target.Name}");
                int n = (int)lengthProp.GetValue(v);
                Type et = target.GetElementType();
                Array dst = Array.CreateInstance(et, n);
                for (int i = 0; i < n; i++)
                    dst.SetValue(ConvertValue(item.GetValue(v, new object[] { i }), et, resolve), i);
                return dst;
            }

            // 남은 것은 중첩 struct — 재귀(`Random`→`SimRandom` 도 `state` 동명으로 여기서 처리된다).
            if (target.IsValueType && !target.IsPrimitive)
                return ConvertStruct(v, target, resolve);

            throw new InvalidOperationException(
                $"[Shadow] 값 변환 규칙이 없다: {src.FullName} → {target.FullName}. " +
                "조용히 기본값을 남기면 그림자가 다른 초기 상태에서 출발한다.");
        }
    }
}
