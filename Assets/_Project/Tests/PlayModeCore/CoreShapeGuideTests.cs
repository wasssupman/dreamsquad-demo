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

            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(0, 0)).Accepted, "적 스폰 거절");
            var world = driver.Match.World;
            Unit enemy = null;
            for (int i = 0; i < world.Units.Count; i++)
                if (world.Units[i].Kind == UnitKind.Enemy) enemy = world.Units[i];
            Assert.IsNotNull(enemy, "적이 안 섰다");
            yield return null;

            // ── 방향 유닛: 뜬다 · 적을 향한다 · 길이 = 범위 + 자기 몸 ──
            Assert.IsTrue(TryAnchorInReach(driver, shaped, enemy, out int2 anchor, out float3 foot),
                "적이 사거리 안에 드는 앵커가 판에 없다");
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

        // 적이 그 유닛의 사거리 안에 드는 앵커. 도달은 **정본 진입점**으로 묻는다(테스트도 자를 새로 안 만든다).
        // 발밑 = 오버레이·배치와 같은 점(`CoreMapOverlay.PaintRange` — 앵커 x + (폭−1)/2, 앵커 y).
        private static bool TryAnchorInReach(BattleDriver driver, int defIndex, Unit enemy,
                                             out int2 anchor, out float3 foot)
        {
            ref var u = ref driver.Definition.Units[defIndex];
            int w = math.max(1, u.FootprintWidth);
            float ts = driver.TileSize;
            var size = driver.GridSize;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var f = new float3((x + (w - 1) * 0.5f) * ts, 0f, y * ts);
                float dx = enemy.Position.x - f.x, dz = enemy.Position.z - f.z;
                if (dx * dx + dz * dz < 0.25f * ts * ts) continue;   // 같은 자리면 방향이 없다
                if (!AttackReach.InReach(f, enemy.Position, u.AttackRange, ts, u.BodyRadiusTiles, enemy.HitRadius))
                    continue;
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
