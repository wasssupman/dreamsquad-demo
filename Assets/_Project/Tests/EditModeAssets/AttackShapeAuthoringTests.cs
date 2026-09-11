using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.Battle.Combat;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // directional-attack-shape unit 1 — 드리프트 그물. **이 spec 의 unit 4 가 저작하기 전까지** 모든 유닛
    // SO 는 Omni 로 구워져야 한다(라이브 무변의 증언). unit 4 가 저작을 넣으면 이 단언은 «허용 목록»으로
    // 바뀐다 — 그때 이 파일의 목적이 「무변」에서 「의도된 저작만」으로 옮겨간다.
    public class AttackShapeAuthoringTests
    {
        private const string DefenderRoot = "Assets/_Project/Data/Defenders";
        private const string EnemyRoot = "Assets/_Project/Data";

        [Test]
        public void AllDefenders_BakeToOmni_UntilUnit4Authors()
        {
            var offenders = new StringBuilder();
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData", new[] { DefenderRoot }))
            {
                var u = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u == null) continue;
                n++;
                var baked = AttackShapeBake.From(u.attackShape, out bool ok);
                if (!ok || !baked.IsOmni) offenders.Append(u.id).Append(' ');
            }
            Assert.Greater(n, 0);
            Assert.IsTrue(offenders.Length == 0, $"도형이 저작된(또는 정의역 밖) 방어유닛: {offenders}");
        }

        // 계약 11 의 기계적 그물 — unit 4 가 위 「전부 Omni」를 허용 목록으로 바꿔도 이 단언은 남는다.
        // 추격(detection) 적은 추격판 사격 칸이 셀 디스크라 도형을 모른다 → 띠 밖 칸에서 영원히 못 쏘는 교착.
        [Test]
        public void HuntingEnemies_NeverAuthorAShape_Contract11()
        {
            var offenders = new StringBuilder();
            foreach (var guid in AssetDatabase.FindAssets("t:AttackUnitData", new[] { EnemyRoot }))
            {
                var u = AssetDatabase.LoadAssetAtPath<AttackUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u == null || !u.UsesDetection) continue;
                if (!AttackShapeBake.From(u.attackShape, out _).IsOmni) offenders.Append(u.name).Append(' ');
            }
            Assert.IsTrue(offenders.Length == 0, $"추격 적에 도형을 저작했다(계약 11): {offenders}");
        }

        [Test]
        public void AllEnemies_BakeToOmni_UntilUnit4Authors()
        {
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
