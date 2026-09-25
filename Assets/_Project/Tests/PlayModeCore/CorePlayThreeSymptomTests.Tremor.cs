using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wassup.BattleCore;
using Wassup.BattleCoreUnity;
using Wassup.BattleCoreUnity.Hud;
using Wassup.BattleCoreUnity.Input;
using Wassup.BattleCoreUnity.View;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    public sealed partial class CorePlayThreeSymptomTests
    {
        [UnityTest]
        public IEnumerator 진동갑주가_발동하면_숙주_자리에서_착탄_연출이_터진다()
        {
            // 규칙(체력 30% 경계)은 헤드리스 `PlayThreeSymptomTests` 가 본다. 여기는 **발동이 화면에 무엇을 남기나**.
            BattleDriver driver = null;
            yield return BootWithDeck(d => driver = d, TremorCard);
            driver.Apply(Command.FinishPlacement());
            var pool = Object.FindAnyObjectByType<CoreProjectileViewPool>();
            Assert.IsNotNull(pool);
            int row = CardRow(driver, "tremor_plate");
            var cell = new int2(driver.GridSize.x / 2, driver.GridSize.y / 2);
            Assert.IsTrue(driver.Apply(Command.DebugSpawnDefender(0, cell)).Accepted, "숙주");
            SimEntityId host = SimEntityId.None;
            var units = driver.Match.World.Units;
            for (int i = units.Count - 1; i >= 0 && host.IsNone; i--) if (units[i].Kind == UnitKind.Defender) host = units[i].Id;
            Assert.IsTrue(driver.Apply(Command.DebugAttachCard(row, host)).Accepted, "부착");
            var binding = driver.Find(host).Bindings[driver.Find(host).Bindings.Count - 1];

            var hits = new List<CoreEvent>();
            System.Action<CoreEvent> probe = e => { if (e.Kind == CoreEventKind.ProjectileHit) hits.Add(e); };
            driver.Subscribe(ViewOrder.Trace, probe);
            try
            {
                Assert.IsTrue(driver.Apply(Command.DebugFireBinding(host, binding.InstanceId)).Accepted, "강제 발동");
                for (int i = 0; i < 30 && hits.Count == 0; i++) yield return null;
                Assert.Greater(hits.Count, 0, "진동갑주 착탄 사건이 안 났다");
                yield return null;
                var hitPrefab = driver.ViewAssets.Projectile(hits[0].DefIndex).hitPrefab;
                Assert.IsNotNull(hitPrefab, "착탄 연출 프리팹이 저작돼 있지 않다");
                bool burst = false;
                foreach (Transform c in pool.transform)
                    if (c.gameObject.activeInHierarchy && c.name.StartsWith(hitPrefab.name)) burst = true;
                Assert.IsTrue(burst, "진동갑주 착탄 연출이 안 터졌다");
            }
            finally
            {
                driver.Unsubscribe(probe);
            }
        }
    }
}
