using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // 근접 유닛 공중 타격 버그(2026-09-03 사용자 보고) — 층 축 개통 때 전 방어유닛이
    // attackTargetLayers = Path|Air 로 깔려, 손이 닿을 리 없는 근접(사거리 1)까지 비행 적을
    // 때렸다. 규칙: **근접 공격자는 지상 전용** — Air 비트 금지(지원형 targetAllies 는 층
    // 마스크가 0 으로 구워져 제외). 신규 근접 유닛도 이 불변식을 지나야 한다.
    public class DefenderMeleeAirTargetTests
    {
        private const string Root = "Assets/_Project/Data/Defenders";

        [Test]
        public void MeleeAttackers_DoNotTargetAir()
        {
            var offenders = new StringBuilder();
            int melee = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData", new[] { Root }))
            {
                var u = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u == null || u.targetAllies || u.attackRange > 1) continue;
                melee++;
                if ((u.EffectiveAttackTargetLayers & PlacementLayer.Air) != 0)
                    offenders.Append(u.id).Append(' ');
            }
            Assert.Greater(melee, 0, "근접(사거리 ≤1) 방어유닛을 찾지 못했다 — 경로/필드 규약이 바뀌었나?");
            Assert.IsTrue(offenders.Length == 0,
                $"근접인데 공중을 때린다(attackTargetLayers 에 Air): {offenders}");
        }
    }
}
