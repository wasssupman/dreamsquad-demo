using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // directional-attack-shape — 저작 허용 목록 그물. unit 1 시절엔 「전부 Omni」(라이브 무변의 증언)였고,
    // unit 4 rev 3 가 다중 타격 파이터에 저작을 넣으며 「의도된 저작만」으로 목적이 옮겨왔다.
    public class AttackShapeAuthoringTests
    {
        private const string DefenderRoot = "Assets/_Project/Data/Defenders";
        private const string EnemyRoot = "Assets/_Project/Data";

        // unit 4 (사용자 결정 2026-09-12 「안 1 · 60°」) — 다중 타격(attackTargetCount > 1) 파이터는 60° 부채꼴,
        // 나머지는 Omni. 단일 타겟 파이터(말많은놈·순찰병)는 도형이 효과 0 이라 저작하지 않는다.
        // unit 4 rev 3b (사용자 결정 2026-09-12 「이쑤시개는 rect 로 확정」) — 이쑤시개(`slasher`)만 **띠**(Rect 폭 1):
        // 찌르는 창이라 주 대상 뒤 일직선(사거리 2 + 몸)의 최대 3체. 유일한 Band 저작이라 id 로 못박는다.
        // 이 목록이 곧 「어느 유닛이 도형을 갖나」의 정본이다 — 바꾸려면 spec unit 4 를 고친다.
        private const string BandDefenderId = "slasher";
        private const float BandDefenderWidth = 1f;

        [Test]
        public void Defenders_MultiHitFightersAreSector60_SlasherIsBand_OthersOmni()
        {
            var offenders = new StringBuilder();
            int n = 0, shaped = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData", new[] { DefenderRoot }))
            {
                var u = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u == null) continue;
                n++;
                var baked = AttackShapeBake.From(u.attackShape, out bool ok);
                if (!ok) { offenders.Append(u.id).Append("(정의역 밖) "); continue; }
                if (u.id == BandDefenderId)
                {
                    shaped++;
                    if (baked.kind != AttackShapeBaked.BandKind || u.attackShape.width != BandDefenderWidth)
                        offenders.Append(u.id).Append("(띠 폭 1 이어야 한다) ");
                    if (u.attackTargetCount <= 1)
                        offenders.Append(u.id).Append("(단일 타겟에 띠 — 효과 0) ");
                    continue;
                }
                bool expectSector = u.role == DefenderClass.Fighter && u.attackTargetCount > 1;
                if (expectSector)
                {
                    shaped++;
                    if (baked.kind != AttackShapeBaked.SectorKind || u.attackShape.angleDeg != 60f)
                        offenders.Append(u.id).Append("(다중 타격 파이터인데 60° 부채꼴 아님) ");
                }
                else if (!baked.IsOmni)
                    offenders.Append(u.id).Append(u.attackTargetCount <= 1 ? "(단일 타겟에 도형 — 효과 0) " : "(비파이터에 도형) ");
            }
            Assert.Greater(n, 0);
            Assert.Greater(shaped, 0, "다중 타격 파이터를 찾지 못했다 — role/attackTargetCount 규약이 바뀌었나?");
            Assert.IsTrue(offenders.Length == 0, $"도형 저작이 허용 목록과 다르다: {offenders}");
        }

        [Test]
        public void AllEnemies_BakeToOmni_NoEnemyAuthoringYet()
        {
            // 사용자 결정 2026-09-12: 적 저작 없음(회오리는 전방위가 정체성). 적에 도형을 넣게 되면 이 단언을 허용 목록으로.
            var offenders = new StringBuilder();
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:AttackUnitData", new[] { EnemyRoot }))
            {
                var u = AssetDatabase.LoadAssetAtPath<AttackUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u == null) continue;
                n++;
                var baked = AttackShapeBake.From(u.attackShape, out bool ok);
                if (!ok || !baked.IsOmni) offenders.Append(u.name).Append(' ');
            }
            Assert.Greater(n, 0);
            Assert.IsTrue(offenders.Length == 0, $"도형이 저작된(또는 정의역 밖) 적: {offenders}");
        }
    }
}
