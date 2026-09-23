using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5a — 뷰 방출 순서는 **`ViewOrder` 가 말한다.**
    //
    // 이 테스트가 막는 것: C# 이벤트(`+=`) 로 돌아가는 날의 조용한 회귀. 그 순서는 곧 씬
    // 컴포넌트의 나열 순서라, 하이어라키에서 오브젝트 하나를 끌어 올리면 연출 순서가 뒤집힌다.
    // 그래서 **일부러 거꾸로 구독하고** 방출 순서가 그대로인지 묻는다.
    public sealed class CoreViewOrderTests
    {
        [UnityTest]
        public IEnumerator Emission_FollowsViewOrder_NotSubscriptionOrder()
        {
            var go = new GameObject("BattleDriver_OrderProbe");
            var driver = go.AddComponent<BattleDriver>();
            var log = new List<string>();

            // 씬 순서를 뒤집은 상황 = 늦게 받아야 할 것이 **먼저** 구독한다.
            driver.Subscribe(ViewOrder.Overhead, _ => log.Add("overhead"));
            driver.Subscribe(ViewOrder.Damage, _ => log.Add("damage"));
            driver.Subscribe(ViewOrder.Projectile, _ => log.Add("projectile"));
            driver.Subscribe(ViewOrder.Unit, _ => log.Add("unit"));
            driver.Subscribe(ViewOrder.Leap, _ => log.Add("leap"));

            // 판 하나를 걸면 `Begin` 이 그 자리에서 `MatchStarted` 를 배달한다 — 사건 하나면 충분하다.
            driver.Begin(MinimalDefinition());
            yield return null;

            CollectionAssert.AreEqual(
                new[] { "leap", "unit", "projectile", "damage", "overhead" }, log,
                "구독 순서가 아니라 ViewOrder 순서로 방출돼야 한다");

            Object.DestroyImmediate(go);
        }

        [UnityTest]
        public IEnumerator Unsubscribe_DuringEmission_DoesNotSkipOthers()
        {
            var go = new GameObject("BattleDriver_UnsubProbe");
            var driver = go.AddComponent<BattleDriver>();
            var log = new List<string>();

            System.Action<CoreEvent> second = null;
            second = _ => log.Add("second");
            driver.Subscribe(ViewOrder.Leap, _ =>
            {
                log.Add("first");
                // 풀이 방출 도중 꺼지는 상황(씬 전환·teardown). 이번 방출의 명단은 흔들리면 안 된다.
                driver.Unsubscribe(second);
            });
            driver.Subscribe(ViewOrder.Unit, second);

            driver.Begin(MinimalDefinition());
            yield return null;

            CollectionAssert.AreEqual(new[] { "first", "second" }, log,
                "방출 중 구독 해지가 같은 방출의 뒷사람을 건너뛰면 안 된다");

            Object.DestroyImmediate(go);
        }

        // 최소 판. 이 테스트가 묻는 것은 **방출 순서**뿐이지만, 판을 세우려면 격자가 있어야
        // 한다(흐름장 슬롯이 셀 수에서 나온다) — 그래서 4×3 빈 판을 준다.
        private static MatchDefinition MinimalDefinition()
        {
            var map = new Wassup.BattleCore.Map.MapSnapshot
            {
                Width = 4,
                Height = 3,
                TileSize = 1f,
                Goals = new[] { new Unity.Mathematics.int2(3, 1) },
                Spawns = new[] { new Unity.Mathematics.int2(0, 1) },
            };
            map.Normalize();
            var def = new MatchDefinition { Seed = 1, Map = map };
            def.ConfigHash = def.ComputeConfigHash();
            return def;
        }
    }
}
