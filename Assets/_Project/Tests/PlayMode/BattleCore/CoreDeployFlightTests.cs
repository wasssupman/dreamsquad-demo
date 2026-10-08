using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.BattleCoreUnity.View;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // battle-core-rebuild 5b 수정 — **배치 비행이 있는가.**
    //
    // 사용자 플레이 1차의 문장: **「유닛이 툭 생긴다」**. 증상 단언은 「놓은 유닛의 뷰가
    // 한동안 제 칸 밖 공중에 있고, 다 날고 나서야 착지 신호가 나간다」다.
    //
    // ⚠ 포인터 제스처는 여기서도 흉내 내지 않는다(`CorePlacementFlowTests` 와 같은 이유).
    // 대신 **입력이 부르는 그 함수**(`Launch`)를 직접 부른다 — 이 테스트가 증언하는 것은
    // 비행이지 손가락이 아니다.
    public sealed class CoreDeployFlightTests
    {
        [UnityTest]
        public IEnumerator 배치한_유닛은_공중을_날아와_착지한다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");

            var flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            Assert.IsNotNull(flight,
                "배치 비행 프리젠터가 씬에 없다 — 놓은 유닛이 「툭」 생긴다");

            var pool = Object.FindAnyObjectByType<CoreUnitViewPool>();
            Assert.IsNotNull(pool, "유닛 뷰 풀이 씬에 없다");

            driver.Apply(Command.FinishPlacement());

            // 놓인 개체의 id 는 **사건만이 안다**(receipt 는 「받아들여졌다」만 말한다).
            var placed = new List<SimEntityId>();
            var activated = new List<SimEntityId>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.Placed) placed.Add(e.A);
                else if (e.Kind == CoreEventKind.DefenderActivated) activated.Add(e.A);
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            try
            {
                Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor),
                    "로스터의 어떤 유닛도 이 판 어디에도 놓을 수 없다");

                var receipt = driver.Apply(Command.PlaceDefender(defIndex, anchor));
                Assert.IsTrue(receipt.Accepted, "판정이 통과한 자리인데 거절됐다: " + receipt.Reason);
                Assert.AreEqual(1, placed.Count, "배치 사건이 정확히 한 번 나야 한다");
                var id = placed[0];

                // 입력이 드롭 순간에 하는 그 한 줄. 출발점 = 트레이 칸이 있는 화면 아래쪽.
                var from = new Vector2(Screen.width * 0.5f, Screen.height * 0.1f);
                Assert.IsTrue(flight.Launch(id, from),
                    "비행이 안 떴다 — 저작(DragSwaySettings)이나 보드 평면이 없다");
                Assert.IsTrue(flight.IsFlying(id));

                // 제 칸에 섰을 때의 자리. 「날았다」는 이 점과 **다르다**는 뜻이다.
                Vector3 rest = (Vector3)Somnia.Battle.Core.BoardSpace.ToView(driver.Find(id).Position);

                // 반동(웅크림) 구간이 지나야 뜨기 시작한다 — 몇 프레임 준다.
                float lifted = 0f;
                Vector3 apex = rest;
                for (int i = 0; i < 12 && flight.IsFlying(id); i++)
                {
                    yield return null;
                    if (!flight.TryGetFlightView(id, out var p, out float lift, out _)) break;
                    if (lift > lifted) { lifted = lift; apex = p; }
                }

                Assert.Greater(lifted, 0.01f,
                    "비행 중에 한 번도 뜨지 않았다 — 「툭 생긴다」와 같은 그림이다");
                Assert.Greater(Vector3.Distance(apex, rest), 0.01f,
                    "비행 중 뷰가 제 칸 자리에 붙어 있었다 — 날아오지 않았다");

                // 끝까지. 비행 길이는 저작(≈0.45초)이라 넉넉히 기다린다.
                float waited = 0f;
                while (flight.IsFlying(id) && waited < 3f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
                Assert.IsFalse(flight.IsFlying(id), "비행이 3초가 지나도 안 끝났다");
                Assert.AreEqual(0, flight.FlightCount, "공중에 남은 연출이 있다");

                // 착지 신호가 나갔다 = 코어의 배치 페이즈가 **착지 기준으로 다시 세어** 끝난다.
                // 신호가 아예 안 나갔다면 활성화는 배치 시점 기준이라 이 시점에 이미 지나 있다.
                float motion = driver.Definition.Units[defIndex].DeployMotionSeconds;
                if (motion > 0f)
                {
                    Assert.AreEqual(0, activated.Count,
                        "착지 전에 활성화됐다 — 착지 신호가 비행을 기다리지 않았다");
                    float deadline = motion * 2f + 1f;
                    float t = 0f;
                    while (activated.Count == 0 && t < deadline)
                    {
                        t += Time.unscaledDeltaTime;
                        yield return null;
                    }
                    Assert.AreEqual(1, activated.Count,
                        "착지 뒤 활성화가 정확히 한 번 나야 한다(착지 신호가 없거나 두 번이다)");
                    Assert.AreEqual(id, activated[0]);
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
        public IEnumerator 비행이_끊겨도_착지_신호는_반드시_나간다()
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);

            var flight = Object.FindAnyObjectByType<CoreDeployFlightPresenter>();
            Assert.IsNotNull(flight);
            driver.Apply(Command.FinishPlacement());

            var placed = new List<SimEntityId>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.Placed) placed.Add(e.A);
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            try
            {
                Assert.IsTrue(TryFindPlaceable(driver, out int defIndex, out int2 anchor));
                Assert.IsTrue(driver.Apply(Command.PlaceDefender(defIndex, anchor)).Accepted);
                var id = placed[0];

                Assert.IsTrue(flight.Launch(id, new Vector2(Screen.width * 0.5f, Screen.height * 0.1f)));
                yield return null;
                Assert.IsTrue(flight.IsFlying(id));

                // 판 중간에 연출이 끊긴다(씬 teardown · 컴포넌트 비활성). **착지는 그래도 나간다** —
                // 안 나가면 그 유닛은 영영 「배치 중」이라 사냥판의 소스도 표적도 아니게 되고,
                // 화면에는 멀쩡히 서 있어서 「가끔 한 놈이 아무것도 안 한다」로만 보인다.
                flight.enabled = false;
                yield return null;
                Assert.AreEqual(0, flight.FlightCount, "끊긴 비행이 남았다");

                // 착지가 나갔으면 코어의 배치 페이즈가 **다시 세어** 끝난다 — 비활성 상태로도.
                float motion = driver.Definition.Units[defIndex].DeployMotionSeconds;
                float deadline = motion * 2f + 1f;
                float t = 0f;
                while (t < deadline)
                {
                    var u = driver.Find(id);
                    if (u == null || !u.Deploying) break;
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
                var unit = driver.Find(id);
                Assert.IsNotNull(unit, "유닛이 사라졌다");
                Assert.IsFalse(unit.Deploying,
                    "끊긴 비행 뒤에도 영영 배치 중이다 — 착지 신호가 안 나갔다");
            }
            finally
            {
                driver.Unsubscribe(probe);
                flight.enabled = true;
            }
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

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
