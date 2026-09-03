using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Core.TimeControl;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // active-ally-zone unit 4 — 아군 버프가 "시전 순간 스냅샷" 이 아니라 **시간제 장판**임을 잰다.
    //
    // 핵심 불변식 3개:
    //  (1) 장판 안에 있는 동안만 강화 — 이탈·만료로 자연 소멸(AllyBuffApplySec 지연 상한)
    //  (2) 아군 0기여도 시전 성공 — 적 장판과 규칙이 같아졌다(구 0기 거절 폐기)
    //  (3) 배치 오라와 **합산**(전용 stackId) — 지불한 효과가 오라에 덮이지 않는다
    //
    // 대기 시간은 EffectSpawner.AllyBuffApplySec 를 **읽어서** 만든다(상수 복제 금지 — knob 을
    // 조정하는 순간 조용히 어긋난다). 캐스트는 `if (!_running)` 뒤라 StartBattle 필수(레포 관례).
    public class ActiveAllyZoneTest
    {
        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTearDown]
        public IEnumerator UnityTearDown()
        {
            TimeManager.Instance.ResetAll();
            PrimeTween.Tween.StopAll();
            yield return null;
        }

        [UnityTest]
        public IEnumerator Zone_BuffsAlliesInside_NotOutside()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.IsNotNull(bridge, "BattleBridge present");
            var cat = FindCatalog();
            var a = cat.ById("ranger");
            var b = cat.ById("scout");
            var far = cat.ById("guardian");

            yield return BeginWith(bridge, new[] { a, b, far });

            Assert.IsTrue(PlaceFirstValid(bridge, a), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            Assert.IsTrue(PlaceAdjacentTo(bridge, b, center), "place scout adjacent");
            Assert.IsTrue(PlaceFirstValidFarFrom(bridge, far, center, 3), "place a defender outside the zone");
            bridge.StartBattle();
            yield return null;

            // 속사(AttackSpeedMul)로 잰다 — 공격폭증의 DamageMul 은 인접 시너지가 같은 stat 을 쓰는
            // 채널이라 절대값이 배치 인접성에 따라 흔들린다.
            var skill = MakeSkill(SkillEffectType.RapidFire, magnitude: 2f, durationSec: 30f, range: 1f);
            Assert.IsTrue(bridge.CastSkillAtTile(skill, center, out _), "장판 시전");

            // 지연 0: 시스템이 매 프레임 도므로 다음 프레임엔 이미 걸려 있다(+ 집계 1프레임).
            for (int i = 0; i < 3; i++) yield return null;

            foreach (var (id, cell) in AllDefenderCells(bridge, em))
            {
                bool inside = Chebyshev(cell, center) <= 1;
                float mul = GetStat(bridge, em, id).attackSpeedMul;
                if (inside) Assert.AreEqual(2f, mul, 0.01f, $"{id}@{cell} 장판 안 → 공속 x2");
                else Assert.AreEqual(1f, mul, 0.01f, $"{id}@{cell} 장판 밖 → 불변");
            }
        }

        [UnityTest]
        public IEnumerator Zone_EmptyTileCastSucceeds_AndBuffsLateArrival()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var late = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { late });

            // 아직 아무도 배치되지 않은 상태에서 배치 가능 셀 하나를 골라 그 자리에 장판을 깐다.
            Assert.IsTrue(TryFindPlaceableCell(bridge, late, out var target), "빈 배치 가능 셀");
            bridge.StartBattle();
            yield return null;

            var skill = MakeSkill(SkillEffectType.RapidFire, magnitude: 2f, durationSec: 30f, range: 1f);
            Assert.IsTrue(bridge.CastSkillAtTile(skill, target, out int affected),
                "아군 0기여도 성공 — 구 0기 거절은 폐기됐다");
            Assert.AreEqual(0, affected, "스냅샷 카운트는 0(로그용)");

            // 사후 진입: 장판이 사는 동안 그 안에 배치하면 강화된다.
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            Assert.IsTrue(bridge.CanPlaceDefenderAt(target.x, target.y, late, out _), "그 칸에 배치 가능");
            Assert.IsTrue(bridge.PlaceDefenderAs(target.x, target.y, late), "장판 안에 배치");
            for (int i = 0; i < 5; i++) yield return null;

            Assert.AreEqual(2f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f,
                "장판이 이미 깔린 칸에 새로 배치된 유닛도 강화된다");

            // 계약 3-1 그물 — 슬롯 remaining 은 어떤 시점에도 AllyBuffApplySec 를 넘지 않는다.
            // 스킬 지속시간(여기 30초)으로 한 번이라도 걸면 refresh 가 max(old,new) 라서 값을 내릴
            // 수 없고, 장판을 벗어나도 30초간 버프가 남는 스냅샷 동작으로 조용히 회귀한다.
            Assert.LessOrEqual(SkillSlotRemaining(bridge, em, "ranger", StatKind.AttackSpeedMul),
                EffectSpawner.AllyBuffApplySec + 0.01f,
                "적용 지속시간은 스킬 지속시간이 아니라 AllyBuffApplySec 여야 한다");
        }

        // 리뷰 M4 — "0기여도 성공" 의 대칭을 지키는 그물. 선행 spec 이 적 장판 쪽에서 이걸 단정했는데
        // 아군 케이스를 재작성하며 사라졌다(계약 2: 6종 전부 0기 허용).
        [UnityTest]
        public IEnumerator EnemyField_EmptyTileCast_StillSucceeds()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            bridge.BeginPlacement();
            bridge.StartBattle();
            yield return null;

            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.SlowField, 0.6f, 5f, 2f), new Vector2Int(4, 4), out _),
                "적 장판은 대상 0기여도 성공한다");
        }

        // 리뷰 M5 / 계약 3 — 갱신을 ECS 에 둔 주된 이유가 "정지 중 큐가 드레인 없이 쌓였다가 재개
        // 프레임에 한꺼번에 터진다" 를 막는 것이다. 그룹 skip 이 구조적이라 지금은 맞지만, 그 구조가
        // 바뀌면 아무도 모르므로 여기서 고정한다.
        [UnityTest]
        public IEnumerator Zone_AcrossPause_DoesNotBurstOnResume()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { unit });
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            yield return null;

            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.RapidFire, 2f, 30f, 1f), center, out _), "장판 시전");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(2f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f, "먼저 걸린다");

            using (TimeManager.Instance.Request(TimeDomain.Battle, 0f, 100))
            {
                for (int i = 0; i < 20; i++) yield return null; // 정지 중 프레임 경과
                Assert.AreEqual(2f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f,
                    "정지 중에는 갱신도 소멸도 진행되지 않는다(그룹 skip)");
            }

            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(2f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f,
                "재개 프레임에 누적분이 몰려 터지지 않는다");
            Assert.LessOrEqual(SkillSlotRemaining(bridge, em, "ranger", StatKind.AttackSpeedMul),
                EffectSpawner.AllyBuffApplySec + 0.01f, "remaining 도 튀지 않는다");
        }

        [UnityTest]
        public IEnumerator Zone_Expiry_LapsesBuff_AndDestroysCarrier()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { unit });

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            yield return null;

            const float zoneLife = 0.4f;
            var skill = MakeSkill(SkillEffectType.RapidFire, magnitude: 2f, durationSec: zoneLife, range: 1f);
            Assert.IsTrue(bridge.CastSkillAtTile(skill, center, out _), "장판 시전");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(2f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f, "먼저 걸린다");

            // 수명 + 소멸 지연 + 여유
            yield return PumpFor(zoneLife + EffectSpawner.AllyBuffApplySec + 0.3f);

            Assert.AreEqual(1f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f,
                $"만료 후 ≤{EffectSpawner.AllyBuffApplySec}s 안에 풀린다");
            Assert.AreEqual(0, CountLiveZones(em), "캐리어 엔티티도 정리된다");
        }

        [UnityTest]
        public IEnumerator Zone_RelocatingOut_LapsesBuff()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { unit });

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var from = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            yield return null;

            var skill = MakeSkill(SkillEffectType.RapidFire, magnitude: 2f, durationSec: 30f, range: 1f);
            Assert.IsTrue(bridge.CastSkillAtTile(skill, from, out _), "장판 시전");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(2f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f, "먼저 걸린다");

            // 장판 밖(체비셰프 ≥ 3)으로 재배치. 뷰 코루틴을 거치지 않고 bridge API 3개로 구동한다.
            Assert.IsTrue(TryFindRelocationTargetFarFrom(bridge, from, 3, out var to), "장판 밖 재배치 대상");
            Assert.IsTrue(bridge.TryBeginDefenderRelocation(from, to, out var entity, out _), "재배치 시작");
            bridge.FinishDefenderRelocation(to, entity);
            bridge.ActivateDeployedDefender(to, entity);

            yield return PumpFor(EffectSpawner.AllyBuffApplySec + 0.3f);
            Assert.AreEqual(1f, GetStat(bridge, em, "ranger").attackSpeedMul, 0.01f,
                "장판을 벗어나면 갱신이 끊겨 자연 소멸한다");
        }

        [UnityTest]
        public IEnumerator Zone_StacksOnTopOfPlacementAura_AndOverlapDoesNotStack()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var guardian = cat.ById("guardian"); // 배치 시 주변 공격력 오라(on-place 슬롯)
            yield return BeginWith(bridge, new[] { guardian });

            Assert.IsTrue(PlaceFirstValid(bridge, guardian), "place guardian");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "guardian");
            bridge.StartBattle();
            for (int i = 0; i < 3; i++) yield return null;

            float before = GetStat(bridge, em, "guardian").damageMul;

            // 공격폭증 ×2.0 = Additive +1.0. 전용 슬롯이라 오라 기여 위에 **얹힌다**.
            var surge = MakeSkill(SkillEffectType.PowerSurge, magnitude: 2f, durationSec: 30f, range: 1f);
            Assert.IsTrue(bridge.CastSkillAtTile(surge, center, out _), "1번째 장판");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1f, GetStat(bridge, em, "guardian").damageMul - before, 0.01f,
                "오라를 덮지 않고 +100%p 가 얹힌다");

            // 같은 스킬 장판을 겹쳐 깔아도 merge 키가 같아 한 슬롯으로 접힌다(누적 아님).
            Assert.IsTrue(bridge.CastSkillAtTile(surge, center, out _), "2번째 장판(겹침)");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.AreEqual(1f, GetStat(bridge, em, "guardian").damageMul - before, 0.01f,
                "겹친 장판은 누적되지 않는다(refresh) — 의도된 동작");
        }

        // 리뷰 test-gap 4 — 한 유닛이 두 stat 을 동시에 받는다(merge 키에 stat 이 들어가므로 슬롯 분리).
        [UnityTest]
        public IEnumerator Zone_TwoStatsOnSameDefender_BothApply()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { unit });

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            for (int i = 0; i < 3; i++) yield return null;

            float beforeDamage = GetStat(bridge, em, "ranger").damageMul;
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.PowerSurge, 2f, 30f, 1f), center, out _), "공격폭증 장판");
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.RapidFire, 2f, 30f, 1f), center, out _), "속사 장판");
            for (int i = 0; i < 4; i++) yield return null;

            var stats = GetStat(bridge, em, "ranger");
            Assert.AreEqual(1f, stats.damageMul - beforeDamage, 0.01f, "공격력 +100%p");
            Assert.AreEqual(2f, stats.attackSpeedMul, 0.01f, "공속 x2 — 두 stat 이 서로를 덮지 않는다");
        }

        // 리뷰 test-gap 5 / M1 — 매치 경계에서 캐리어가 살아남으면 다음 판에 보이지 않는 강화 구역이 된다.
        [UnityTest]
        public IEnumerator Zone_MatchTeardown_DestroysCarriers()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { unit });

            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            yield return null;

            // 넉넉한 수명 — 매치가 끝나도 자연 만료로 사라지지 않을 만큼.
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.RapidFire, 2f, 120f, 1f), center, out _), "장판 시전");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1, CountLiveZones(em), "장판 1개 생존");

            bridge.StopBattle();
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(0, CountLiveZones(em), "매치 종료가 캐리어를 정리한다");
        }

        // dreamcatcher-attach-range-preview 후속(2026-09-03 사용자) — 장판 시전은 **보드 타일을 하나도 칠하지 않는다.**
        // 조준·판정이 원(N+0.5+몸)이 된 뒤에도 옛 (2N+1)² 사각 점등이 수명 동안 남아 "부착 후 타일이 남는다"로
        // 읽혔다. 링 외 타일 채널(zone/range/effect 어느 것도)로 장판을 그리지 않음을 그리드 아래 Tilemap
        // 전체의 사용 타일 수 스냅샷으로 잰다 — 이름 하나에 매이지 않고 어떤 채널로 새도 잡힌다.
        [UnityTest]
        public IEnumerator ZoneCast_PaintsNoBoardTiles()
        {
            LogAssert.ignoreFailingMessages = true;
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var bridge = Object.FindObjectOfType<BattleBridge>();
            var cat = FindCatalog();
            var unit = cat.ById("ranger");
            yield return BeginWith(bridge, new[] { unit });
            Assert.IsTrue(PlaceFirstValid(bridge, unit), "place ranger");
            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var center = CellOf(bridge, em, "ranger");
            bridge.StartBattle();
            yield return null;

            var before = SnapshotBoardTiles(bridge);
            Assert.IsTrue(bridge.CastSkillAtTile(
                MakeSkill(SkillEffectType.PowerSurge, 2f, 30f, 1f), center, out _), "공격폭증 장판");
            for (int i = 0; i < 3; i++) yield return null;
            Assert.AreEqual(1, CountLiveZones(em), "장판은 살아 있다(효과는 그대로)");

            var after = SnapshotBoardTiles(bridge);
            Assert.AreEqual(before.Count, after.Count, "시전이 새 Tilemap 을 만들지 않는다");
            foreach (var kv in after)
            {
                Assert.IsTrue(before.TryGetValue(kv.Key, out int n), $"시전 후 새 Tilemap '{kv.Key}'");
                Assert.AreEqual(n, kv.Value, $"Tilemap '{kv.Key}' 의 타일 수가 시전으로 바뀌었다");
            }
        }

        // ── helpers ──────────────────────────────────────────────────────────

        // 그리드 아래 모든 Tilemap 의 사용 타일 수 — 바닥·오버레이·프리뷰 채널 전부. 시전 전후 비교용.
        private static Dictionary<string, int> SnapshotBoardTiles(BattleBridge bridge)
        {
            var viewField = typeof(BattleBridge).GetField("tilemapMapView",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var view = (Component)viewField.GetValue(bridge);
            Assert.IsNotNull(view, "tilemapMapView 배선");
            var gridField = view.GetType().GetField("grid", BindingFlags.NonPublic | BindingFlags.Instance);
            var grid = (Grid)gridField.GetValue(view);
            Assert.IsNotNull(grid, "grid 배선");
            var result = new Dictionary<string, int>();
            foreach (var tm in grid.GetComponentsInChildren<UnityEngine.Tilemaps.Tilemap>(true))
            {
                // GetUsedTilesCount 는 타일 **종류** 수라 칸 수를 못 잰다 — 블록을 읽어 non-null 칸을 센다.
                tm.CompressBounds();
                int set = 0;
                foreach (var t in tm.GetTilesBlock(tm.cellBounds)) if (t != null) set++;
                result[tm.gameObject.name] = set;
            }
            return result;
        }

        private static IEnumerator BeginWith(BattleBridge bridge, DefenderUnitData[] pool)
        {
            bridge.SetDefenderPool(pool);
            bridge.BeginPlacement();
            var gm = Object.FindObjectOfType<GameManager>();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(1000);
            yield return null;
        }

        // 만료는 **배틀 도메인 델타**로 진행되므로 같은 시계를 누산한다(Time.time 은 배율을 모른다).
        private static IEnumerator PumpFor(float seconds)
        {
            float acc = 0f;
            int guard = 0;
            while (acc < seconds && guard++ < 6000)
            {
                yield return null;
                acc += TimeManager.Instance.DeltaTime(TimeDomain.Battle);
            }
        }

        // 스킬 아군 버프 슬롯의 남은 시간(계약 3-1 검증). 없으면 0.
        private static float SkillSlotRemaining(BattleBridge bridge, EntityManager em, string id, StatKind stat)
        {
            foreach (var (defId, cell) in AllDefenderCells(bridge, em))
            {
                if (defId != id) continue;
                if (!bridge.TryGetDefenderAt(cell, out var e) || !em.HasBuffer<StatModifierSlot>(e)) return 0f;
                var buf = em.GetBuffer<StatModifierSlot>(e);
                for (int i = 0; i < buf.Length; i++)
                    if (buf[i].stat == stat && buf[i].header.stackId == AllyBuffField.StackId)
                        return buf[i].header.remaining;
            }
            return 0f;
        }

        private static int CountLiveZones(EntityManager em)
        {
            var q = em.CreateEntityQuery(ComponentType.ReadOnly<AllyBuffField>());
            int n = q.CalculateEntityCount();
            q.Dispose();
            return n;
        }

        private static SkillData MakeSkill(SkillEffectType effect, float magnitude, float durationSec, float range)
        {
            var s = ScriptableObject.CreateInstance<SkillData>();
            s.id = $"test_{effect}";
            s.effect = effect;
            s.magnitude = magnitude;
            s.durationSec = durationSec;
            s.range = range;
            s.cooldownSec = 0f;
            s.cost = 0;
            return s;
        }

        private static int Chebyshev(Vector2Int a, Vector2Int b)
            => Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));

        private static DefenderCatalog FindCatalog()
        {
            var all = Resources.FindObjectsOfTypeAll<DefenderCatalog>();
            return all.Length > 0 ? all[0] : null;
        }

        private static bool TryFindPlaceableCell(BattleBridge bridge, DefenderUnitData u, out Vector2Int cell)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                    {
                        cell = new Vector2Int(x, y);
                        return true;
                    }
            cell = default;
            return false;
        }

        private static bool PlaceFirstValid(BattleBridge bridge, DefenderUnitData u)
            => TryFindPlaceableCell(bridge, u, out var c) && bridge.PlaceDefenderAs(c.x, c.y, u);

        // center 의 이웃 8칸 중 처음 배치 가능한 곳(= 체비셰프 1). 호출 8회 상한.
        private static bool PlaceAdjacentTo(BattleBridge bridge, DefenderUnitData u, Vector2Int center)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int x = center.x + dx, y = center.y + dy;
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
                }
            return false;
        }

        private static bool PlaceFirstValidFarFrom(BattleBridge bridge, DefenderUnitData u,
            Vector2Int center, int minDist)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                {
                    if (Chebyshev(new Vector2Int(x, y), center) < minDist) continue;
                    if (bridge.CanPlaceDefenderAt(x, y, u, out _))
                        return bridge.PlaceDefenderAs(x, y, u);
                }
            return false;
        }

        private static bool TryFindRelocationTargetFarFrom(BattleBridge bridge, Vector2Int from,
            int minDist, out Vector2Int to)
        {
            for (int x = -24; x < 48; x++)
                for (int y = -24; y < 48; y++)
                {
                    var cand = new Vector2Int(x, y);
                    if (Chebyshev(cand, from) < minDist) continue;
                    if (bridge.CanRelocateDefender(from, cand, out _))
                    {
                        to = cand;
                        return true;
                    }
                }
            to = default;
            return false;
        }

        private static ModifierStats GetStat(BattleBridge bridge, EntityManager em, string id)
        {
            foreach (var (defId, cell) in AllDefenderCells(bridge, em))
            {
                if (defId != id) continue;
                if (bridge.TryGetDefenderAt(cell, out var e) && em.HasComponent<ModifierStats>(e))
                    return em.GetComponentData<ModifierStats>(e);
            }
            return default;
        }

        // _defenderByTile: cell → (entity, DefenderUnitData) — bridge 의 배치 사실(테스트 관례).
        private static IEnumerable<(string id, Vector2Int cell)> AllDefenderCells(
            BattleBridge bridge, EntityManager em)
        {
            var f = typeof(BattleBridge).GetField("_defenderByTile",
                BindingFlags.NonPublic | BindingFlags.Instance);
            var dict = (System.Collections.IDictionary)f.GetValue(bridge);
            var found = new List<(string, Vector2Int)>();
            foreach (System.Collections.DictionaryEntry de in dict)
            {
                var val = de.Value;
                var t = val.GetType();
                var entity = (Entity)t.GetField("Item1").GetValue(val);
                var data = (DefenderUnitData)t.GetField("Item2").GetValue(val);
                if (entity == Entity.Null || !em.Exists(entity) || data == null) continue;
                found.Add((data.id, (Vector2Int)de.Key));
            }
            return found;
        }

        private static Vector2Int CellOf(BattleBridge bridge, EntityManager em, string id)
        {
            foreach (var (defId, cell) in AllDefenderCells(bridge, em))
                if (defId == id) return cell;
            Assert.Fail($"defender '{id}' not placed");
            return default;
        }
    }
}
