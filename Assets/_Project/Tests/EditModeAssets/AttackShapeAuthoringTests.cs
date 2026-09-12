using System.Text;
using NUnit.Framework;
using UnityEditor;
using Wassup.Battle.Combat;
using Wassup.Data;

namespace Wassup.Tests.EditModeAssets
{
    // directional-attack-shape — 저작 허용 목록 그물. unit 1 시절엔 「전부 Omni」(라이브 무변의 증언)였고,
    // unit 4 가 파이터에 저작을 넣으며 「의도된 저작만」으로 목적이 옮겨왔다.
    public class AttackShapeAuthoringTests
    {
        private const string DefenderRoot = "Assets/_Project/Data/Defenders";
        private const string EnemyRoot = "Assets/_Project/Data";

        // unit 4 (사용자 결정 2026-09-12 「모든 파이터 클래스 90°」) — 허용 목록. 파이터는 보는 쪽 90° 부채꼴,
        // 나머지는 Omni. ⚠ 순찰병(`patrol_soldier`)은 파이터지만 **순찰 이동 유닛**이라 계약 11 로 Omni 를 강제한다 —
        // `PatrolAreaMath.CloseInDir` 가 「못 때리는 적」쪽으로 다가가므로 도형을 주면 머리 위 열의 적이 영원한
        // 접근 대상이 된다. 이 목록이 곧 「어느 유닛이 도형을 갖나」의 정본이다 — 바꾸려면 spec unit 4 를 고친다.
        private static readonly string[] PatrolUnits = { "patrol_soldier" };

        [Test]
        public void Defenders_FightersAreSector90_OthersOmni_PatrolAlwaysOmni()
        {
            var offenders = new StringBuilder();
            int n = 0, fighters = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:DefenderUnitData", new[] { DefenderRoot }))
            {
                var u = AssetDatabase.LoadAssetAtPath<DefenderUnitData>(AssetDatabase.GUIDToAssetPath(guid));
                if (u == null) continue;
                n++;
                var baked = AttackShapeBake.From(u.attackShape, out bool ok);
                if (!ok) { offenders.Append(u.id).Append("(정의역 밖) "); continue; }
                bool patrol = System.Array.IndexOf(PatrolUnits, u.id) >= 0;
                bool expectSector = u.role == DefenderClass.Fighter && !patrol;
                if (expectSector)
                {
                    fighters++;
                    if (baked.kind != AttackShapeBaked.SectorKind || u.attackShape.angleDeg != 90f)
                        offenders.Append(u.id).Append("(파이터인데 90° 부채꼴 아님) ");
                }
                else if (!baked.IsOmni)
                    offenders.Append(u.id).Append(patrol ? "(순찰 유닛에 도형 — 계약 11) " : "(비파이터에 도형) ");
            }
            Assert.Greater(n, 0);
            Assert.Greater(fighters, 0, "파이터를 찾지 못했다 — role 필드 규약이 바뀌었나?");
            Assert.IsTrue(offenders.Length == 0, $"도형 저작이 허용 목록과 다르다: {offenders}");
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
