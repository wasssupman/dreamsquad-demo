using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Wassup.Core;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Battle.Units;
using Wassup.Battle.Effects;
using Wassup.Presentation;

namespace Wassup.Tests.PlayMode
{
    // dot-effect-extraction unit 1 — 지속 피해 오라가 **도트 자신**을 소스로 켜지는지.
    //
    // 회귀 대상 2건:
    //  (a) 종류 래치 시절, 한 번 켠 비트를 내리는 경로가 "도트가 하나도 안 돈다" 뿐이었다.
    //      출혈은 계속 맞으면 remainingTime=max 로 안 끊기므로, 얼음이 한 번만 스쳐도
    //      얼음 오라가 매치 끝까지 남았다.
    //  (b) 점등 게이트가 "아무 도트나 진행 중"이라, 출혈만 아픈데 얼음 오라가 같이 떴다.
    //
    // 관측은 StatusFxSpawner 의 실제 활성 집합으로 한다(래치 같은 중간 상태가 아니라).
    public class DotAuraFromFlavorTest
    {
        private EntityManager _em;
        private BattleBridge _bridge;
        private StatusFxSpawner _spawner;
        private FieldInfo _activeField;
        private Entity _victim;

        private bool AuraLit(StatusFxKind kind)
        {
            var active = (IDictionary<(Entity, StatusFxKind), StatusFxView>)_activeField.GetValue(_spawner);
            return active.ContainsKey((_victim, kind));
        }

        [UnityTest]
        public IEnumerator Aura_FollowsDotFlavor_NotStackSlots()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            _em = World.DefaultGameObjectInjectionWorld.EntityManager;
            _bridge = Object.FindObjectOfType<BattleBridge>();
            _spawner = Object.FindObjectOfType<StatusFxSpawner>();
            Assert.NotNull(_bridge); Assert.NotNull(_spawner);
            _activeField = typeof(StatusFxSpawner).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);

            var gm = Object.FindObjectOfType<GameManager>();
            var unit = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0].ById("slasher");
            _bridge.SetDefenderPool(new[] { unit });
            _bridge.BeginPlacement();
            gm.CostRuntime.ResetToStart();
            gm.CostRuntime.AddCost(100000);

            var cell = new Vector2Int(int.MinValue, int.MinValue);
            for (int x = -24; x < 48 && cell.x == int.MinValue; x++)
                for (int y = -24; y < 48; y++)
                    if (_bridge.CanPlaceDefenderAt(x, y, unit, out _)) { cell = new Vector2Int(x, y); break; }
            Assert.AreNotEqual(int.MinValue, cell.x, "배치 가능 타일을 못 찾았다");
            _bridge.PlaceDefenderAs(cell.x, cell.y, unit);
            _bridge.StartBattle();

            // 배치된 방어 유닛을 피해자로 쓴다 — **뷰가 붙어 있어야** 오라가 실제로 스폰된다.
            // 합성 더미는 ResolveUnitViewTransform 이 null 이라 스포너까지 가지 못한다.
            var byTile = (System.Collections.IDictionary)typeof(BattleBridge)
                .GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_bridge);
            foreach (System.Collections.DictionaryEntry de in byTile)
            { _victim = (Entity)de.Value.GetType().GetField("Item1").GetValue(de.Value); break; }
            Assert.AreNotEqual(Entity.Null, _victim);
            Assert.IsTrue(_em.HasBuffer<DotEffect>(_victim), "유닛 생성 시 DotEffect 버퍼가 붙어야 한다");

            var dotQ = _em.CreateEntityQuery(ComponentType.ReadOnly<DotApplyEventsSingleton>())
                          .GetSingleton<DotApplyEventsSingleton>().queue;

            void Enqueue(DotFlavor flavor, float duration) => dotQ.Enqueue(new DotApplyEvent
            {
                target = _victim,
                effect = new DotEffect
                { flavor = flavor, scalar = 1f, tickInterval = 0.5f, remainingTime = duration },
            });

            // 1) 출혈 도트만 → 출혈 오라만 켜진다.
            Enqueue(DotFlavor.Bleed, 6f);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(HasDot(DotFlavor.Bleed), "도트 부여 경로가 끊겼다(오라 이전 단계 문제)");
            Assert.IsTrue(AuraLit(StatusFxKind.Bleed), "출혈 도트가 도는데 오라가 안 켜졌다");
            Assert.IsFalse(AuraLit(StatusFxKind.IceStack), "무관한 오라가 같이 켜졌다");

            // 2) 냉기 도트를 얹으면 둘 다 켜진다 — 슬롯이 갈려 있으므로 별도 장치 없이 성립.
            Enqueue(DotFlavor.Ice, 0.6f);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsTrue(AuraLit(StatusFxKind.IceStack), "냉기 도트가 도는데 오라가 안 켜졌다");
            Assert.IsTrue(AuraLit(StatusFxKind.Bleed), "냉기가 얹히자 출혈 오라가 사라졌다");

            // 3) 회귀의 핵심 — 냉기만 만료되면 냉기 오라만 꺼진다.
            //    출혈은 계속 돌고 있으므로 옛 래치 방식에서는 냉기 비트가 영영 안 내려갔다.
            float t = 0f;
            while (t < 3f && HasDot(DotFlavor.Ice)) { t += Time.deltaTime; yield return null; }
            Assert.IsFalse(HasDot(DotFlavor.Ice), "냉기 도트가 만료되지 않았다");
            for (int i = 0; i < 3; i++) yield return null;

            Assert.IsTrue(HasDot(DotFlavor.Bleed), "출혈이 아직 돌고 있어야 회귀 구간이 성립한다");
            Assert.IsFalse(AuraLit(StatusFxKind.IceStack),
                "냉기 도트가 끝났는데 냉기 오라가 남아 있다 — 종류가 도트가 아닌 곳에서 오고 있다");
            Assert.IsTrue(AuraLit(StatusFxKind.Bleed), "출혈 오라는 유지돼야 한다");
        }

        private bool HasDot(DotFlavor flavor)
        {
            if (!_em.HasBuffer<DotEffect>(_victim)) return false;
            var b = _em.GetBuffer<DotEffect>(_victim, isReadOnly: true);
            for (int i = 0; i < b.Length; i++)
                if (b[i].flavor == flavor && b[i].remainingTime > 0f) return true;
            return false;
        }
    }
}
