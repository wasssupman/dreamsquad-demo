using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Effects;

namespace Wassup.Tests.PlayMode
{
    // dot-effect-extraction — 서로 다른 파이프라인이 만든 지속 피해가 **실제 시스템을 통과하면서**
    // 공존하는지. `DotEffectMergeTests` 는 병합 함수만 직접 부르지만, 이 테스트는 전용 큐 →
    // `DotApplySystem` 드레인 → 틱 → 감쇠의 실제 사슬을 탄다.
    //
    // 회귀 대상(CRITICAL). 한 버퍼를 쓰던 시절엔 피해자당 슬롯이 하나라 나중에 온 도트가
    // scalar·tickInterval 을 덮고 remainingTime 만 max 로 남았다. 그래서 출혈(5 / 0.5s / 장기)
    // 중인 적이 화염 장판(10 / 0.25s / 0.2s)을 밟으면
    //   - 요율이 장판 것(40 DPS)으로 바뀌고
    //   - 장판을 나가도 출혈의 긴 지속만큼 그 요율로 계속 탔다
    // 총 피해가 의도의 4배 가까이 나던 결함이다. 여기서 고정하는 것은 **덮어쓰기가 없다**는
    // 사실 그 자체 — 두 슬롯이 각자의 값과 각자의 수명을 끝까지 유지한다.
    public class DotCoexistenceTest
    {
        private EntityManager _em;
        private Entity _victim;

        // 실제 저작값의 축소판. 비율(출혈=저요율·장기 / 장판=고요율·순간)만 보존하고
        // 테스트가 1.5초 안에 끝나도록 지속만 줄였다.
        private const float BleedScalar = 5f;
        private const float BleedInterval = 0.5f;
        private const float BleedDuration = 3f;
        private const float FireScalar = 10f;
        private const float FireInterval = 0.25f;
        private const float FireDuration = 0.2f;

        [TearDown]
        public void TearDown() => LogAssert.ignoreFailingMessages = false;

        [UnityTest]
        public IEnumerator SeparatePipelines_KeepOwnRateAndLifetime()
        {
            // 배틀 씬 PlayMode 테스트의 프로젝트 관례. batchmode 에서는 Entities Graphics 의
            // asset GC(`EntitiesGraphicsSystem.cs:717`)와 PrimeTween 이 이 테스트와 무관한
            // 에러 로그를 뱉는데, 기본 설정이면 그것만으로 실패 처리된다.
            LogAssert.ignoreFailingMessages = true;

            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            Assert.NotNull(bridge);

            var gm = Object.FindObjectOfType<GameManager>();
            var unit = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0].ById("slasher");
            bridge.SetDefenderPool(new[] { unit });
            bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            var cell = new Vector2Int(int.MinValue, int.MinValue);
            for (int x = -24; x < 48 && cell.x == int.MinValue; x++)
                for (int y = -24; y < 48; y++)
                    if (bridge.CanPlaceDefenderAt(x, y, unit, out _)) { cell = new Vector2Int(x, y); break; }
            Assert.AreNotEqual(int.MinValue, cell.x, "배치 가능 타일을 못 찾았다");
            bridge.PlaceDefenderAs(cell.x, cell.y, unit);
            bridge.StartBattle();

            var byTile = (System.Collections.IDictionary)typeof(BattleBridge)
                .GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(bridge);
            foreach (System.Collections.DictionaryEntry de in byTile)
            { _victim = (Entity)de.Value.GetType().GetField("Item1").GetValue(de.Value); break; }
            Assert.AreNotEqual(Entity.Null, _victim);

            var dotQ = _em.CreateEntityQuery(ComponentType.ReadOnly<DotApplyEventsSingleton>())
                          .GetSingleton<DotApplyEventsSingleton>().queue;

