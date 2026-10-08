using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Map;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.BattleCoreUnity.View;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 6c — **효과의 그림이 사건을 따라가는가.**
    //
    // 재는 것은 계약 12·7 의 뷰 쪽 절반이다:
    //   · 사건 1건 → 뷰 1개(장판·길막·픽업·사직서·상태 표식)
    //   · 소멸 사건 → 그 뷰 회수
    //   · **숙주 소멸 → 붙어 있던 표식 전부 회수**(폴링 없이)
    //
    // 판을 멈추고(`Pause`) 틱을 **손으로** 민다 — 뷰가 무엇을 받는지는 틱 사이 방출이 정하므로
    // 틱 수를 세야 단언이 결정론이다. 사건은 되도록 **코어의 진짜 문**으로 낸다(디버그 커맨드 ·
    // 군중 제어 요청 · 월드의 스폰/제거 함수) — 가짜 사건을 버스에 밀어 넣으면 「뷰가 코어가 실제로
    // 내는 모양을 받는가」를 못 잰다.
    public sealed class CoreEffectViewTests
    {
        private static IEnumerator Boot(System.Action<BattleDriver> found)
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            Assert.IsTrue(driver.Running, "부팅 직후 판이 걸려 있어야 한다");
            driver.Apply(Command.FinishPlacement());
            driver.Pause(true);
            found(driver);
        }

        private static IEnumerator Ticks(BattleDriver driver, int n)
        {
            for (int i = 0; i < n; i++) driver.Match.Tick();
            yield return null;   // 드라이버 Update 가 방출한다
            yield return null;   // 풀의 LateUpdate 가 앵커를 잡는다
        }

        private static int2 PathCell(MapRuntime map, int skip = 0)
        {
            var snap = map.Snapshot;
            for (int y = 0; y < snap.Height; y++)
                for (int x = 0; x < snap.Width; x++)
                {
                    var c = new int2(x, y);
                    if (snap.IsGoalCell(c) || (snap.TravelLayersAt(c) & LayerBits.Path) == 0) continue;
                    if (skip-- > 0) continue;
                    return c;
                }
            Assert.Fail("맵에 길 칸이 없다");
            return default;
        }

        [UnityTest]
        public IEnumerator 존_장판_사건1_뷰1_그리고_수명_만료로_회수된다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var pool = Object.FindAnyObjectByType<CoreHazardViewPool>();
            Assert.IsNotNull(pool, "씬에 CoreHazardViewPool 이 없다");
            var def = driver.Match.Definition;
            Assert.Greater(def.Hazards.Length, 0, "BattleDriver 에 존 장판 저작이 없다(6c 씬 배선)");

            var cell = PathCell(driver.Match.Map);
            Assert.IsTrue(driver.Apply(Command.DebugSpawnHazard(0, cell, 0f)).Accepted, "장판이 거절됐다");
            yield return Ticks(driver, 0);

            var world = driver.Match.World;
            Assert.AreEqual(1, world.Hazards.Count, "코어에 장판이 하나 서야 한다");
            Assert.AreEqual(world.Hazards.Count, pool.ZoneViewCount, "사건 1건 → 장판 뷰 1개");

            // 그림의 지름 = 2 × (반경 + 칸 반폭) — 규칙 쪽 짝에서 나온다(뷰가 다시 재지 않는다).
            var h = world.Hazards[0];
            Assert.IsTrue(pool.TryGetZoneDiameter(h.Id, out float diameter));
            float expected = 2f * (Mathf.Max(0, h.RadiusTiles) + Somnia.Battle.Skills.SkillMath.CellShapePaddingTiles)
                             * driver.TileSize;
            Assert.AreEqual(expected, diameter, 1e-4f, "장판 그림 지름 = 판정 자(자리형 원점 항 = 칸 반폭)");
            // 옛 프리팹의 **자기 시계**가 남아 있으면 판이 멈춰도 그림이 저작 수명 뒤에 스스로 사라진다
            // (6c Play 스모크 실측 — 뷰 수 2, 선 오브젝트 0). 그림의 수명은 코어 개체의 수명이어야 한다.
            Assert.IsNull(pool.GetComponentInChildren<Somnia.Battle.Presentation.HazardVisualLifetime>(true),
                "장판 뷰에 옛 자기 수명 시계가 남아 있다");

            int ticks = Mathf.CeilToInt(def.Hazards[0].Lifetime * 60f) + 2;
            yield return Ticks(driver, ticks);
            Assert.AreEqual(0, world.Hazards.Count, "수명이 끝나면 코어에서 사라진다");
            Assert.AreEqual(0, pool.ZoneViewCount, "소멸 사건 → 장판 뷰 회수(자기 시계로 지우지 않는다)");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 상태_표식은_걸림에_켜지고_풀림에_꺼지며_숙주가_사라지면_전부_회수된다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var fx = Object.FindAnyObjectByType<CoreStatusFxSpawner>();
            Assert.IsNotNull(fx, "씬에 CoreStatusFxSpawner 가 없다");

            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(0, 0)).Accepted, "적 스폰 거절");
            var world = driver.Match.World;
            SimEntityId enemy = SimEntityId.None;
            for (int i = 0; i < world.Units.Count; i++)
                if (world.Units[i].Kind == UnitKind.Enemy) enemy = world.Units[i].Id;   // 가장 최근
            Assert.IsFalse(enemy.IsNone, "적이 안 섰다");
            yield return Ticks(driver, 0);

            // 사건 1 → 표식 1. 짧은 기절(0.25초)을 코어의 **그 문**으로 건다.
            world.RequestCc(CcRequest.Of(enemy, CcRequestKind.Stun, 0.25f, SimEntityId.None));
            yield return Ticks(driver, 1);
            Assert.IsTrue(fx.IsShown(enemy, StatusFxKind.Stun), "기절 사건 → 기절 표식");
            Assert.AreEqual(1, fx.ActiveCount, "사건 1건 → 표식 1개");

            // 풀림 → 회수.
            yield return Ticks(driver, 30);
            Assert.IsFalse(fx.IsShown(enemy, StatusFxKind.Stun), "기절이 풀리면 표식이 내려간다");
            Assert.AreEqual(0, fx.ActiveCount);

            // 다시 걸고 **숙주를 지운다** — 붙어 있던 표식이 전부 가야 한다.
            world.RequestCc(CcRequest.Of(enemy, CcRequestKind.Stun, 5f, SimEntityId.None));
            yield return Ticks(driver, 1);
            Assert.GreaterOrEqual(fx.WantedCountOf(enemy), 1, "다시 건 기절이 표식으로 서야 한다");
            Assert.IsTrue(driver.Apply(Command.DebugDestroy(enemy)).Accepted);
            yield return Ticks(driver, 0);
            Assert.AreEqual(0, fx.WantedCountOf(enemy), "숙주 소멸 → 그 몸의 표식 전부 회수");
            Assert.AreEqual(0, fx.ActiveCount, "숙주 소멸 → 선 표식 뷰 0");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // 6c 후속 — **어그로 표식은 코어의 풀림 사건에 꺼진다**(폴링 없음). 도발을 코어의 진짜 문
        // (어그로 요청)으로 걸고, 시한 만료로 풀어 표식이 내려가는지 본다.
        [UnityTest]
        public IEnumerator 어그로_표식은_획득에_켜지고_코어의_풀림_사건에_꺼진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var fx = Object.FindAnyObjectByType<CoreStatusFxSpawner>();
            Assert.IsNotNull(fx, "씬에 CoreStatusFxSpawner 가 없다");
            var def = driver.Match.Definition;

            // 가디언 = 어그로 수용량이 저작된 방어유닛, 적 = 유닛을 노리고 공격 수단이 있는 적.
            // (코어의 획득 게이트가 거르는 적을 고르면 「표식이 안 뜬다」가 뷰가 아니라 게이트에서 난다.)
            int guardianDef = -1;
            for (int i = 0; i < def.Units.Length && guardianDef < 0; i++)
                if (def.Units[i].AggroCapacity > 0) guardianDef = i;
            Assert.GreaterOrEqual(guardianDef, 0, "라이브 정의표에 어그로 수용량이 있는 방어유닛이 없다");
            int enemyDef = -1;
            for (int i = 0; i < def.Enemies.Length && enemyDef < 0; i++)
                if (def.Enemies[i].AttackRange > 0f
                    && (Somnia.Battle.BattleCore.Combat.TargetDefaults.ResolveEnemy(def.Enemies[i].TargetFactions)
                        & Somnia.Battle.Skills.Factions.AnyUnit) != 0)
                    enemyDef = i;
            Assert.GreaterOrEqual(enemyDef, 0, "라이브 정의표에 유닛을 노리는 적이 없다");

            var world = driver.Match.World;
            var cell = PathCell(driver.Match.Map, 2);
            Assert.IsTrue(driver.Apply(Command.DebugSpawnDefender(guardianDef, cell)).Accepted, "가디언 스폰 거절");
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(enemyDef, 0)).Accepted, "적 스폰 거절");
            SimEntityId guardian = SimEntityId.None, enemy = SimEntityId.None;
            for (int i = 0; i < world.Units.Count; i++)
            {
                if (world.Units[i].Kind == UnitKind.Defender) guardian = world.Units[i].Id;
                if (world.Units[i].Kind == UnitKind.Enemy) enemy = world.Units[i].Id;
            }
            Assert.IsFalse(guardian.IsNone || enemy.IsNone, "가디언·적이 안 섰다");
            world.Find(guardian).Attack = null;   // 히트가 어그로를 다시 물지 못하게(재획득은 이 질문 밖)
            yield return Ticks(driver, 1);

            var acquired = new System.Collections.Generic.List<CoreEvent>();
            var released = new System.Collections.Generic.List<CoreEvent>();
            driver.Match.Bus.Subscribe(CoreEventKind.AggroAcquired, 0, acquired.Add);
            driver.Match.Bus.Subscribe(CoreEventKind.AggroReleased, 0, released.Add);

            world.AggroRequests.Add(AggroRequest.Taunted(enemy, guardian, 0.25f));
            yield return Ticks(driver, 1);
            Assert.AreEqual(1, acquired.Count, "도발이 코어 게이트를 지나 붙어야 한다");
            Assert.IsTrue(fx.IsShown(enemy, StatusFxKind.Aggro), "획득 사건 → 어그로 표식");

            yield return Ticks(driver, 30);
            Assert.AreEqual(1, released.Count, "시한이 끝나면 풀림 사건이 한 번 난다");
            Assert.IsTrue(world.IsAlive(enemy), "적은 살아 있다 — 표식이 내려가는 까닭은 숙주 소멸이 아니다");
            Assert.IsFalse(fx.IsShown(enemy, StatusFxKind.Aggro), "풀림 사건 → 어그로 표식 회수");
            Assert.AreEqual(0, fx.WantedCountOf(enemy), "풀린 적에 남은 표식이 없다");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // 6c 후속 — **라스트런 표식은 코어의 닫힘 사건에 꺼진다**(초당 플래그 확인 없음).
        // 라이브 모드는 기믹이 0 이라 소비 단계가 돌지 않는다 — 먹힘은 월드의 **제거 문**으로 내고
        // (그 문이 `PickupTaken` 을 낸다), 창은 진행형 상태에 직접 연다. 닫힘은 코어의 crash 시계가 낸다.
        [UnityTest]
        public IEnumerator 라스트런_표식은_레드불_먹힘에_켜지고_코어의_닫힘_사건에_꺼진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var fx = Object.FindAnyObjectByType<CoreStatusFxSpawner>();
            Assert.IsNotNull(fx, "씬에 CoreStatusFxSpawner 가 없다");

            var world = driver.Match.World;
            var map = driver.Match.Map;
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(0, 0)).Accepted, "적 스폰 거절");
            Unit taker = null;
            for (int i = 0; i < world.Units.Count; i++)
                if (world.Units[i].Kind == UnitKind.Enemy) taker = world.Units[i];
            Assert.IsNotNull(taker, "적이 안 섰다");
            yield return Ticks(driver, 0);

            var ended = new System.Collections.Generic.List<CoreEvent>();
            driver.Match.Bus.Subscribe(CoreEventKind.LastRunEnded, 0, ended.Add);

            var cell = PathCell(map, 3);
            var p = world.SpawnPickup(PickupKind.RedBull, cell, map.CenterOf(cell), 30f, driver.Match.Clock.Tick);
            if (taker.Progressive == null) taker.Progressive = world.Parts.RentProgressive();
            taker.Progressive.BeginLastRun(0.25f, 0f);   // 비율 0 — crash 피해로 숙주가 죽지 않게
            world.RemovePickup(p.Id, taker, driver.Match.Clock.Tick);
            yield return Ticks(driver, 1);
            Assert.IsTrue(fx.IsShown(taker.Id, StatusFxKind.LastRun), "레드불 먹힘 → 라스트런 표식");

            yield return Ticks(driver, 30);
            Assert.AreEqual(1, ended.Count, "창의 시간이 끝나면 닫힘 사건이 한 번 난다");
            Assert.IsTrue(world.IsAlive(taker.Id), "숙주는 살아 있다 — 표식이 내려가는 까닭은 소멸이 아니다");
            Assert.IsFalse(fx.IsShown(taker.Id, StatusFxKind.LastRun), "닫힘 사건 → 라스트런 표식 회수");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 픽업과_사직서는_코어_개체를_따라_서고_사라진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var pickups = Object.FindAnyObjectByType<CorePickupViewPool>();
            var resignations = Object.FindAnyObjectByType<CoreResignationViewPool>();
            Assert.IsNotNull(pickups, "씬에 CorePickupViewPool 이 없다");
            Assert.IsNotNull(resignations, "씬에 CoreResignationViewPool 이 없다");

            var world = driver.Match.World;
            var map = driver.Match.Map;
            var a = PathCell(map);
            var b = PathCell(map, 1);
            int tick = driver.Match.Clock.Tick;
            // 놓는 자는 unit 7 이다 — 여기서는 월드의 **유일한 스폰·제거 문**을 직접 부른다(그 문이 사건을 낸다).
            var p = world.SpawnPickup(PickupKind.RedBull, a, map.CenterOf(a), 30f, tick);
            var r = world.DropResignation(b, map.CenterOf(b), SimEntityId.None,
                                          Somnia.Battle.Skills.Faction.None, tick);
            yield return Ticks(driver, 1);
            Assert.AreEqual(world.Pickups.Count, pickups.ViewCount, "픽업 사건 → 픽업 뷰");
            Assert.AreEqual(1, pickups.ViewCount);
            Assert.AreEqual(world.Resignations.Count, resignations.ViewCount, "사직서 사건 → 사직서 뷰");
            Assert.AreEqual(1, resignations.ViewCount);

            world.RemovePickup(p.Id, null, driver.Match.Clock.Tick);
            world.ConsumeResignations(1, driver.Match.Clock.Tick);
            yield return Ticks(driver, 1);
            Assert.AreEqual(0, pickups.ViewCount, "만료 사건 → 픽업 뷰 회수");
            Assert.AreEqual(0, resignations.ViewCount, "소모 사건 → 사직서 뷰 회수");
            _ = r;

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 길막은_해저드_풀이_들고_부서지면_회수된다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var pool = Object.FindAnyObjectByType<CoreHazardViewPool>();
            var units = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(pool);
            Assert.IsNotNull(units);
            Assert.Greater(driver.Match.Definition.BlockingHazards.Length, 0,
                "정의표에 길막 줄이 없다(BattleDriver 길막 저작 — 6c 씬 배선)");

            int unitViewsBefore = units.ViewCount;
            var map = driver.Match.Map;
            bool placed = false;
            for (int k = 0; k < 64 && !placed; k++)
                placed = driver.Apply(Command.DebugSpawnBlocker(0, PathCell(map, k), 0f)).Accepted;
            Assert.IsTrue(placed, "어느 길 칸에도 길막이 안 섰다");
            yield return Ticks(driver, 0);

            var world = driver.Match.World;
            SimEntityId blocker = SimEntityId.None;
            int count = 0;
            for (int i = 0; i < world.Units.Count; i++)
                if (world.Units[i].Kind == UnitKind.BlockingHazard) { blocker = world.Units[i].Id; count++; }
            Assert.AreEqual(1, count);
            Assert.AreEqual(count, pool.BlockerViewCount, "길막 스폰 사건 → 길막 뷰 1개");
            Assert.AreEqual(unitViewsBefore, units.ViewCount, "길막은 유닛 뷰 풀이 들지 않는다");

            Assert.IsTrue(driver.Apply(Command.DebugDestroy(blocker)).Accepted);
            yield return Ticks(driver, 0);
            Assert.AreEqual(0, pool.BlockerViewCount, "소멸 사건 → 길막 뷰 회수");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }
    }
}
