using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;
using Wassup.Battle.Combat.Projectile;

namespace Wassup.Tests.PlayMode
{
    // on-place-skill-rework units 0~2 — **캐논 사슬의 끝**에서 한 번에 검증한다.
    // (2026-08-16 사용자 결정: unit 단위 PlayMode → 사슬 끝 1회. units 0·1 은 설계상
    //  단독으로 아무 동작도 안 해 관측점이 억지가 되고, 여기엔 자연스러운 관측점이 있다.)
    //
    // 한 번에 증명되는 것:
    //  unit 0 — 배치 사건이 OnPlace 슬롯을 발화시킨다(캐논은 에셋만 바뀌었다)
    //  unit 1 — scopeTileRange 가 후보를 반경으로 자른다 · fanOut 이 전원에게 1발씩
    //  unit 2 — 저작값(피해 · 예고 · 연타 시차 · impactTileRange 0)이 그대로 흐른다
    //
    // ⚠ 적을 **캐논 북쪽**에도 두고 반경 밖에도 둔다. "전원" 과 "반경" 은 서로를 가려주지
    // 못한다 — 반경 게이트가 죽어도 전원 단언은 통과하고, fan-out 이 죽어도 반경 밖
    // 무피해 단언은 통과한다.
    public class OnPlaceSkyStrikeTest
    {
        private const float Hp = 100000f;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        // 사슬 본체 — 반경 안 전원이 1발씩 맞고, 반경 밖은 무피해.
        [UnityTest]
        public IEnumerator SkyStrike_HitsEveryEnemyInScope_OncEach_AndNothingOutside()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_scope");
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);

            // 반경 2 안 3마리(서로 다른 칸, 북/동/남서) + 반경 밖 1마리.
            var inA = SpawnDummy(em, bridge, new Vector2Int(cell.x, cell.y + 2));
            var inB = SpawnDummy(em, bridge, new Vector2Int(cell.x + 2, cell.y));
            var inC = SpawnDummy(em, bridge, new Vector2Int(cell.x - 1, cell.y - 1));
            var outFar = SpawnDummy(em, bridge, new Vector2Int(cell.x + 5, cell.y + 5));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");

            // 경계 계측 — 실패 시 어디서 끊겼는지 한눈에 보이게 한다.
            var host = bridge.TryGetDefenderAt(cell, out Entity he) ? he : Entity.Null;
            string chain = $"host={host.Index}";
            chain += $" defTag={(host != Entity.Null && em.HasComponent<DefenderUnitTag>(host))}";
            if (host != Entity.Null && em.HasBuffer<Wassup.Battle.Combat.DcTriggerSlot>(host))
            {
                var sl = em.GetBuffer<Wassup.Battle.Combat.DcTriggerSlot>(host);
                chain += $" slots={sl.Length}";
                for (int i = 0; i < sl.Length; i++)
                    chain += $"[t={sl[i].trigger},p={sl[i].payload},pi={sl[i].patternIndex}]";
            }
            else chain += " slots=NONE";
            chain += host != Entity.Null && em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(host)
                ? $" patSlots={em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.PatternSlot>(host).Length}" : " patSlots=NONE";

