using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCore.Map;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.View;
using Wassup.Skills;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 8a2 — **뷰 이전 잔여 8행**이 새 씬에서 실체를 갖는가(`--owners` 가 「미실현」으로 세던 것).
    //
    // 행마다 한 테스트: 사건(또는 입력) → 뷰 호출을 증언한다. 사건은 되도록 **코어의 진짜 문**으로 낸다
    // (디버그 커맨드 · `Intents.Apply` · 월드의 스폰) — 가짜 사건을 버스에 밀면 「뷰가 코어가 실제로 내는
    // 모양을 받는가」를 못 잰다. 판을 멈추고(`Pause`) 틱을 손으로 민다(`CoreEffectViewTests` 와 같은 규율).
    public sealed class CoreViewRemainderTests
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
            yield return null;   // 뷰의 LateUpdate 가 그린다
        }

        private static Unit SpawnEnemy(BattleDriver driver)
        {
            Assert.IsTrue(driver.Apply(Command.DebugSpawnEnemyInLane(0, 0)).Accepted, "적 스폰 거절");
            var units = driver.Match.World.Units;
            Unit enemy = null;
            for (int i = 0; i < units.Count; i++)
                if (units[i].Kind == UnitKind.Enemy) enemy = units[i];   // 가장 최근
            Assert.IsNotNull(enemy, "적이 안 섰다");
            return enemy;
        }

        private static void AssertNoErrors()
        {
            CoreSceneFixture.EndErrorWatch();
            Assert.IsEmpty(CoreSceneFixture.Errors, string.Join("\n", CoreSceneFixture.Errors));
        }

        // ── 행 1 — 효과 타일 칸 표시(T15) ────────────────────────────────────────────
        [UnityTest]
        public IEnumerator 행1_효과_타일_칸이_판_위에_그_종류의_그림으로_칠해진다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay, "씬에 CoreMapOverlay 가 없다");
            yield return Ticks(driver, 0);

            var placement = driver.Match.Placement;
            var armed = placement.ArmedEffectTiles;
            // 라이브 테마(시즌 등록부 → 맵 테마)는 3칸이다 — 0 이면 새 씬이 시즌을 안 묶은 것이다(옛 `BattleBridge.Awake:685`).
            Assert.Greater(armed.Count, 0, "효과 타일이 한 칸도 안 뽑혔다 — 드라이버 _seasonRegistry 배선 확인");
            Assert.AreEqual(armed.Count, overlay.EffectTileCellCount, "뽑힌 칸 수 = 칠한 칸 수");
            for (int i = 0; i < armed.Count; i++)
            {
                Assert.IsTrue(overlay.TryGetEffectTileCell(i, out var cell, out var sprite), $"칸 {i} 가 칠해지지 않았다");
                Assert.AreEqual(armed[i], cell, "칠한 칸 = 코어가 뽑은 칸");
                var data = driver.ViewAssets.EffectTile(placement.EffectTileKindAt(cell));
                var tile = data != null ? data.overlayTile as UnityEngine.Tilemaps.Tile : null;
                Assert.IsNotNull(tile, "종류의 저작 타일");
                Assert.AreSame(tile.sprite, sprite, "그림 = 그 종류의 저작 타일(옛 SetEffectTile)");
            }
            AssertNoErrors();
        }

        // ── 행 2 — 궁극기 착지 예고(T16·T17) ──────────────────────────────────────
        [UnityTest]
        public IEnumerator 행2_궁극기_이탈에_착지_칸_예고가_뜨고_배치_드래그가_지우지_않으며_강하에_내린다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay, "씬에 CoreMapOverlay 가 없다");

            var enemy = SpawnEnemy(driver);
            var map = driver.Match.Map;
            var cell = map.CellOf(enemy.Position);
            const int slamTiles = 2;
            driver.Match.Intents.Apply(new SimIntent
            {
                Kind = SimIntentKind.BeginUltimateLeap,
                Target = Wassup.BattleCore.Trigger.CoreSkillContext.ToSkill(enemy.Id),
                Cell = cell, Position = map.CenterOf(cell),
                Duration = 0.5f, Amount = 0f, TileRange = slamTiles, DataIndex = -1,
            });
            yield return Ticks(driver, 0);

            Assert.IsTrue(overlay.TryGetLandingTelegraph(out var leaper, out var center, out float radius),
                "이탈 사건 → 착지 예고 링");
            Assert.AreEqual(enemy.Id, leaper, "예고의 주인 = 도약자");
            Assert.AreEqual(slamTiles + Wassup.Skills.SkillMath.CellShapePaddingTiles, radius, 1e-5f,
                "반경 = 슬램 칸 수 + 칸 반폭(자리형 — 보스 몸 안 읽음, 옛 CenteredRingRadius)");
            Assert.AreEqual(map.CenterOf(cell).x, center.x, 1e-4f, "중심 = 착지 칸 중심");
            Assert.AreEqual(map.CenterOf(cell).z, center.z, 1e-4f, "중심 = 착지 칸 중심");

            // T16 — 전용 채널: 배치 드래그가 예고를 지우지 않는다(예고 중 유닛을 빼고 놓는 것이 이 스킬의 놀이).
            overlay.ShowPlacement(0, new int2(0, 0), true);
            yield return null;
            Assert.IsTrue(overlay.TryGetLandingTelegraph(out _, out _, out _), "배치 드래그 중에도 예고가 남는다");
            overlay.HidePlacement();

            // 강하 = 코어가 착지를 확정한 순간 → 예고를 내린다.
            yield return Ticks(driver, 40);
            Assert.IsFalse(enemy.Progressive != null && enemy.Progressive.LeapActive, "도약이 끝나야 한다");
            Assert.IsFalse(overlay.TryGetLandingTelegraph(out _, out _, out _), "강하 사건 → 예고 내림");
            AssertNoErrors();
        }
        // ── 행 8 — 배치 사거리 칸 채움(T3·T13) ──────────────────────────────────────
        [UnityTest]
        public IEnumerator 행8_배치_드래그의_사거리_칸은_판정과_같은_자로_세고_링_안을_한_겹으로_채운다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return Boot(d => driver = d);
            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay, "씬에 CoreMapOverlay 가 없다");
            Assert.IsNotNull(overlay.TileSet, "오버레이 타일셋 저작이 없다");

            var def = driver.Definition;
            int defIndex = -1;
            for (int i = 0; i < def.Units.Length && defIndex < 0; i++)
                if (def.Units[i].AttackRange > 0f) defIndex = i;
            Assert.GreaterOrEqual(defIndex, 0, "사거리 있는 방어유닛이 없다");
            var unit = def.Units[defIndex];
            var size = driver.GridSize;
            var anchor = new int2(size.x / 2, size.y / 2);

            overlay.ShowPlacement(defIndex, anchor, true);
            yield return null;
            yield return null;

            Assert.Greater(overlay.PlacementRangeCellCount, 0, "사거리 칸이 하나도 없다");
            // 판정과 **같은 자** — 발밑 원점 · 표준 잡몹 몸(T3) · 앵커 칸 자신은 빈다(옛 includeCenter=false).
            float ts = driver.TileSize;
            int w = Mathf.Max(1, unit.FootprintWidth);
            var foot = new float3((anchor.x + (w - 1) * 0.5f) * ts, 0f, anchor.y * ts);
            int expected = 0;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var c = new int2(x, y);
                bool inReach = !c.Equals(anchor) && Wassup.BattleCore.Combat.AttackReach.InReach(
                    foot, new float3(x * ts, 0f, y * ts), unit.AttackRange, ts,
                    unit.BodyRadiusTiles, Wassup.Skills.SkillMath.StandardBodyRadiusTiles);
                if (inReach) expected++;
                Assert.AreEqual(inReach, overlay.IsPlacementRangeCell(c), $"칸 {c} 의 사거리 판단이 판정 자와 다르다");
            }
            Assert.AreEqual(expected, overlay.PlacementRangeCellCount);

            // 링 안 채움 한 겹 — 알파 = 링이 있을 때의 채움(`rangeFillAlphaUnderRing`). 칸 채움은 링이 있으면 0(그리지 않는다).
            Assert.IsTrue(overlay.TryGetRangeFill(out var fill), "링 안 채움이 없다");
            Assert.AreEqual(overlay.TileSet.rangeFillAlphaUnderRing, fill.a, 1e-4f, "채움 알파 = rangeFillAlphaUnderRing");

            overlay.HidePlacement();
            yield return null;
            Assert.AreEqual(0, overlay.PlacementRangeCellCount, "드래그가 끝나면 사거리 칸도 비운다");
            Assert.IsFalse(overlay.TryGetRangeFill(out _), "드래그가 끝나면 채움도 내린다");
            AssertNoErrors();
        }
    }
}