            void Enqueue(DotOrigin origin, DotElement element, float scalar, float interval, float duration)
                => dotQ.Enqueue(new DotApplyEvent
                {
                    target = _victim,
                    effect = new DotEffect
                    {
                        origin = origin, element = element,
                        scalar = scalar, tickInterval = interval, remainingTime = duration,
                    },
                });

            // 1) 스택 파생 출혈 위에 해저드 장판 화염을 얹는다 — 실제로 도달하는 조합
            //    (난도질꾼 + FireCaster 는 둘 다 카탈로그에 있다).
            Enqueue(DotOrigin.Stack, DotElement.Bleed, BleedScalar, BleedInterval, BleedDuration);
            Enqueue(DotOrigin.Zone, DotElement.Fire, FireScalar, FireInterval, FireDuration);
            for (int i = 0; i < 2; i++) yield return null;

            Assert.IsTrue(TryFind(DotOrigin.Stack, DotElement.Bleed, out var bleed),
                "출혈 슬롯이 없다 — 부여 seam 이 끊겼다");
            Assert.IsTrue(TryFind(DotOrigin.Zone, DotElement.Fire, out var fire),
                "화염 슬롯이 없다 — 출혈이 화염을 삼켰다(한 슬롯 시절의 증상)");

            // 각자 자기 요율. 이 두 줄이 과피해가 사라지는 이유 그 자체다.
            Assert.AreEqual(BleedScalar, bleed.scalar, 1e-3f, "출혈 요율이 장판 값으로 덮였다");
            Assert.AreEqual(BleedInterval, bleed.tickInterval, 1e-3f, "출혈 주기가 장판 값으로 덮였다");
            Assert.AreEqual(FireScalar, fire.scalar, 1e-3f);
            Assert.AreEqual(FireInterval, fire.tickInterval, 1e-3f);

            // 짧은 장판 지속이 긴 출혈 지속으로 늘어나지 않는다(max 병합의 부작용이던 부분).
            Assert.Less(fire.remainingTime, FireDuration + 0.05f,
                "장판 도트가 출혈의 남은 지속을 물려받았다");

            // 2) 장판을 나간 상황 — 갱신이 끊기면 화염만 자기 지속으로 죽는다.
            float t = 0f;
            while (t < 2f && TryFind(DotOrigin.Zone, DotElement.Fire, out _))
            { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(TryFind(DotOrigin.Zone, DotElement.Fire, out _),
                "화염 슬롯이 만료되지 않았다");

            // 3) 핵심 — 화염이 사라진 뒤에도 출혈은 **자기 요율 그대로** 계속 돈다.
            //    옛 구조에서는 여기서 scalar 10 · 주기 0.25s 로 남아 장판 밖에서 장판 요율로 탔다.
            Assert.IsTrue(TryFind(DotOrigin.Stack, DotElement.Bleed, out bleed),
                "화염이 만료되면서 출혈까지 같이 사라졌다");
            Assert.AreEqual(BleedScalar, bleed.scalar, 1e-3f,
                "장판이 지나간 뒤 출혈이 장판 요율로 타고 있다 — 과피해 회귀");
            Assert.AreEqual(BleedInterval, bleed.tickInterval, 1e-3f,
                "장판이 지나간 뒤 출혈 주기가 장판 주기로 남았다 — 과피해 회귀");
            Assert.Greater(bleed.remainingTime, 0f);
            Assert.Less(bleed.remainingTime, BleedDuration,
                "출혈의 남은 지속이 줄지 않았다 — 감쇠가 안 돌고 있다");
        }

        private bool TryFind(DotOrigin origin, DotElement element, out DotEffect found)
        {
            found = default;
            if (!_em.Exists(_victim) || !_em.HasBuffer<DotEffect>(_victim)) return false;
            var b = _em.GetBuffer<DotEffect>(_victim, isReadOnly: true);
            for (int i = 0; i < b.Length; i++)
                if (b[i].origin == origin && b[i].element == element) { found = b[i]; return true; }
            return false;
        }
    }
}