            // 순간값은 애매하다(shots=1 이라 emitter 가 같은 틱에 소비·제거할 수 있다).
            // 창 전체에서 **최대치**를 잡아 «한 번이라도 존재했나» 를 본다.
            var pq = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileTag>());
            var cq = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileRequestCarrier>());
            int maxEmit = 0, maxCarrier = 0, maxProj = 0;
            for (int f = 0; f < 60; f++)
            {
                if (host != Entity.Null && em.HasBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(host))
                    maxEmit = Mathf.Max(maxEmit, em.GetBuffer<Wassup.Battle.Combat.Projectile.Emission.EmitterInstance>(host).Length);
                maxCarrier = Mathf.Max(maxCarrier, cq.CalculateEntityCount());
                maxProj = Mathf.Max(maxProj, pq.CalculateEntityCount());
                yield return null;
            }
            chain += $" maxEmitter={maxEmit} maxCarrier={maxCarrier} maxProjectile={maxProj}";

            float dA = Hp - em.GetComponentData<Health>(inA).value;
            float dB = Hp - em.GetComponentData<Health>(inB).value;
            float dC = Hp - em.GetComponentData<Health>(inC).value;
            float dOut = Hp - em.GetComponentData<Health>(outFar).value;
            foreach (var e in new[] { inA, inB, inC, outFar }) em.DestroyEntity(e);
            Object.Destroy(cannon);

            Assert.Greater(dA, 0f, $"북쪽 적이 안 맞았다 — fan-out 이 전원에게 안 나갔다. 경계: {chain}");
            Assert.Greater(dB, 0f, "동쪽 적이 안 맞았다");
            Assert.Greater(dC, 0f, "남서쪽 적이 안 맞았다");
            Assert.AreEqual(0f, dOut, 0.001f,
                "반경 2 밖 적이 맞았다 — scopeTileRange 게이트가 죽었다(맵 전체 폭격)");
        }

        // 1:1 핀 — 각 적이 **정확히 1발**만 맞는다. impactTileRange 0(겹침 없음) + fan-out
        // 중복 없음을 함께 고정한다. 저작 피해 80 이 그대로 흐르는지도 여기서 본다.
        [UnityTest]
        public IEnumerator SkyStrike_EachEnemyTakesExactlyOneShotOfAuthoredDamage()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_onehit");
            float authored = AuthoredDamage(cannon);
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);

            // 인접한 두 칸 — impactTileRange 가 0 이 아니면 서로의 폭발에 같이 맞아 2배가 된다.
            var e1 = SpawnDummy(em, bridge, new Vector2Int(cell.x + 1, cell.y));
            var e2 = SpawnDummy(em, bridge, new Vector2Int(cell.x + 2, cell.y));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");
            yield return Frames(60);

            float d1 = Hp - em.GetComponentData<Health>(e1).value;
            float d2 = Hp - em.GetComponentData<Health>(e2).value;
            em.DestroyEntity(e1); em.DestroyEntity(e2);
            Object.Destroy(cannon);

            Assert.AreEqual(authored, d1, 0.01f,
                $"인접 적 1 의 피해가 저작값과 다르다 — 1발 초과로 맞았거나(impactTileRange>0 / fan-out 중복) 값이 안 흘렀다");
            Assert.AreEqual(authored, d2, 0.01f, "인접 적 2 의 피해가 저작값과 다르다");
        }

        // 같은 칸 적 둘 → **미사일 두 발, 둘 다 저작 피해만큼**.
        //
        // 이 둘이 함께 걸려야 unit 8 이 성립한다:
        //  · 발수 = 적 수 — 칸당 1발로 접혀 있던 시절(unit 1) 3기가 뭉치면 1발만 떨어졌다.
        //  · 적당 피해 = 저작값 — 접기를 **게이트 없이** 풀면 셀 낙하탄이 `impactTileRange 0`
        //    이어도 그 칸 전원을 때리므로(TileAoe) 각자 2배를 맞는다.
        // 즉 각 발이 «자기 적»(`ProjectileState.target`)만 때려야 한다. 한쪽만 재면 나머지
        // 한쪽이 조용히 깨진다 — 실제로 unit 1 의 테스트가 `> 0` 만 재서 과피해를 놓쳤다.
        [UnityTest]
        public IEnumerator TwoEnemiesInSameCell_EachTakeExactlyOneShot()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_samecell");
            float authored = AuthoredDamage(cannon);
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);

            var target = new Vector2Int(cell.x + 1, cell.y + 1);
            var a = SpawnDummy(em, bridge, target);
            var b = SpawnDummy(em, bridge, target);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");

            // 발수는 **동시 생존 최대치**로 센다. 갈래는 한 프레임에 다 발사되고 시차는
            // 낙하 시간에만 들어가므로(emitter 주석), 발사 직후 프레임에 전부 살아 있다.
            //
            // ⚠ 전체 투사체를 세면 안 된다 — 판에는 웨이브 적도 걸어 들어오고(`Wave 1 queued`)
            // 그놈이 스코프에 들어오면 **자기 미사일**을 하나 더 받는다. 실측으로 더미 2기에
            // 3발이 세어져 「여분이 샌다」로 오진됐다. 우리가 물어보는 것은 «이 두 적에게 몇
            // 발이 갔나» 이므로 **임자(`target`)가 우리 더미인 탄만** 센다 — unit 8 이 각 발에
            // 임자를 실어서 이 구분이 가능해졌다.
            int maxAlive = 0;
            using (var q = em.CreateEntityQuery(
                       ComponentType.ReadOnly<Wassup.Battle.Combat.Projectile.ProjectileState>()))
            {
                for (int f = 0; f < 60; f++)
                {
                    var states = q.ToComponentDataArray<Wassup.Battle.Combat.Projectile.ProjectileState>(
                        Unity.Collections.Allocator.Temp);
                    int mine = 0;
                    for (int s = 0; s < states.Length; s++)
                        if (states[s].target == a || states[s].target == b) mine++;
                    states.Dispose();
                    maxAlive = Mathf.Max(maxAlive, mine);
                    yield return null;
                }
            }

            float da = Hp - em.GetComponentData<Health>(a).value;
            float db = Hp - em.GetComponentData<Health>(b).value;
            em.DestroyEntity(a); em.DestroyEntity(b);
            Object.Destroy(cannon);

            Assert.AreEqual(2, maxAlive,
                $"같은 칸 적 2기에 미사일이 {maxAlive}발 떨어졌다 — 발수는 적 수를 따라야 한다" +
                " (1발이면 칸당 1발로 접힌 것, 3발 이상이면 여분이 새는 것)");

            // ⚠ **「둘 다 맞았다」로는 부족하다.** 셀을 겨누는 낙하탄은 `impactTileRange 0` 이라도
            // 그 칸의 **전원**을 때린다(TileAoe). 같은 칸에 미사일을 두 발 떨어뜨리면 두 적이
            // 각각 **두 발씩** 맞아 피해가 2배가 된다 — spec 이 못 박은 「적당 정확히 80」이 깨진다.
            // 그래서 «맞았나» 가 아니라 «얼마나» 를 잰다.
            Assert.AreEqual(authored, da, 0.01f,
                $"같은 칸 적 A 의 피해가 저작값과 다르다({da}) — 셀을 겹쳐 겨누면 서로의 폭발에 함께 맞는다");
            Assert.AreEqual(authored, db, 0.01f, $"같은 칸 적 B 의 피해가 저작값과 다르다({db})");
        }

        // 연타 — 갈래가 **동시에** 떨어지지 않는다.
        //
        // ⚠ 관측을 「연출이 예쁜가」가 아니라 **「착탄 시각이 실제로 갈리는가」**로 잰다:
        // 먼저 떨어질 칸의 적이 피해를 받은 프레임에, 나중 칸의 적은 아직 멀쩡해야 한다.
        // 시차가 0 이면(또는 정렬이 죽으면) 둘이 같은 프레임에 맞아 이 단언이 빨개진다.
        [UnityTest]
        public IEnumerator FanOutImpacts_AreStaggered_NotSimultaneous()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_stagger");
            float stagger = AuthoredStagger(cannon);
            Assert.Greater(stagger, 0f, "연타 사양이라 시차가 저작돼 있어야 한다");
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);

            // row-major rank 로 정렬하므로 **y 가 작은 칸이 먼저** 떨어진다.
            var firstCell = new Vector2Int(cell.x, cell.y - 1);
            var lastCell = new Vector2Int(cell.x, cell.y + 2);
            var first = SpawnDummy(em, bridge, firstCell);
            var last = SpawnDummy(em, bridge, lastCell);

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");

            // 「먼저 맞은 쪽이 있는데 나중 쪽은 아직 안 맞은」 프레임이 존재하는가.
            bool sawGap = false;
            for (int f = 0; f < 90 && !sawGap; f++)
            {
                bool firstHit = em.GetComponentData<Health>(first).value < Hp;
                bool lastHit = em.GetComponentData<Health>(last).value < Hp;
                if (firstHit && !lastHit) sawGap = true;
                yield return null;
            }
            yield return Frames(40);

            float dFirst = Hp - em.GetComponentData<Health>(first).value;
            float dLast = Hp - em.GetComponentData<Health>(last).value;
            em.DestroyEntity(first); em.DestroyEntity(last);
            Object.Destroy(cannon);

            Assert.Greater(dFirst, 0f, "먼저 떨어질 칸의 적이 안 맞았다");
            Assert.Greater(dLast, 0f, "나중 칸의 적이 결국 안 맞았다 — 시차가 낙하를 삼키면 안 된다");
            Assert.IsTrue(sawGap,
                $"두 칸이 같은 프레임에 터졌다 — 시차({stagger}s)가 착탄에 반영되지 않았다");
        }

        // 반경 밖에만 적이 있으면 그 적은 무사하다 — 「후보 0 이면 조용히 소모」 경로가
        // 예외를 내거나 엉뚱한 곳을 때리지 않는지 본다.
        //
        // ⚠ 전역 투사체 수로 재지 않는다: 전투가 돌고 있어 웨이브가 스폰되고 다른 무엇이
        // 쏠 수 있다. **내가 만든 더미의 체력**만이 이 테스트가 통제하는 사실이다.
        [UnityTest]
        public IEnumerator NoEnemyInScope_LeavesTheFarEnemyUntouched()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_noenemy");
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);
            var far = SpawnDummy(em, bridge, new Vector2Int(cell.x + 6, cell.y + 6));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");
            yield return Frames(60);

            float d = Hp - em.GetComponentData<Health>(far).value;
            em.DestroyEntity(far);
            Object.Destroy(cannon);

            Assert.AreEqual(0f, d, 0.001f, "반경 밖 적이 맞았다");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        private static IEnumerator LoadBattle()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;
        }

        private static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        // 카탈로그 사본. 평타가 섞이면 배치 폭격분을 분리 측정할 수 없으므로 사거리 0.
        private static DefenderUnitData MakeCannon(string testId)
        {
            var catalog = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0];
            var unit = Object.Instantiate(catalog.ById("cannon"));
            unit.id = testId;
            unit.attackRange = 0f;
            unit.cost = 0;
            unit.maxOnBoard = 100;
            Assert.AreEqual(OnPlaceEffectType.None, unit.onPlaceEffect,
                "캐논의 레거시 배치 효과는 해제돼 있어야 한다(규칙 경로로 이관)");
            Assert.IsNotNull(unit.GetAbility<UnitSkillAbility>(), "캐논에 UnitSkillAbility 가 배선돼야 한다");
            return unit;
        }

        // 저작값은 테스트에 하드코딩하지 않는다 — 밸런스 튜닝이 테스트를 깨면 안 된다.
        private static float AuthoredDamage(DefenderUnitData unit)
        {
            var ability = unit.GetAbility<UnitSkillAbility>();
            Assert.IsNotNull(ability.mechanics, "mechanics");
            Assert.Greater(ability.mechanics.Length, 0, "mechanics 비어 있음");
            var pattern = ability.mechanics[0].payload.pattern;
            Assert.IsNotNull(pattern, "payload.pattern 미배선");
            Assert.IsNotNull(pattern.barrel, "pattern.barrel 미배선");
            Assert.AreEqual(0, pattern.barrel.impactTileRange,
                "1:1 타격 사양이라 barrel 의 impactTileRange 는 0 이어야 한다");
            return pattern.damage;
        }

        // ⚠ **전투를 시작한 뒤에 배치한다.** 이 스킬은 투사체를 쓰는데, 브리지의
        // `DrainProjectileSpawnRequests` 는 `Update` 의 `if (!_running) return;` 아래에 있다 —
        // 배치 페이즈에 놓으면 emitter 가 캐리어를 만들어도 아무도 드레인하지 않아 미사일이
        // 한 발도 안 뜬다(실측: maxCarrier=3, maxProjectile=0).
        //
        // 이건 이 spec 의 결함이 아니라 **배치 페이즈의 사실**이며, README 후속 후보
        // 「배치 페이즈 발동 정책」이 가리키는 비용이 캐논에서 어떻게 나타나는지의 실물이다
        // (피해가 낭비되는 정도가 아니라 요청이 큐에 쌓인 채 남는다).
        // 저작값은 하드코딩하지 않는다 — 연타 간격은 Play 튜닝 대상이다.
        private static float AuthoredStagger(DefenderUnitData unit)
            => unit.GetAbility<UnitSkillAbility>().mechanics[0].payload.pattern.fanOutStaggerSec;

        private static void Prepare(BattleBridge bridge, GameManager gm, DefenderUnitData unit)
        {
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);
            bridge.StartBattle();

            // ⚠ **판 위에 우리 더미만 있는 게 아니다.** 맵이 본능/적 마음 구조물을 스폰하고
            // (`[BattleBridge] Structures spawned: 5`) 본능은 `attackDamage` 로 AttackState 를
            // 달고 적을 쏜다(BattleBridge 의 구조물 bake). 그 피해가 더미에 얹히면 «미사일이
            // 저작값만큼 때렸나» 를 잴 수 없다 — 실측으로 더미가 80 대신 100 을 받았고,
            // 스코프 **밖** 더미까지 20~40 을 받아 「반경 게이트가 죽었다」로 **오진**됐다.
            // (캐논 자기 평타는 MakeCannon 이 attackRange 0 으로 이미 막았다. 빠진 건 남의
            //  공격원이었다.)
            //
            // 배치 전이라 지금 AttackState 를 가진 것은 구조물뿐이다 — 여기서 벗기면
            // 방어유닛/투사체 경로는 손대지 않고 «다른 공격원» 만 사라진다.
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using (var atkQ = em.CreateEntityQuery(
                       ComponentType.ReadOnly<Wassup.Battle.Combat.AttackState>()))
            {
                if (!atkQ.IsEmpty) em.RemoveComponent<Wassup.Battle.Combat.AttackState>(atkQ);
            }
        }

        private static Entity SpawnDummy(EntityManager em, BattleBridge bridge, Vector2Int cell)
        {
            var w = bridge.GridToWorldCenterVector(cell);
            var e = em.CreateEntity();
            em.AddComponentData(e, LocalTransform.FromPosition(new float3(w.x, w.y, w.z)));
            em.AddComponentData(e, new Health { value = Hp, max = Hp });
            em.AddComponentData(e, new FactionTag { value = Faction.EnemyUnit });
            em.AddBuffer<IncomingDamage>(e);
            em.AddBuffer<CcEffect>(e);
            em.AddComponent<AttackUnitTag>(e);
            return e;
        }

        private static Vector2Int FindPlaceableCell(BattleBridge bridge, DefenderUnitData u)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _)) return new Vector2Int(x, y);
            Assert.Fail("배치 가능 칸 없음");
            return default;
        }
    }
}
