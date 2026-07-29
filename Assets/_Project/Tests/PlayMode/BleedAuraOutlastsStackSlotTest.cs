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

namespace Wassup.Tests.PlayMode
{
    // bleed-fighter-defender 후속 — 출혈 오라가 **파생 DoT 가 도는 내내** 유지되는지.
    //
    // 회귀 대상: 오라 게이트가 "슬롯 보유 AND DoT 진행" 이던 시절, Consume 임계가 스택을
    // 0으로 되돌리고 슬롯도 perAppDuration(2s) 이 지나면 만료되는데 파생 DoT 는 4.85s 를
    // 살아서, 도트 후반부에 오라가 조용히 꺼졌다. 이제 bridge 가 종류를 래치하고 점등은
    // DoT 로만 판단한다.
    public class BleedAuraOutlastsStackSlotTest
    {
        [UnityTest]
        public IEnumerator AuraStaysLitAfterStackSlotExpires()
        {
            yield return SceneManager.LoadSceneAsync(SceneNames.Battle, LoadSceneMode.Single);
            for (int i = 0; i < 6; i++) yield return null;

            var em = World.DefaultGameObjectInjectionWorld.EntityManager;
            var bridge = Object.FindObjectOfType<BattleBridge>();
            var gm = Object.FindObjectOfType<GameManager>();
            var unit = Resources.FindObjectsOfTypeAll<DefenderCatalog>()[0].ById("slasher");
            Assert.NotNull(unit, "난도질꾼이 카탈로그에 없다");

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
                .GetField("_defenderByTile", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(bridge);
            Entity defender = Entity.Null;
            foreach (System.Collections.DictionaryEntry de in byTile)
            { defender = (Entity)de.Value.GetType().GetField("Item1").GetValue(de.Value); break; }
            Assert.AreNotEqual(Entity.Null, defender);

            // 오래 버티는 더미 적. AttackUnitTag 가 없으면 공격 쿼리에 안 잡혀 vacuous 해진다.
            var pos = em.GetComponentData<LocalTransform>(defender).Position;
            var victim = em.CreateEntity();
            em.AddComponentData(victim, LocalTransform.FromPosition(pos + new float3(0.05f, 0f, 0f)));
            em.AddComponentData(victim, new Health { value = 1000000f, max = 1000000f });
            em.AddComponentData(victim, new FactionTag { value = Faction.Enemy });
            em.AddBuffer<IncomingDamage>(victim);
            em.AddBuffer<CcEffect>(victim);
            em.AddComponent<AttackUnitTag>(victim);

            // 관측 대상 = bridge 의 종류 래치. 실제로 깨졌던 건 "이 DoT 를 만든 스택이
            // 무엇인가"를 아는 기간이었다. 스폰된 뷰 자체로는 관측할 수 없다 — 합성 더미
            // 엔티티에는 유닛 뷰가 없어 ResolveUnitViewTransform 이 null 이고, 앵커 해석은
            // 다른 StatusFx 종류들과 공유하는 이미 검증된 경로다.
            var latchField = typeof(BattleBridge)
                .GetField("_stackAuraLatch", BindingFlags.NonPublic | BindingFlags.Instance);

            bool AuraLit()
            {
                var latch = (IDictionary<Entity, StatusFxKind>)latchField.GetValue(bridge);
                return latch.TryGetValue(victim, out var k) && k == StatusFxKind.Bleed;
            }
            bool DotRunning()
            {
                var ccs = em.GetBuffer<CcEffect>(victim, isReadOnly: true);
                for (int i = 0; i < ccs.Length; i++)
                    if (ccs[i].kind == CcKind.DoT && ccs[i].remainingTime > 0f) return true;
                return false;
            }
            bool SlotLive()
            {
                if (!em.HasBuffer<StackModifierSlot>(victim)) return false;
                var slots = em.GetBuffer<StackModifierSlot>(victim, isReadOnly: true);
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i].kind == StackKind.Bleed && slots[i].header.remaining > 0f) return true;
                return false;
            }

            // 1) 누적 → 임계 발동까지 기다린다.
            float t = 0f;
            while (t < 6f && !DotRunning()) { t += Time.deltaTime; yield return null; }
            Assert.IsTrue(DotRunning(), "출혈 임계가 발동하지 않았다");
            yield return null; // reconcile 한 프레임
            Assert.IsTrue(AuraLit(), "발동 직후 오라 종류가 식별되지 않았다");

            // 2) 공격자를 제거해 스택 재적용을 끊는다 → 슬롯은 만료되고 DoT 만 남는다.
            em.DestroyEntity(defender);

            // 3) 슬롯이 만료된 뒤에도 DoT 가 도는 동안 오라가 유지되는지 — 회귀의 핵심.
            bool sawSlotGoneWhileDotRuns = false;
            float elapsed = 0f;
            while (elapsed < 6f && DotRunning())
            {
                if (!SlotLive())
                {
                    sawSlotGoneWhileDotRuns = true;
                    Assert.IsTrue(AuraLit(),
                        $"슬롯이 사라진 뒤 DoT 가 도는 중인데 오라 종류 식별이 끊겼다 (경과 {elapsed:F2}s)");
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
            Assert.IsTrue(sawSlotGoneWhileDotRuns,
                "슬롯이 DoT 보다 오래 살아서 회귀 구간을 못 밟았다 — perAppDuration/지속 관계를 확인할 것");

            // 4) DoT 가 끝나면 꺼진다(래치가 영구 점등이 되지 않는다).
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(AuraLit(), "DoT 종료 후에도 래치가 남아 있다(영구 점등)");

            em.DestroyEntity(victim);
        }
    }
}
