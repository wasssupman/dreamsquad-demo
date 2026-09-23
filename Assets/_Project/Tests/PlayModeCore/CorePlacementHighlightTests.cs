using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.View;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **배치 하이라이트가 규칙을 옳게 가르치는가.**
    //
    // 사용자 플레이 1차의 문장: 「배치 하이라이트가 옛 룩이 아니다」 → 답(2026-09-23):
    // **못 놓는 칸을 칠하고, 「지형·프랍이 막았다」와 「유닛이 서 있다」를 색으로 가른다.**
    // 놓을 수 있는 칸은 안 칠한다.
    //
    // 단언은 값이 아니라 **출처**다: 그림과 두 색이 `TileSetData` 에서 나왔는가. 색 하나를
    // 못박으면 다음 튜닝이 이 테스트를 빨갛게 만들고, 그건 저작을 막는 테스트다.
    public sealed class CorePlacementHighlightTests
    {
        [UnityTest]
        public IEnumerator 하이라이트는_못_놓는_칸을_이유별_두_색으로_칠한다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay, "맵 오버레이가 씬에 없다");
            Assert.IsNotNull(overlay.TileSet,
                "오버레이에 타일셋이 없다 — 하이라이트 룩이 코드 상수라는 뜻이다");

            var set = overlay.TileSet;
            var tile = set.blockedTile as UnityEngine.Tilemaps.Tile;
            Assert.IsNotNull(tile, "타일셋에 못 놓는 칸 타일(blockedTile)이 없다");
            Assert.IsNotNull(tile.sprite, "그 타일에 스프라이트가 없다");
            Assert.AreNotEqual(set.blockedColor, set.occupiedColor,
                "지형과 유닛 점유가 같은 색이다 — 플레이어가 둘을 구분할 수 없다");

            driver.Apply(Command.FinishPlacement());

            // 유닛이 하나는 서 있어야 「유닛이 막은 칸」을 증언할 수 있다.
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");
            Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
            yield return null;

            overlay.ShowPlacement(defIndex, anchor, false);
            yield return null;
            yield return null;                  // LateUpdate 두 번 — 칠하고 나서 틴트

            // 「손가락 칸」이 정본이다(오버레이와 **같은 함수**로 앵커를 만든다 — 규칙의 두 번째
            // 사본을 세우지 않는다).
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            var fp = new Vector2Int(math.max(1, driver.Definition.Units[defIndex].FootprintWidth),
                                    math.max(1, driver.Definition.Units[defIndex].FootprintHeight));
            int2 freeCell = default, terrainCell = default, occupiedCell = default;
            bool hasFree = false, hasTerrain = false, hasOccupied = false;
            for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
            {
                var a = FootprintMath.AnchorFromBottomCenter(new Vector2Int(x, y), fp);
                switch (placement.SpaceBlock(defIndex, new int2(a.x, a.y)))
                {
                    case RejectReason.None:
                        if (!hasFree) { freeCell = new int2(x, y); hasFree = true; }
                        break;
                    case RejectReason.Occupied:
                        if (!hasOccupied) { occupiedCell = new int2(x, y); hasOccupied = true; }
                        break;
                    case RejectReason.NotBuildable:
                    case RejectReason.OutOfBounds:
                        if (!hasTerrain) { terrainCell = new int2(x, y); hasTerrain = true; }
                        break;
                }
            }
            Assert.IsTrue(hasFree, "놓을 수 있는 칸이 하나도 없다 — 이 판으로는 증언할 수 없다");
            Assert.IsTrue(hasTerrain, "지형·프랍이 막은 칸이 하나도 없다");
            Assert.IsTrue(hasOccupied, "유닛이 막은 칸이 하나도 없다 — 배치가 안 반영됐다");

            // (i) 놓을 수 있는 칸은 **안 칠한다**.
            Assert.IsFalse(TryFindCellTint(overlay, driver, tile, freeCell, out _),
                "놓을 수 있는 칸이 칠해졌다 — 빈 땅이 곧 「여기 된다」여야 한다");

            // (ii) 지형·프랍 = blockedColor.
            Color got;
            Assert.IsTrue(TryFindCellTint(overlay, driver, tile, terrainCell, out got),
                "지형·프랍이 막은 칸이 안 칠해졌다");
            AssertTint(set.blockedColor, tile.color, got, "지형·프랍");

            // (iii) 유닛 점유 = occupiedColor.
            Assert.IsTrue(TryFindCellTint(overlay, driver, tile, occupiedCell, out got),
                "유닛이 막은 칸이 안 칠해졌다");
            AssertTint(set.occupiedColor, tile.color, got, "유닛 점유");

            overlay.HidePlacement();
        }

        [UnityTest]
        public IEnumerator 하이라이트는_코스트를_보지_않는다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            driver.Apply(Command.FinishPlacement());

            var placement = driver.Match.Placement;

            // 하이라이트는 «공간 조건»을 말하는 표시다. 자원을 섞으면 그 사유가 **모든 칸에
            // 똑같이** 붙어 보드 전체가 칠해지고, 코스트 재생 경계마다 판이 통째로 깜빡인다.
            // 「칠해지지 않은 칸인데 비용이 모자라 고스트는 빨강」이 정상이다.
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor));
            int price = driver.Definition.Units[defIndex].Cost;
            Assert.Greater(price, 0, "값이 0 인 유닛으로는 이 계약을 증언할 수 없다");

            var ledger = driver.Match.Cost;
            Assert.IsTrue(ledger.TryPay(ledger.CurrentInt, driver.Match.Clock.Tick),
                "잔액을 비우지 못했다");
            Assert.IsFalse(ledger.CanAfford(price), "못 사는 상태를 만들지 못했다");

            Assert.AreEqual(RejectReason.InsufficientCost, placement.SlotBlock(defIndex),
                "잔액이 비었는데 슬롯이 「쓸 수 있다」고 답한다");
            Assert.AreEqual(RejectReason.None, placement.SpaceBlock(defIndex, anchor),
                "코스트가 모자란다고 «공간» 판정까지 막혔다 — 보드 전체가 칠해진다");
            Assert.AreEqual(RejectReason.InsufficientCost, placement.Judge(defIndex, anchor),
                "고스트(전부 판정)는 여전히 「못 산다」고 답해야 한다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private static void AssertTint(Color authored, Color tileColor, Color got, string who)
        {
            Assert.AreEqual(authored.r * tileColor.r, got.r, 1e-3f, who + " 칸의 R 이 저작에서 나오지 않았다");
            Assert.AreEqual(authored.g * tileColor.g, got.g, 1e-3f, who + " 칸의 G 가 저작에서 나오지 않았다");
            Assert.AreEqual(authored.b * tileColor.b, got.b, 1e-3f, who + " 칸의 B 가 저작에서 나오지 않았다");
            Assert.LessOrEqual(got.a, authored.a * tileColor.a + 1e-3f,
                who + " 칸의 알파가 저작 상한을 넘었다 — 페이드가 아니라 덮어쓰기다");
            Assert.Greater(got.a, 0f, who + " 칸이 완전히 투명하다");
        }

        // 그 칸 자리에 칠해진 슬래브가 있으면 그 틴트를 준다. 자리로 찾는 이유: 오버레이가
        // 「어느 칸을 칠했나」를 내주는 창구를 따로 두면 그게 테스트 전용 표면이 된다.
        private static bool TryFindCellTint(CoreMapOverlay overlay, BattleDriver driver,
                                            UnityEngine.Tilemaps.Tile tile, int2 cell, out Color tint)
        {
            float ts = driver.TileSize;
            Vector3 want = (Vector3)Wassup.Core.BoardSpace.ToView(new float3(cell.x * ts, 0f, cell.y * ts));
            var mpb = new MaterialPropertyBlock();
            var renderers = overlay.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                var sr = renderers[i];
                if (!sr.enabled || sr.sprite != tile.sprite) continue;
                // 표면 오프셋(z-fight 회피)만큼 떠 있으므로 반 칸 안이면 그 칸이다.
                if (Vector3.Distance(sr.transform.position, want) > ts * 0.45f) continue;
                sr.GetPropertyBlock(mpb);
                tint = mpb.GetColor(Shader.PropertyToID("_BaseColor"));
                return true;
            }
            tint = default;
            return false;
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var c = new int2(x, y);
                    if (placement.Judge(i, c) != RejectReason.None) continue;
                    defIndex = i;
                    anchor = c;
                    return true;
                }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }
    }
}
