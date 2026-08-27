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
            int maxAlive = 0;
            using (var projectiles = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileState>()))
            {
                for (int f = 0; f < 60; f++)
                {
                    maxAlive = Mathf.Max(maxAlive, MissilesAimedAt(projectiles, a, b));
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

        // ── unit 9 — 낡은 조준(stale aim) ────────────────────────────────────
        //
        // **위 5개가 초록인데도 실전에서 피해가 0 이던 이유가 여기 있다: 위 더미들은 가만히
        // 서 있다.** 캐논 미사일은 발사 시점의 **칸**을 겨누고 `impactTileRange 0` 이라 착탄
        // 시점에 **같은 칸에 있어야만** 맞는데, 안 움직이는 더미는 칸을 벗어날 수 없다.
        //
        // 실전 수치(라이브 월드 실측): 예고 0.40s × 적 속도 2.00 = **0.80타일** 이동, 칸 소속
        // 유지 폭은 중심 ±0.50타일. 즉 **최소 예고에도 벗어나고** 뒤 슬롯(0.72s = 1.44타일)은
        // 전원 벗어난다. unit 8 이 임자 게이트를 넣기 전엔 그 칸에 **누가 있든** 때려서
        // 행군하는 뒤 적이 빈 칸을 채웠고, 조준이 낡았다는 사실이 그렇게 가려져 있었다.
        //
        // ⚠ 이동을 경로 추종에 맡기지 않는다 — 맵·통행 층·시드에 묶이면 테스트가 부서진다.
        // **옮기는 폭만** 실측에서 가져오고 변위는 직접 준다.
        [UnityTest]
        public IEnumerator MovedDuringTelegraph_StillTakesDamage()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_moved");
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);
            float tile = TileSize(bridge);

            var victim = SpawnDummy(em, bridge, new Vector2Int(cell.x + 1, cell.y));
            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");

            // 탄이 실제로 뜬 뒤에 옮긴다 — 발사 전에 옮기면 새 칸을 겨눠서 결함이 숨는다.
            bool launched = false;
            using (var projectiles = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileState>()))
            {
                for (int f = 0; f < 30 && !launched; f++)
                {
                    // ⚠ 두 인자에 같은 적을 넘긴다. `Entity.Null` 을 넘기면 임자 없는 탄
                    // (메테오 등 기존 TileAoe 발사)이 세어져 «떴다» 가 거짓이 된다.
                    launched = MissilesAimedAt(projectiles, victim, victim) > 0;
                    if (!launched) yield return null;
                }
            }

            // 자기 칸을 **벗어나는** 변위(0.8타일 > 경계 0.5타일). 스코프 안에는 남는다.
            var before = em.GetComponentData<LocalTransform>(victim).Position;
            em.SetComponentData(victim, LocalTransform.FromPosition(
                new float3(before.x + 0.8f * tile, before.y, before.z)));
            yield return Frames(90);

            float dmg = Hp - em.GetComponentData<Health>(victim).value;
            em.DestroyEntity(victim);
            Object.Destroy(cannon);

            Assert.IsTrue(launched, "미사일이 아예 뜨지 않았다 — 이 테스트가 재는 것은 착탄이다");
            Assert.Greater(dmg, 0f,
                "예고 중 자기 칸을 벗어난 적이 **아무 피해도 받지 않았다**. 탄은 «칸»(발사 시점)을" +
                " 겨누는데 페이로드는 «적»(착탄 시점)을 본다 — 조준이 둘이라 예고 시간만큼 어긋난다.");
        }

        // 같은 칸 두 발이 **화면에서 갈리는가**. unit 8 은 여분 발을 0.28타일만 비켜
        // 떨어뜨렸는데 미사일 `visualScale` 이 6 이다 — 완전히 겹쳐 한 발로 보인다
        // (제보: 「범위 안 5~7기인데 낙하가 2개」).
        //
        // 착탄점 «거리» 로 재는 이유: 발수(위 `TwoEnemiesInSameCell…`)는 이미 초록인데도
        // 화면에는 안 보였다. 발수와 시인성은 서로를 가려주지 못한다.
        [UnityTest]
        public IEnumerator BunchedEnemies_LandOnDistinctPoints()
        {
            yield return LoadBattle();
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();

            var cannon = MakeCannon("test_skystrike_distinct");
            Prepare(bridge, gm, cannon);
            var cell = FindPlaceableCell(bridge, cannon);
            float tile = TileSize(bridge);

            var target = new Vector2Int(cell.x + 1, cell.y + 1);
            var a = SpawnDummy(em, bridge, target);
            var b = SpawnDummy(em, bridge, target);

            // ⚠ `SpawnDummy` 는 둘을 **정확히 같은 좌표**에 놓는다. 실제 판에서는 분리 반경이
            // 적들을 갈라놓지만 더미에는 그 시스템이 없다 — 그대로 두면 착탄점이 같은 것이
            // 당연해져서 이 테스트가 아무것도 못 잰다. 같은 칸 **안에서** 갈라 놓는다:
            // ±0.3타일이면 둘 다 같은 칸으로 반올림된다(칸 경계는 ±0.5).
            var center = em.GetComponentData<LocalTransform>(a).Position;
            em.SetComponentData(a, LocalTransform.FromPosition(
                new float3(center.x - 0.3f * tile, center.y, center.z)));
            em.SetComponentData(b, LocalTransform.FromPosition(
                new float3(center.x + 0.3f * tile, center.y, center.z)));

            Assert.IsTrue(bridge.PlaceDefenderAs(cell.x, cell.y, cannon), "배치");

            float3 ia = default, ib = default;
            bool got = false;
            using (var projectiles = em.CreateEntityQuery(ComponentType.ReadOnly<ProjectileState>()))
            {
                for (int f = 0; f < 60 && !got; f++)
                {
                    got = TryGetImpacts(projectiles, a, b, out ia, out ib);
                    if (!got) yield return null;
                }
            }

            em.DestroyEntity(a); em.DestroyEntity(b);
            Object.Destroy(cannon);

            Assert.IsTrue(got, "같은 칸 두 적을 겨눈 탄 2발을 못 찾았다");
            float apart = math.distance(new float2(ia.x, ia.z), new float2(ib.x, ib.z));
            Assert.GreaterOrEqual(apart, 0.5f * tile,
                $"같은 칸 두 발의 착탄점이 {apart / tile:F2}타일밖에 안 떨어져 있다 — 미사일" +
                " visualScale 이 6 이라 화면에서 한 발로 접힌다(발수를 늘린 목적이 사라진다).");
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // 인접 셀 중심 간 거리 = 타일 크기. 상수로 박지 않는다(맵이 정한다).
        private static float TileSize(BattleBridge bridge)
            => (bridge.GridToWorldCenterVector(new Vector2Int(1, 0))
                - bridge.GridToWorldCenterVector(new Vector2Int(0, 0))).magnitude;

        // 두 적을 각각 겨눈 탄의 착탄점. 둘 다 살아 있는 프레임에만 true.
        private static bool TryGetImpacts(EntityQuery projectiles, Entity a, Entity b,
                                          out float3 ia, out float3 ib)
        {
            ia = default; ib = default;
            bool hasA = false, hasB = false;
            var states = projectiles.ToComponentDataArray<ProjectileState>(
                Unity.Collections.Allocator.Temp);
            for (int i = 0; i < states.Length; i++)
            {
                if (states[i].target == a && !hasA) { ia = states[i].impact; hasA = true; }
                else if (states[i].target == b && !hasB) { ib = states[i].impact; hasB = true; }
            }
            states.Dispose();
            return hasA && hasB;
        }


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
            SilenceOtherAttackers();
        }

        // 이 파일의 모든 단언은 «캐논의 배치 폭격이 얼마나 때렸나» 를 묻는다. 그러려면 판 위에
        // 때리는 것이 그것뿐이어야 하는데, 배틀 씬은 비어 있지 않다 — 맵이 본능 구조물을
        // 스폰하고(`Structures spawned: 5`) 본능은 `attackDamage` 로 AttackState 를 달고 적을
        // 쏜다. `MakeCannon` 이 캐논 **자기** 평타를 attackRange 0 으로 막는 것과 같은 이유로,
        // **남의** 공격원도 꺼야 한다.
        //
        // 안 끄면 조용히 오진된다(실측): 더미가 80 대신 100 을 받고, 스코프 **밖** 더미까지
        // 20~40 을 받아 「반경 게이트가 죽었다」로 읽힌다.
        //
        // 배치 전이라 지금 AttackState 를 가진 것은 구조물뿐이다 — 방어유닛/투사체 경로는
        // 손대지 않는다.
        private static void SilenceOtherAttackers()
        {
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            using (var attackers = em.CreateEntityQuery(
                       ComponentType.ReadOnly<Wassup.Battle.Combat.AttackState>()))
            {
                if (!attackers.IsEmpty)
                    em.RemoveComponent<Wassup.Battle.Combat.AttackState>(attackers);
            }
        }

        // 임자(`ProjectileState.target`)가 이 두 적인 탄만 센다.
        //
        // 전체 투사체를 세면 안 된다 — 판에는 웨이브 적도 걸어 들어오고(`Wave 1 queued`)
        // 그놈이 스코프에 들어오면 **자기 미사일**을 하나 더 받는다. 실측으로 더미 2기에
        // 3발이 세어져 「여분이 샌다」로 오진됐다. unit 8 이 각 발에 임자를 실어서 이 구분이
        // 가능해졌다.
        private static int MissilesAimedAt(EntityQuery projectiles, Entity a, Entity b)
        {
            var states = projectiles.ToComponentDataArray<ProjectileState>(
                Unity.Collections.Allocator.Temp);
            int count = 0;
            for (int i = 0; i < states.Length; i++)
                if (states[i].target == a || states[i].target == b) count++;
            states.Dispose();
            return count;
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
