using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Battle.Combat;
using Wassup.Battle.Units;
using Wassup.Bridge;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // ⚠ 진단용 임시 재현 — 「Whirlpot 이 멈추기만 하고 데미지를 안 넣는다」.
    //
    // EditMode 순수 sim 재현(WhirlpotEngageRepro)에서는 **정상 동작한다** — FSM 이 Engaging 에
    // 닿고 인접 방어유닛이 맞는다. 그러므로 AttackSystem/FSM/타게팅/outputs 는 무죄이고,
    // 끊긴 곳은 **베이크 또는 라이브 상태**다. 여기서 경계별로 찍어 위치를 잡는다.
    public class WhirlpotLiveRepro
    {
        private const string WhirlpotPath = "Assets/_Project/Data/Enemies/Enemy_Whirlpot.asset";

        [TearDown] public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Whirlpot_AdjacentDefender_TakesDamage()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var ranger = cat.ById("ranger");

            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "방어유닛 배치 실패");
            var defender = FirstDefender(em);
            Assert.AreNotEqual(Entity.Null, defender, "방어유닛 엔티티를 찾지 못했다");

            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(bridge, em, so);
            Assert.AreNotEqual(Entity.Null, whirlpot, "Whirlpot 스폰 실패");

            // ── 경계 0: 베이크 ───────────────────────────────────────────────
            Assert.IsTrue(em.HasComponent<AttackState>(whirlpot),
                "★AttackState 가 없다 = 무장 해제로 베이크됐다(wantsAttack false). 걷기만 한다.");
            var atk = em.GetComponentData<AttackState>(whirlpot);
            Assert.AreEqual(10, atk.attackTargetCount, "attackTargetCount 가 저작값과 다르다");
            Assert.AreEqual(2f, atk.range, 0.001f, "range 가 저작값과 다르다");
            Assert.AreNotEqual(0, atk.targetMask, "targetMask 0 = 아무도 후보로 안 본다");
            Assert.IsTrue(em.HasBuffer<AttackOutputElement>(whirlpot),
                "★outputs 버퍼가 없다 = 때려도 아무 효과가 없다");
            Assert.Greater(em.GetBuffer<AttackOutputElement>(whirlpot).Length, 0, "outputs 비었다");
            Assert.IsTrue(em.HasComponent<EnemyAiState>(whirlpot), "EnemyAiState 미부착");

            // 방어유닛 바로 위로 옮긴다 — 이동/경로를 변수에서 제거하고 «사거리 안» 만 만든다.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            em.SetComponentData(whirlpot, LocalTransform.FromPosition(defPos));

            float hpBefore = em.GetComponentData<Health>(defender).value;

            // ── 경계 1: FSM ──────────────────────────────────────────────────
            AiState seenState = AiState.Marching;
            bool sawEngaging = false;
            float hpAfter = hpBefore;
            for (int i = 0; i < 40; i++)
            {
                yield return null;
                if (!em.Exists(whirlpot) || !em.Exists(defender)) break;
                seenState = em.GetComponentData<EnemyAiState>(whirlpot).value;
                if (seenState == AiState.Engaging || seenState == AiState.Standoff) sawEngaging = true;
                hpAfter = em.GetComponentData<Health>(defender).value;
                if (hpAfter < hpBefore) break;
            }

            Assert.IsTrue(sawEngaging,
                $"★FSM 이 40프레임 동안 한 번도 Engaging/Standoff 에 못 갔다(마지막={seenState}). "
                + "그러면 AttackSystem 의 stateAllowsFire 가 false 라 영영 발사하지 않는다.");

            // ── 경계 2: 피해 ─────────────────────────────────────────────────
            Assert.Less(hpAfter, hpBefore,
                $"★붙어 있는 방어유닛의 HP 가 40프레임 동안 그대로다({hpBefore}). = 보고된 증상 재현. "
                + $"FSM 상태={seenState}");
        }

        // 인접 배치는 통과한다. 남은 변수는 «걸어와서 멈추는» 구간이다 — 사용자 문장의
        // 「인식범위에 적이 있으면 멈춰있다」가 그 구간을 가리킨다. 궤적을 찍는다.
        [UnityTest]
        public IEnumerator Whirlpot_WalksIn_ThenEngages()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var ranger = cat.ById("ranger");

            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "방어유닛 배치 실패");
            var defender = FirstDefender(em);

            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(bridge, em, so);
            Assert.AreNotEqual(Entity.Null, whirlpot, "Whirlpot 스폰 실패");

            // 방어유닛에서 5타일 떨어뜨린다(tileSize=1) — 마지막 접근 구간만 재현한다.
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            em.SetComponentData(whirlpot,
                LocalTransform.FromPosition(defPos + new Unity.Mathematics.float3(5f, 0f, 0f)));

            float hp0 = em.GetComponentData<Health>(defender).value;
            var trace = new System.Text.StringBuilder();
            float minDist = float.MaxValue;
            bool damaged = false;

            for (int i = 0; i < 400; i++)
            {
                yield return null;
                if (!em.Exists(whirlpot) || !em.Exists(defender)) { trace.Append("[소멸]"); break; }

                var p = em.GetComponentData<LocalTransform>(whirlpot).Position;
                var d = em.GetComponentData<LocalTransform>(defender).Position;
                float dist = Unity.Mathematics.math.max(
                    Unity.Mathematics.math.abs(p.x - d.x), Unity.Mathematics.math.abs(p.z - d.z));
                minDist = Unity.Mathematics.math.min(minDist, dist);
                float hp = em.GetComponentData<Health>(defender).value;
                if (hp < hp0) { damaged = true; }

                if (i % 40 == 0 || damaged)
                    trace.Append($"f{i} d={dist:F2} {em.GetComponentData<EnemyAiState>(whirlpot).value} hp={hp:F0} | ");
                if (damaged) break;
            }

            Assert.IsTrue(damaged,
                $"★걸어온 Whirlpot 이 400프레임 동안 방어유닛을 한 대도 못 때렸다. "
                + $"최소접근={minDist:F2}타일(사거리 2) 궤적: {trace}");
        }

        // 피해는 3번 재현에서 전부 들어갔다. 그러면 「아무 일도 안 일어나는 느낌」의 출처는
        // **피드백**일 수 있다. Whirlpot 은 attack 애니가 빈 값이라 몸이 반응하지 않으므로
        // 화면의 유일한 신호가 회오리 VFX 하나다 — 그게 실제로 뜨는지 센다.
        [UnityTest]
        public IEnumerator Whirlpot_Attack_SpawnsWhirlVfx()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var spawner = Object.FindObjectOfType<Wassup.Presentation.VfxSpawner>();
            Assert.IsNotNull(spawner, "VfxSpawner 없음");

            var cat = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var ranger = cat.ById("ranger");
            bridge.SetDefenderPool(new[] { ranger });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, ranger), "방어유닛 배치 실패");
            var defender = FirstDefender(em);

            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            Assert.IsNotNull(so.attackVfxPrefab, "회오리 프리팹 미배선");
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(bridge, em, so);

            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            em.SetComponentData(whirlpot, LocalTransform.FromPosition(defPos));

            int before = spawner.transform.childCount;
            float hp0 = em.GetComponentData<Health>(defender).value;
            bool damaged = false;
            int maxChildren = before;

            for (int i = 0; i < 60; i++)
            {
                yield return null;
                if (!em.Exists(defender)) break;
                maxChildren = Mathf.Max(maxChildren, spawner.transform.childCount);
                if (em.GetComponentData<Health>(defender).value < hp0) damaged = true;
            }

            Assert.IsTrue(damaged, "선행 조건: 공격 자체가 성사돼야 한다");
            Assert.Greater(maxChildren, before,
                "★공격은 성사됐는데 VfxSpawner 밑에 회오리 인스턴스가 하나도 안 생겼다. "
                + "Whirlpot 은 attack 애니가 빈 값이라 화면의 유일한 신호가 이 VFX 다 — "
                + "안 뜨면 「멈춰서 아무것도 안 한다」로 보인다.");
        }

        // 사용자 관측: 「먼데 서서 아무것도 안 하는 느낌 · 회오리는 보였다」.
        // 공격은 성사되고 있다는 뜻이므로 남은 질문은 «연출이 판정만큼 큰가» 다.
        // 판정 = Chebyshev 2 = 5×5 타일. 연출 = attackRange(2) × scalePerTile(0.5) = 1.0.
        [UnityTest]
        public IEnumerator WhirlVfx_VisualSize_MatchesHitRadius()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var spawner = Object.FindObjectOfType<Wassup.Presentation.VfxSpawner>();

            // VfxSpawner 와 같은 계산으로 인스턴스를 만든다(브리지가 넘기는 인자 그대로).
            float s = so.attackRange * so.attackVfxScalePerTile;
            var go = Object.Instantiate(so.attackVfxPrefab, Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one * s;
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main; main.loop = true; main.playOnAwake = true;
                ps.Clear(true); ps.Play(true);
            }
            for (int i = 0; i < 45; i++) yield return null;   // 파티클이 차오를 시간

            var rends = go.GetComponentsInChildren<ParticleSystemRenderer>(true);
            Assert.Greater(rends.Length, 0, "파티클 렌더러 없음");
            var b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);

            // tileSize = 1 (FlowField 로그로 확인됨). 판정은 중심에서 ±2 타일 = 폭 5.
            float widthTiles = Mathf.Max(b.size.x, b.size.z);
            Object.Destroy(go);

            Assert.GreaterOrEqual(widthTiles, 4f,
                $"★회오리 연출 폭이 {widthTiles:F2}타일인데 판정은 5×5타일이다(반경 2). "
                + $"localScale={s} (attackRange {so.attackRange} × scalePerTile {so.attackVfxScalePerTile}). "
                + "연출이 판정보다 작으면 「회오리가 안 닿는 곳의 유닛이 깎인다」가 되고, "
                + "팽이는 사거리 2 라 애초에 대상에서 2타일 떨어져 멈추므로 "
                + "「멀리 서서 아무것도 안 하는」 그림이 된다.");
        }

        // 앞선 테스트들은 「한 대는 때린다」만 봤다(HP 감소 즉시 break). 「계속 때리는가」는
        // 별개 질문이고, 「아무것도 안 하는 느낌」이 여기서 갈린다 — 한 번 때리고 멈추면
        // 저작 8.3 DPS 보다 훨씬 낮게 나온다.
        [UnityTest]
        public IEnumerator Whirlpot_SustainsDps_NotJustOneHit()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return BattleBridgeTestAccess.LoadBattleScene();

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            // 탱커를 쓴다 — 3초 안에 죽지 않아야 측정이 끊기지 않는다(배스티온 2070).
            var tank = cat.ById("bastion") ?? cat.ById("guardian");
            bridge.SetDefenderPool(new[] { tank });
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart(); gm.CostRuntime.AddCost(1000);
            yield return null;

            Assert.IsTrue(PlaceFirstValid(bridge, tank), "방어유닛 배치 실패");
            var defender = FirstDefender(em);

            bridge.StartBattle();
            for (int i = 0; i < 2; i++) yield return null;

            var so = BattleBridgeTestAccess.LoadEnemy(WhirlpotPath);
            var whirlpot = BattleBridgeTestAccess.SpawnEnemy(bridge, em, so);
            var defPos = em.GetComponentData<LocalTransform>(defender).Position;
            em.SetComponentData(whirlpot, LocalTransform.FromPosition(defPos));

            // ★웨이브 1(Debuffer 3 · Sniper 3)이 같은 방어유닛을 때린다 — 초판 측정이
            // 그것 때문에 23 DPS 로 부풀었다. 팽이 외의 적을 계속 치워 측정을 격리한다.
            PurgeOtherEnemies(em, whirlpot);

            // 첫 타를 기다린 뒤부터 잰다(스폰~교전 진입 지연을 측정에서 뺀다).
            float hpStart = em.GetComponentData<Health>(defender).value;
            for (int i = 0; i < 120 && em.GetComponentData<Health>(defender).value >= hpStart; i++)
            {
                PurgeOtherEnemies(em, whirlpot);
                yield return null;
            }

            hpStart = em.GetComponentData<Health>(defender).value;
            float t0 = Time.time;
            // 6초 — 3초 창은 «4회냐 5회냐» 의 위상 오차가 20% 로 보인다.
            for (float t = 0f; t < 6f; t += Time.deltaTime)
            {
                PurgeOtherEnemies(em, whirlpot);
                yield return null;
            }
            float elapsed = Time.time - t0;

            // ★측정 도중 팽이가 죽으면 DPS 가 조용히 낮게 나온다(배스티온이 반격한다).
            Assert.IsTrue(em.Exists(whirlpot) && !em.HasComponent<DeadTag>(whirlpot)
                          && em.GetComponentData<Health>(whirlpot).value > 0f,
                "측정 창 안에서 Whirlpot 이 죽었다 — 이 회차의 DPS 는 신뢰할 수 없다.");
            float dealt = hpStart - em.GetComponentData<Health>(defender).value;
            float dps = dealt / elapsed;

            // 기대치는 **SO 에서 유도한다** — 리터럴로 박으면 밸런스를 조정할 때마다 낡는다.
            float authoredDps = so.outputs[0].magnitude / so.attackCooldown;
            Assert.Greater(dps, authoredDps * 0.5f,
                $"★{elapsed:F1}초 동안 {dealt:F0} 피해 = {dps:F1} DPS. 저작은 {authoredDps:F1} DPS "
                + $"(magnitude {so.outputs[0].magnitude} / cooldown {so.attackCooldown}) 다. "
                + "절반 미만이면 회오리가 연타되지 않고 간헐적으로만 성사되는 것 — "
                + "「돌고 있는데 아무 일도 안 일어난다」의 실체다.");

            // 관측값을 로그로 남긴다(통과해도 수치를 봐야 밸런스 판단이 된다).
            Debug.Log($"[WhirlpotRepro] 실측 {dps:F2} DPS / 저작 {authoredDps:F2} DPS · {elapsed:F2}초 {dealt:F0} 피해");
        }

        // 팽이 외의 «적» 을 정상 사망 경로로 치운다(HP 0 → DeathSystem). DestroyEntity 를
        // 직접 부르면 브리지의 뷰/등록부 정리를 건너뛴다.
        private static void PurgeOtherEnemies(EntityManager em, Entity keep)
        {
            var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<AttackUnitTag>(),
                ComponentType.ReadOnly<FactionTag>(),
                ComponentType.ReadWrite<Health>());
            var ents = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            for (int i = 0; i < ents.Length; i++)
            {
                if (ents[i] == keep) continue;
                if (em.GetComponentData<FactionTag>(ents[i]).value != Faction.EnemyUnit) continue;
                var h = em.GetComponentData<Health>(ents[i]);
                if (h.value <= 0f) continue;
                h.value = 0f;
                em.SetComponentData(ents[i], h);
            }
            ents.Dispose();
        }

        // ── helpers ─────────────────────────────────────────────────────────
        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
            return false;
        }

        private static Entity FirstDefender(EntityManager em)
        {
            var q = em.CreateEntityQuery(
                ComponentType.ReadOnly<DefenderUnitTag>(),
                ComponentType.ReadOnly<Health>());
            var arr = q.ToEntityArray(Unity.Collections.Allocator.Temp);
            var r = arr.Length > 0 ? arr[0] : Entity.Null;
            arr.Dispose();
            return r;
        }
    }
}
