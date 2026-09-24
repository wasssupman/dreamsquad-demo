using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCore.Combat;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.View;
using Wassup.Core;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 6c 후속 — **배치 드래그 중 공격 도형 가이드**(directional-attack-shape unit 6 이식).
    //
    // 옛 게임은 방향 유닛(부채꼴·띠)을 끄는 동안 사거리 안 최근접 적 쪽으로 빨간 도형을 깔았다
    // (`TilemapMapView.SetShapeGuide` · 호출부 `BattleBridge.cs:8176-8183`). 새 오버레이에 그게 없었다.
    // 재는 것:
    //   · 방향 유닛 + 사거리 안 적 → 가이드가 뜨고, 그 적 쪽을 향한다
    //   · 도형 크기 = **범위 + 자기 몸**(링과 같은 값 — 대상의 몸은 안 그린다, 제약 13)
    //   · 전방위 유닛은 적이 사거리 안이어도 안 뜬다 · 적이 없으면 안 뜬다 · 드래그를 놓으면 내려간다
    public sealed class CoreShapeGuideTests
    {
        [UnityTest]
        public IEnumerator 드래그_중_방향_유닛은_도형_가이드가_뜨고_전방위_유닛은_안_뜬다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            driver.Apply(Command.FinishPlacement());
            driver.Pause(true);

            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay, "맵 오버레이가 씬에 없다");
            Assert.IsNotNull(overlay.TileSet, "오버레이에 타일셋이 없다 — 가이드 색의 출처가 없다");

            var def = driver.Definition;
            int shaped = -1, omni = -1;
            for (int i = 0; i < def.Units.Length; i++)
            {
                var u = def.Units[i];
                if (u.AttackRange <= 0f) continue;
                // 힐러(아군 마스크)는 적을 안 겨눈다 — 전방위 대조군으로 쓸 수 없다.
                if ((TargetDefaults.ResolveDefender(u.TargetFactions) & (int)Wassup.Battle.Units.Faction.EnemyUnit) == 0)
                    continue;
                if (u.Attack.ShapeKind == AttackShapeBaked.SectorKind && shaped < 0) shaped = i;
                else if (u.Attack.ShapeKind == AttackShapeBaked.OmniKind && omni < 0) omni = i;
            }
            Assert.GreaterOrEqual(shaped, 0, "로스터에 부채꼴 유닛(말파이트류)이 없다 — 가이드를 증언할 수 없다");
            Assert.GreaterOrEqual(omni, 0, "로스터에 전방위 공격 유닛이 없다 — 대조군이 없다");

            // 판 가운데에 세운다 — 레인 스폰 자리는 적 본능 거점(역시 후보다)의 사거리 안이라, 거기선
            // 「이 적 쪽」과 「후보가 없으면 안 뜬다」를 증언할 앵커가 없다(`TryAnchorInReach` 주석).
            var size = driver.GridSize;
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemy(0, new int2(size.x / 2 - 1, size.y / 2))).Accepted, "적 스폰 거절");
            Unit enemy = LatestEnemy(driver);
            Assert.IsNotNull(enemy, "적이 안 섰다");
            yield return null;

            // ── 방향 유닛: 뜬다 · 적을 향한다 · 길이 = 범위 + 자기 몸 ──
            Assert.IsTrue(TryAnchorInReach(driver, shaped, enemy, out int2 anchor, out float3 foot),
                "그 적만 사거리 안에 드는 앵커가 판에 없다");
            overlay.ShowPlacement(shaped, anchor, true);
            yield return null;
            yield return null;

            Assert.IsTrue(overlay.TryGetShapeGuide(out var spec, out var originView, out var dirView),
                "방향 유닛을 끄는데 사거리 안 적이 있는데도 도형 가이드가 없다");
            var su = def.Units[shaped];
            Assert.AreEqual(Wassup.Data.AttackShapeBaked.SectorKind, spec.kind, "가이드 형이 저작 bake 와 다르다");
            Assert.AreEqual(su.AttackRange + su.BodyRadiusTiles, spec.lengthTiles, 1e-5f,
                "가이드 반경 ≠ 범위 + 자기 몸 — 링과 다른 자다(대상 몸을 더했거나 몸을 뺐다)");
            float expectAngle = 2f * Mathf.Atan2(su.Attack.ShapeSinHalf, su.Attack.ShapeCosHalf) * Mathf.Rad2Deg;
            Assert.AreEqual(expectAngle, spec.angleDeg, 1e-3f, "가이드 각이 bake 역산과 다르다");

            Vector3 footView = BoardSpace.ToView(foot);
            Vector3 toEnemy = (Vector3)BoardSpace.ToView(enemy.Position) - footView;
            Assert.Greater(Vector3.Dot(dirView.normalized, toEnemy.normalized), 0.999f,
                "가이드가 사거리 안 최근접 적 쪽을 향하지 않는다");
            // 원점 = 발밑(링 중심). 띄움(z-fighting 회피)만큼은 법선 방향이라 평면 위 거리로 잰다.
            var n = BoardSpace.RaycastPlane().normal;
            var off = originView - footView;
            Assert.Less((off - Vector3.Dot(off, n) * n).magnitude, 1e-3f, "가이드 원점이 발밑이 아니다");

            // ── 전방위 유닛: 적이 사거리 안이어도 안 뜬다 ──
            Assert.IsTrue(TryAnchorInReach(driver, omni, enemy, out int2 omniAnchor, out _),
                "전방위 유닛의 사거리 안에 적이 드는 앵커가 없다");
            overlay.ShowPlacement(omni, omniAnchor, true);
            yield return null;
            yield return null;
            Assert.IsFalse(overlay.TryGetShapeGuide(out _, out _, out _),
                "전방위 유닛인데 도형 가이드가 떴다 — 원 링이 전부여야 한다");

            // ── 적이 없으면 안 뜬다(기본 방향 없음) ──
            overlay.ShowPlacement(shaped, anchor, true);
            yield return null;
            yield return null;
            Assert.IsTrue(overlay.TryGetShapeGuide(out _, out _, out _), "방향 유닛으로 돌아왔는데 가이드가 다시 안 선다");
            Assert.IsTrue(driver.Apply(Command.DebugDestroy(enemy.Id)).Accepted);
            driver.Match.Tick();
            yield return null;
            yield return null;
            Assert.IsFalse(overlay.TryGetShapeGuide(out _, out _, out _),
                "사거리 안 적이 없는데 가이드가 남았다 — 방향은 타겟이 정한다");

            // ── 드래그를 놓으면 내려간다 ──
            overlay.HidePlacement();
            yield return null;
            Assert.IsFalse(overlay.TryGetShapeGuide(out _, out _, out _), "드래그가 끝났는데 가이드가 남았다");

            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // 6c 후속 2 — **마크 후보 자격 = 옛 규칙**(`BattleBridge.cs:8141-8158`). 5b 가 「적 진영 + 생존」으로 좁혀
        // 옮겨, 지상 전용 근접을 끌 때 **때릴 수 없는 비행 적**에 「이놈이 맞는다」가 켜졌고 가이드도 그쪽을 봤다.
        // 비행 적을 **더 가깝게**, 지상 적을 더 멀게 세운다 — 필터가 없으면 최근접이 비행 적이라 빨갛다.
        [UnityTest]
        public IEnumerator 지상_전용_말파이트를_끌면_비행_적에_마크가_안_켜지고_가이드도_그쪽을_안_본다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            driver.Apply(Command.FinishPlacement());
            driver.Pause(true);
            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay);
            Assert.IsNotNull(overlay.TileSet, "오버레이에 타일셋이 없다 — 마크 색의 출처가 없다");

            var def = driver.Definition;
            int shaped = -1;
            for (int i = 0; i < def.Units.Length && shaped < 0; i++)
                if (def.Units[i].Attack.ShapeKind == AttackShapeBaked.SectorKind && def.Units[i].AttackRange > 0f)
                    shaped = i;
            Assert.GreaterOrEqual(shaped, 0, "로스터에 부채꼴 유닛(말파이트)이 없다");
            var su = def.Units[shaped];

            int air = -1, ground = -1;
            for (int i = 0; i < def.Enemies.Length; i++)
            {
                bool flies = (def.Enemies[i].TraversalLayers & Wassup.BattleCore.Map.LayerBits.Air) != 0;
                if (flies && air < 0) air = i;
                if (!flies && def.Enemies[i].TraversalLayers != 0 && ground < 0) ground = i;
            }
            Assert.GreaterOrEqual(air, 0, "적 정의표에 비행 적이 없다 — 증언할 수 없다");
            Assert.GreaterOrEqual(ground, 0, "적 정의표에 지상 적이 없다");
            Assert.IsFalse(Wassup.BattleCore.Map.LayerBits.CanTarget((byte)su.Attack.TargetLayers,
                                                                     (byte)def.Enemies[air].TraversalLayers),
                "이 부채꼴 유닛은 비행 적을 때릴 수 있다 — 전제(지상 전용)가 깨졌다");
            Assert.IsTrue(Wassup.BattleCore.Map.LayerBits.CanTarget((byte)su.Attack.TargetLayers,
                                                                    (byte)def.Enemies[ground].TraversalLayers),
                "이 부채꼴 유닛이 지상 적도 못 때린다 — 대조군이 없다");

            // 판 가운데 앵커. 비행 적 = 발밑 옆 2칸(가깝다) · 지상 적 = 발밑 위 3칸(멀다). 둘 다 사거리 안이어야 한다.
            var size = driver.GridSize;
            var anchor = new int2(size.x / 2 - 1, size.y / 2 - 1);
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemy(air, new int2(anchor.x + 2, anchor.y))).Accepted, "비행 적 스폰 거절");
            Unit flyer = LatestEnemy(driver);
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemy(ground, new int2(anchor.x, anchor.y + 3))).Accepted, "지상 적 스폰 거절");
            Unit walker = LatestEnemy(driver);
            Assert.AreNotSame(flyer, walker);
            yield return null;

            float ts = driver.TileSize;
            int w = math.max(1, su.FootprintWidth);
            var foot = new float3((anchor.x + (w - 1) * 0.5f) * ts, 0f, anchor.y * ts);
            Assert.IsTrue(AttackReach.InReach(foot, flyer.Position, su.AttackRange, ts, su.BodyRadiusTiles, flyer.HitRadius),
                "비행 적이 기하상 사거리 밖이다 — 이 배치로는 필터를 증언할 수 없다");
            Assert.IsTrue(AttackReach.InReach(foot, walker.Position, su.AttackRange, ts, su.BodyRadiusTiles, walker.HitRadius),
                "지상 적이 사거리 밖이다");

            overlay.ShowPlacement(shaped, anchor, true);
            yield return null;
            yield return null;

            Vector3 flyerView = BoardSpace.ToView(flyer.Position);
            Vector3 walkerView = BoardSpace.ToView(walker.Position);
            bool flyerMarked = false, walkerMarked = false;
            for (int i = 0; i < overlay.ActiveMarkCount; i++)
            {
                Assert.IsTrue(overlay.TryGetMark(i, out var p, out var c));
                if (PlanarDistance(p, flyerView) < 0.05f) flyerMarked = true;
                if (PlanarDistance(p, walkerView) < 0.05f) walkerMarked = true;
                var want = overlay.TileSet.rangeTargetMarkColor;
                Assert.AreEqual(want.r, c.r, 1e-4f, "마크 색이 TileSetData.rangeTargetMarkColor 에서 나오지 않았다");
                Assert.AreEqual(want.g, c.g, 1e-4f);
                Assert.AreEqual(want.b, c.b, 1e-4f);
                Assert.AreEqual(want.a, c.a, 1e-4f);
            }
            Assert.IsFalse(flyerMarked, "지상 전용 유닛인데 비행 적에 「이놈이 맞는다」가 켜졌다");
            Assert.IsTrue(walkerMarked, "때릴 수 있는 지상 적에 마크가 없다");

            Assert.IsTrue(overlay.TryGetShapeGuide(out _, out var origin, out var dirView), "가이드가 없다");
            var toWalker = walkerView - (Vector3)BoardSpace.ToView(foot);
            Assert.Greater(Vector3.Dot(dirView.normalized, toWalker.normalized), 0.999f,
                "가이드가 때릴 수 있는 지상 적이 아니라 다른 쪽(더 가까운 비행 적)을 본다");

            // 무효 배치 — 마크는 내려가고(고스트 빨강과 시간 분리) 가이드는 남는다(옛 `ApplyTargetMarkVisibility`).
            overlay.ShowPlacement(shaped, anchor, false);
            yield return null;
            yield return null;
            Assert.AreEqual(0, overlay.ActiveMarkCount, "배치가 무효인데 마크가 켜져 있다 — 빨강 둘이 한 화면에 뜬다");
            Assert.IsTrue(overlay.TryGetShapeGuide(out _, out _, out _), "무효 배치에서 가이드까지 사라졌다(옛 것은 남았다)");

            overlay.HidePlacement();
            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // 6c 후속 2 보충 — **적 거점도 가이드 후보다**(옛 규칙). 마스크 = 저작 98(적 진영 전부 — 옛
        // `DefenderTargetDefaults.Resolve` · 팩션 필터 `BattleBridge.cs:8135`), 거점은 따로 사각 반폭을 두르고
        // (`:8158-8160`) 순위는 **종류를 안 가리는** 순수 최근접(`:8169` `RanksBefore`)이다. 그래서 거점이
        // 스폰한 적보다 가까우면 옛 가이드는 거점을 봤다 — 「적 유닛만 겨눈다」로 좁히면 이 테스트가 빨개진다.
        // 레인 0 스폰 자리 옆에 적 본능 거점이 서 있다(첫 테스트가 그 배치에서 이 규칙을 밟았다).
        [UnityTest]
        public IEnumerator 적_거점이_스폰한_적보다_가까우면_가이드는_거점을_향한다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            driver.Apply(Command.FinishPlacement());
            driver.Pause(true);
            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay);

            var def = driver.Definition;
            int shaped = -1;
            for (int i = 0; i < def.Units.Length && shaped < 0; i++)
            {
                var u = def.Units[i];
                if (u.AttackRange <= 0f || u.Attack.ShapeKind != AttackShapeBaked.SectorKind) continue;
                if ((TargetDefaults.ResolveDefender(u.TargetFactions) & (int)Wassup.Battle.Units.Faction.EnemyUnit) == 0)
                    continue;
                shaped = i;
            }
            Assert.GreaterOrEqual(shaped, 0, "로스터에 적을 겨누는 부채꼴 유닛이 없다");
            var su = def.Units[shaped];
            int mask = TargetDefaults.ResolveDefender(su.TargetFactions);

            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(0, 0)).Accepted, "적 스폰 거절");
            Unit enemy = LatestEnemy(driver);
            Assert.IsNotNull(enemy, "적이 안 섰다");
            yield return null;

            // 앵커 = 적과 적 거점이 **둘 다** 사거리 안이고 거점이 **더 가까운** 자리. 도달은 정본 진입점으로 묻는다.
            float ts = driver.TileSize;
            int w = math.max(1, su.FootprintWidth);
            var size = driver.GridSize;
            var units = driver.Units;
            Unit structure = null;
            int2 anchor = default;
            float3 foot = default;
            for (int y = 0; y < size.y && structure == null; y++)
            for (int x = 0; x < size.x && structure == null; x++)
            {
                var f = new float3((x + (w - 1) * 0.5f) * ts, 0f, y * ts);
                if (!AttackReach.InReach(f, enemy.Position, su.AttackRange, ts, su.BodyRadiusTiles, enemy.HitRadius)) continue;
                float ex = enemy.Position.x - f.x, ez = enemy.Position.z - f.z;
                float enemySq = ex * ex + ez * ez;
                if (enemySq < 0.25f * ts * ts) continue;
                for (int i = 0; i < units.Count; i++)
                {
                    var o = units[i];
                    if (o.Kind != UnitKind.Structure || ((int)o.Faction & mask) == 0 || !o.IsTargetable()) continue;
                    if (!AttackReach.InReach(f, o.Position, su.AttackRange, ts, su.BodyRadiusTiles, o.HitRadius)) continue;
                    float ox = o.Position.x - f.x, oz = o.Position.z - f.z;
                    float sq = ox * ox + oz * oz;
                    if (sq < 0.25f * ts * ts || sq >= enemySq) continue;
                    structure = o; anchor = new int2(x, y); foot = f;
                    break;
                }
            }
            Assert.IsNotNull(structure, "적과 적 거점이 함께 사거리 안이고 거점이 더 가까운 앵커가 없다 — 증언할 수 없다");

            overlay.ShowPlacement(shaped, anchor, true);
            yield return null;
            yield return null;

            Assert.IsTrue(overlay.TryGetShapeGuide(out _, out _, out var dirView), "사거리 안에 적과 거점이 있는데 가이드가 없다");
            Vector3 footView = BoardSpace.ToView(foot);
            Vector3 toStructure = (Vector3)BoardSpace.ToView(structure.Position) - footView;
            Vector3 toEnemy = (Vector3)BoardSpace.ToView(enemy.Position) - footView;
            Assert.Greater(Vector3.Dot(dirView.normalized, toStructure.normalized), 0.999f,
                $"가이드가 더 가까운 적 거점({structure.Faction})을 향하지 않는다 — 후보에서 거점을 뺐거나 순위가 종류를 가린다");
            Assert.Less(Vector3.Dot(dirView.normalized, toEnemy.normalized), 0.999f,
                "가이드가 거점이 아니라 더 먼 적 유닛을 향한다");

            overlay.HidePlacement();
            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        private static Unit LatestEnemy(BattleDriver driver)
        {
            Unit e = null;
            var us = driver.Match.World.Units;
            for (int i = 0; i < us.Count; i++)
                if (us[i].Kind == UnitKind.Enemy && (e == null || us[i].Id.Value > e.Id.Value)) e = us[i];
            return e;
        }

        private static float PlanarDistance(Vector3 a, Vector3 b)
        {
            var n = BoardSpace.RaycastPlane().normal;
            var d = a - b;
            return (d - Vector3.Dot(d, n) * n).magnitude;
        }

        // 적이 그 유닛의 사거리 안에 드는 앵커. 도달은 **정본 진입점**으로 묻는다(테스트도 자를 새로 안 만든다).
        // 발밑 = 오버레이·배치와 같은 점(`CoreMapOverlay.PaintRange` — 앵커 x + (폭−1)/2, 앵커 y).
        //
        // ⚠ 그 적이 사거리 안 **유일한 후보**여야 한다. 후보 마스크(저작 98 = 적 진영 전부)에는 **적 거점**도
        // 들고(옛 `DefenderTargetDefaults.Resolve` + `BattleBridge.cs:8135` 팩션 필터), 순위는 순수 최근접
        // (`:8169` `RanksBefore`)이라 거점이 더 가까우면 옛 가이드도 거점을 봤다. 레인 0 스폰 옆에는 적 본능
        // 거점이 서 있어, 첫 앵커를 그냥 쓰면 가이드가 거점 쪽(0.98)을 향한다 — 규칙이 아니라 배치가 틀린 것이다.
        // 거점이 사거리 안에 **남아 있기만** 해도 그 적을 지운 뒤 가이드가 거점으로 옮겨 가(옛 것도 그랬다)
        // 「후보가 없으면 안 뜬다」를 증언할 수 없다. 그래서 마스크 안의 다른 유닛이 사거리에 **하나라도** 드는
        // 앵커는 건너뛴다 — 그 적이 유일한 후보인 자리만 쓴다(층·IsTargetable 은 안 걸러 더 보수적이다 —
        // 건너뛰는 앵커가 늘 뿐 오판은 없다).
        private static bool TryAnchorInReach(BattleDriver driver, int defIndex, Unit enemy,
                                             out int2 anchor, out float3 foot)
        {
            ref var u = ref driver.Definition.Units[defIndex];
            int w = math.max(1, u.FootprintWidth);
            float ts = driver.TileSize;
            var size = driver.GridSize;
            int mask = TargetDefaults.ResolveDefender(u.TargetFactions);
            var units = driver.Units;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var f = new float3((x + (w - 1) * 0.5f) * ts, 0f, y * ts);
                float dx = enemy.Position.x - f.x, dz = enemy.Position.z - f.z;
                if (dx * dx + dz * dz < 0.25f * ts * ts) continue;   // 같은 자리면 방향이 없다
                if (!AttackReach.InReach(f, enemy.Position, u.AttackRange, ts, u.BodyRadiusTiles, enemy.HitRadius))
                    continue;
                bool rivalInReach = false;
                for (int i = 0; i < units.Count && !rivalInReach; i++)
                {
                    var o = units[i];
                    if (o == enemy || ((int)o.Faction & mask) == 0) continue;
                    rivalInReach = AttackReach.InReach(f, o.Position, u.AttackRange, ts, u.BodyRadiusTiles, o.HitRadius);
                }
                if (rivalInReach) continue;
                anchor = new int2(x, y);
                foot = f;
                return true;
            }
            anchor = default;
            foot = default;
            return false;
        }
    }
}
