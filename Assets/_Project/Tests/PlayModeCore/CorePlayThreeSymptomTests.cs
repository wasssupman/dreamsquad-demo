using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCoreUnity;
using Somnia.Battle.BattleCoreUnity.Hud;
using Somnia.Battle.BattleCoreUnity.Input;
using Somnia.Battle.BattleCoreUnity.View;
using Somnia.Battle.Data;

namespace Somnia.Battle.Tests.PlayMode.Core
{
    // 사용자 플레이 3차의 문장 — 「운석 비주얼이 보이지 않음」 · 「퇴근 작동 하지 않음」.
    // 단언은 **화면이 보여 주는 것**이다(버그 절차 2): 운석이 하늘에서 떨어지는 게 보이나 · 버튼을 누른 손가락이 버튼에 닿나.
    public sealed partial class CorePlayThreeSymptomTests
    {
        private const string MeteorCard = "Assets/_Project/Data/Dreamcatcher/Active_Meteor.asset";
        private const string TremorCard = "Assets/_Project/Data/Dreamcatcher/Card_TremorPlate.asset";

        private static IEnumerator BootWithDeck(System.Action<BattleDriver> found, params string[] cardPaths)
        {
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
#if UNITY_EDITOR
            var cards = new DreamcatcherCard[cardPaths.Length];
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<DreamcatcherCard>(cardPaths[i]);
                Assert.IsNotNull(cards[i], "카드 에셋이 없다: " + cardPaths[i]);
            }
            CoreSceneFixture.OverrideDeck(driver, cards);
#endif
            driver.Begin();
            found(driver);
        }

        private static int CardRow(BattleDriver driver, string id)
        {
            var cards = driver.Definition.Cards;
            for (int i = 0; i < cards.Length; i++) if (cards[i].Id == id) return i;
            Assert.Fail("덱에 카드가 없다: " + id);
            return -1;
        }

        private static GameObject ViewOf(CoreProjectileViewPool pool, SimEntityId id)
        {
            var dict = (System.Collections.IDictionary)typeof(CoreProjectileViewPool)
                .GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pool);
            if (!dict.Contains(id)) return null;
            var state = dict[id];
            return (GameObject)state.GetType().GetField("view").GetValue(state);
        }

        [UnityTest]
        public IEnumerator 운석을_시전하면_하늘에서_떨어지는_운석이_화면에_보이고_착탄_연출이_터진다()
        {
            BattleDriver driver = null;
            yield return BootWithDeck(d => driver = d, MeteorCard);
            driver.Apply(Command.FinishPlacement());
            var pool = Object.FindAnyObjectByType<CoreProjectileViewPool>();
            Assert.IsNotNull(pool, "탄 뷰 풀이 씬에 없다");
            var cam = Camera.main;
            Assert.IsNotNull(cam);

            int row = CardRow(driver, "active_meteor");
            var cell = new int2(driver.GridSize.x / 2, driver.GridSize.y / 2);
            var spawned = new List<CoreEvent>();
            var hits = new List<CoreEvent>();
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.ProjectileSpawned) spawned.Add(e);
                if (e.Kind == CoreEventKind.ProjectileHit) hits.Add(e);
            };
            driver.Subscribe(ViewOrder.Trace, probe);
            try
            {
                Assert.IsTrue(driver.Apply(Command.DebugCastCard(row, cell)).Accepted, "운석 시전");

                // 낙하는 비행 후반(fallPortion)에만 보인다 — 그 앞은 숨는다. 그래서 **숨었다가 다시 켜진 뒤**의
                // 프레임만 잰다(스폰 프레임은 Spawn 이 낙하 시작 높이에 세워 두어 늘 높다 — 그건 낙하가 아니다).
                // 「보인다」 = 화면 안에서 **공중에 떠 있는**(낙하 시작 높이의 1/4 이상) 프레임이 있다.
                float revealLift = -1f, dropHeight = 0f;
                bool wasHidden = false;
                int fallingInFrame = 0;
                SimEntityId meteor = SimEntityId.None;
                for (float t = 0f; t < 6f && hits.Count == 0; t += Time.unscaledDeltaTime)
                {
                    yield return null;
                    if (meteor.IsNone && spawned.Count > 0) meteor = spawned[0].A;
                    if (meteor.IsNone) continue;
                    var p = driver.Match.World.FindProjectile(meteor);
                    var view = ViewOf(pool, meteor);
                    if (p == null || view == null) continue;
                    dropHeight = driver.ViewAssets.Projectile(p.DefIndex).dropHeight;
                    if (!view.activeInHierarchy) { wasHidden = true; continue; }
                    if (!wasHidden) continue;
                    Vector3 ground = Somnia.Battle.Core.BoardSpace.ToView(p.Position);
                    float lift = (view.transform.position - ground).magnitude;
                    if (revealLift < 0f) revealLift = lift;
                    var vp = cam.WorldToViewportPoint(view.transform.position);
                    bool inFrame = vp.z > 0f && vp.x >= 0f && vp.x <= 1f && vp.y >= 0f && vp.y <= 1f;
                    if (inFrame && lift > dropHeight * 0.25f) fallingInFrame++;
                }
                Assert.IsFalse(meteor.IsNone, "코어가 운석 탄을 안 냈다");
                Assert.IsTrue(wasHidden && revealLift >= 0f, "운석 뷰가 낙하 구간에서 한 번도 안 켜졌다");
                Debug.Log($"[CorePlayThreeSymptomTests] meteor revealLift={revealLift:F2} dropHeight={dropHeight:F2} fallingInFrame={fallingInFrame}");
                Assert.Greater(revealLift, dropHeight * 0.5f,
                    $"운석이 하늘에서 떨어지지 않는다 — 낙하가 보이기 시작한 순간 높이 {revealLift:F2} (낙하 시작 저작값 {dropHeight:F2})");
                Assert.Greater(fallingInFrame, 0, "떨어지는 운석이 카메라 화면 안에 한 프레임도 안 잡혔다");
                Assert.Greater(hits.Count, 0, "착탄 사건이 안 났다");
                yield return null;
                var hitPrefab = driver.ViewAssets.Projectile(hits[0].DefIndex).hitPrefab;
                bool burst = false;
                foreach (Transform c in pool.transform)
                    if (c.gameObject.activeInHierarchy && c.name.StartsWith(hitPrefab.name)) burst = true;
                Assert.IsTrue(burst, "착탄 연출이 안 터졌다");
            }
            finally
            {
                driver.Unsubscribe(probe);
            }
        }
    }
}
