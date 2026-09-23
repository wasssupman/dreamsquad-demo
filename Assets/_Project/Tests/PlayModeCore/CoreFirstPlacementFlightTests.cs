using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **첫 배치도 나는가.**
    //
    // 사용자 플레이 2차의 문장: **「첫 번째 유닛 배치 시 비행 미진행」**.
    //
    // 조건이 「첫 번째」인 것이 단서다: 입력이 배치 사건을 기다리는 표식을 `Apply` **뒤**에
    // 켰는데, `BattleDriver.Apply` 는 **그 호출 안에서** 사건을 배달한다. 그래서 첫 배치의
    // 사건은 표식이 꺼진 채 지나가고, 두 번째부터는 직전 배치가 남긴 표식이 우연히 켜져
    // 있어 난다 — **한 판 늦은 걸쇠**다.
    public sealed class CoreFirstPlacementFlightTests
    {
        [UnityTest]
        public IEnumerator 첫_배치도_비행하고_착지_신호를_낸다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            var flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            Assert.IsNotNull(input, "배치 입력이 씬에 없다");
            Assert.IsNotNull(flight, "배치 비행 프리젠터가 씬에 없다");
            Assert.AreEqual(0, flight.FlightCount, "판이 시작부터 비행 중이다");

            driver.Apply(Command.FinishPlacement());

            var activated = new List<SimEntityId>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.DefenderActivated) activated.Add(e.A);
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            try
            {
                var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.1f);

                // ── 첫 배치 ──────────────────────────────────────────────────
                Assert.IsTrue(TryFindPlaceable(driver, out int first, out int2 firstAnchor),
                    "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");
                Assert.IsTrue(input.TryPlace(first, firstAnchor, from), "첫 배치가 거절됐다");

                Assert.AreEqual(1, flight.FlightCount,
                    "첫 배치에 비행이 안 떴다 — 두 번째부터만 나는 「한 판 늦은 걸쇠」다");

                // 착지 신호까지 나갔나 — 배치 페이즈가 **착지 기준으로** 끝나야 한다.
                float waited = 0f;
                while (flight.FlightCount > 0 && waited < 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.AreEqual(0, flight.FlightCount, "첫 비행이 3초가 지나도 안 끝났다");

                float motion = driver.Definition.Units[first].DeployMotionSeconds;
                if (motion > 0f)
                {
                    float deadline = motion * 2f + 1f;
                    float t = 0f;
                    while (activated.Count == 0 && t < deadline)
                    {
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Assert.AreEqual(1, activated.Count,
                        "첫 유닛이 활성화되지 않았다 — 착지 신호가 안 나갔다");
                }

                // ── 두 번째 배치 ─────────────────────────────────────────────
                // 「첫 번째만 안 난다」의 반쪽이다. 둘 다 나야 걸쇠가 사라진 것이다.
                if (TryFindPlaceable(driver, out int second, out int2 secondAnchor, first))
                {
                    Assert.IsTrue(input.TryPlace(second, secondAnchor, from), "두 번째 배치가 거절됐다");
                    Assert.AreEqual(1, flight.FlightCount, "두 번째 배치의 비행이 안 떴다");
                }
            }
            finally
            {
                driver.Unsubscribe(probe);
            }

            CoreSceneFixture.EndErrorWatch();
            Assert.AreEqual(0, CoreSceneFixture.Errors.Count,
                "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        [UnityTest]
        public IEnumerator 거절된_배치는_비행을_남기지_않는다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);
            var input = Object.FindAnyObjectByType<DragPlacementInput>();
            var flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            Assert.IsNotNull(input);
            Assert.IsNotNull(flight);
            driver.Apply(Command.FinishPlacement());

            // 판 밖 — 공간이 거절한다. 걸쇠가 남으면 **다음** 배치가 남의 출발점으로 난다.
            Assert.IsFalse(input.TryPlace(0, new int2(9999, 9999),
                                          new Vector2(Screen.width * 0.5f, Screen.height * 0.1f)));
            Assert.AreEqual(0, flight.FlightCount, "거절됐는데 비행이 떴다");
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex, out int2 anchor,
                                             int skipIndex = -1)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (i == skipIndex || !placement.InRoster(i)) continue;
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
