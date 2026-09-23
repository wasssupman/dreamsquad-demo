using System.Collections;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.View;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **배치 하이라이트가 옛 룩인가.**
    //
    // 사용자 플레이 1차의 문장: **「배치 하이라이트가 옛 룩이 아니다」**. 5b 의 오버레이는
    // 배치 가능 칸을 **코드에 박은 초록**(0.45, 0.92, 0.5, α0.16)으로 칠했다. 정본
    // (`docs/spec/placement-eligible-tile-highlight/`)은 **타일셋의 슬랩 타일 + `placeableColor`
    // 시안 · 정적 · 페이드인**이고 **초록은 hover 전용**이다.
    //
    // 그래서 단언은 값이 아니라 **출처**다: 「그림과 색이 타일셋에서 나왔는가」. 색 하나를
    // 못박으면 다음 튜닝이 이 테스트를 빨갛게 만들고, 그건 저작을 막는 테스트다.
    public sealed class CorePlacementHighlightTests
    {
        [UnityTest]
        public IEnumerator 배치_하이라이트는_타일셋의_슬랩과_색에서_나온다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var overlay = Object.FindAnyObjectByType<CoreMapOverlay>();
            Assert.IsNotNull(overlay, "맵 오버레이가 씬에 없다");
            Assert.IsNotNull(overlay.TileSet,
                "오버레이에 타일셋이 없다 — 하이라이트 룩이 코드 상수라는 뜻이다");

            var slab = overlay.TileSet.placeableTile as UnityEngine.Tilemaps.Tile;
            Assert.IsNotNull(slab, "타일셋에 배치 슬랩 타일(placeableTile)이 없다");
            Assert.IsNotNull(slab.sprite, "슬랩 타일에 스프라이트가 없다");

            driver.Apply(Command.FinishPlacement());
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor),
                "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");

            // 드래그를 흉내 내는 것은 이 한 줄이다(입력이 매 프레임 미는 값).
            overlay.ShowPlacement(defIndex, anchor, true);
            yield return null;
            yield return null;                  // LateUpdate 두 번 — 첫 프레임에 칠하고 둘째에 틴트

            var renderers = overlay.GetComponentsInChildren<SpriteRenderer>(true);
            SpriteRenderer painted = null;
            int slabCells = 0;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled || renderers[i].sprite != slab.sprite) continue;
                slabCells++;
                if (painted == null) painted = renderers[i];
            }

            Assert.IsNotNull(painted,
                "배치 가이드가 타일셋의 슬랩 타일로 칠해지지 않았다 — 룩이 아직 코드의 것이다");
            Assert.Greater(slabCells, 1, "밝힌 칸이 하나뿐이다 — 가이드가 판을 안 읽었다");

            // 색의 출처. 최종 틴트 = `placeableColor` × **타일 자신의 색**(슬랩은 자체 m_Color 가
            // 회색이라 그걸 빼면 옛 타일맵보다 밝게 뜬다). 알파는 페이드가 곱해져 있으므로
            // 상한만 본다.
            var mpb = new MaterialPropertyBlock();
            painted.GetPropertyBlock(mpb);
            var got = mpb.GetColor(Shader.PropertyToID("_BaseColor"));
            var want = overlay.TileSet.placeableColor;
            var tileColor = slab.color;

            Assert.AreEqual(want.r * tileColor.r, got.r, 1e-3f, "가이드 R 이 저작에서 나오지 않았다");
            Assert.AreEqual(want.g * tileColor.g, got.g, 1e-3f, "가이드 G 가 저작에서 나오지 않았다");
            Assert.AreEqual(want.b * tileColor.b, got.b, 1e-3f, "가이드 B 가 저작에서 나오지 않았다");
            Assert.LessOrEqual(got.a, want.a * tileColor.a + 1e-3f,
                "가이드 알파가 저작 상한을 넘었다 — 페이드가 아니라 덮어쓰기다");
            Assert.Greater(got.a, 0f, "가이드가 완전히 투명하다");

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

            // 하이라이트는 «공간 조건»을 말하는 표시다. 자원을 섞으면 코스트 재생 경계마다
            // 보드 전체가 깜빡이고, 못 사는 유닛을 끌 때 「놓을 곳이 한 칸도 없다」고
            // 거짓말한다 — 「밝은 칸인데 비용이 모자라 고스트는 빨강」이 정상이다.
            Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor));
            int price = driver.Definition.Units[defIndex].Cost;
            Assert.Greater(price, 0, "값이 0 인 유닛으로는 이 계약을 증언할 수 없다");

            // 잔액을 0 으로 만든다. 놓아서 빼면 판 위 상한이 먼저 걸려 **다른 이유**로 막히므로
            // 장부에 직접 묻는다 — 재생은 틱이 돌 때만 일어나고 여기는 같은 프레임이다.
            var ledger = driver.Match.Cost;
            Assert.IsTrue(ledger.TryPay(ledger.CurrentInt, driver.Match.Clock.Tick),
                "잔액을 비우지 못했다");
            Assert.IsFalse(ledger.CanAfford(price), "못 사는 상태를 만들지 못했다");

            Assert.AreEqual(RejectReason.InsufficientCost, placement.SlotBlock(defIndex),
                "잔액이 비었는데 슬롯이 「쓸 수 있다」고 답한다");
            Assert.AreEqual(RejectReason.None, placement.SpaceBlock(defIndex, anchor),
                "코스트가 모자란다고 «공간» 판정까지 막혔다 — 하이라이트가 통째로 꺼진다");
            Assert.AreEqual(RejectReason.InsufficientCost, placement.Judge(defIndex, anchor),
                "고스트(전부 판정)는 여전히 「못 산다」고 답해야 한다");
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
