using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // unit-stat-projection Unit 3 — freezes the roster invariants the atk/heal
    // projection depends on: a kind with 0 or 2+ entries cannot be projected.
    // A failure here is a RENEGOTIATION prompt, not a hard prohibition — see the
    // assert messages and docs/spec/unit-stat-projection/README.md.
    public class UnitRosterInvariantTests
    {
        private const string DefenderFolder = "Assets/_Project/Data/Defenders";
        private const string EnemyFolder = "Assets/_Project/Data/Enemies";
        private const string CatalogPath = "Assets/_Project/Data/DefenderCatalog.asset";

        private const string DamageHint =
            "투영 규칙은 Damage 항목이 정확히 1개인 유닛만 지원한다. 2개+가 필요하면 " +
            "투영 규칙(spec unit 0)을 갱신하거나 이 유닛을 시트 비관리(atk 미사용)로 표기하라.";
        private const string HealHint =
            "투영 규칙은 Heal 항목이 정확히 1개인 유닛만 지원한다. 위와 동일하게 재협상하라.";

        [Test]
        public void AllDefenders_SatisfyProjectionInvariants()
        {
            AssertOutputInvariants(LoadAll<DefenderUnitData>(DefenderFolder), so => so.name, so => so.outputs);
        }

        [Test]
        public void AllEnemies_SatisfyProjectionInvariants()
        {
            AssertOutputInvariants(LoadAll<AttackUnitData>(EnemyFolder), so => so.name, so => so.outputs);
        }

        // Uniqueness is per-type: the importer matches defenders[] rows against the
        // Defenders folder and enemies[] rows against the Enemies folder in separate
        // id indexes, so a defender and an enemy may legitimately share an id
        // (e.g. Defender_Sniper / Enemy_Sniper).
        [Test]
        public void DefenderIds_NonEmptyAndUnique()
        {
            var seen = new Dictionary<string, string>();
            foreach (var so in LoadAll<DefenderUnitData>(DefenderFolder))
                AssertId(so.id, so.name, seen);
        }

        [Test]
        public void EnemyIds_NonEmptyAndUnique()
        {
            var seen = new Dictionary<string, string>();
            foreach (var so in LoadAll<AttackUnitData>(EnemyFolder))
                AssertId(so.id, so.name, seen);
        }

        private static void AssertId(string id, string assetName, Dictionary<string, string> seen)
        {
            Assert.IsFalse(string.IsNullOrEmpty(id), $"'{assetName}' has an empty id — the importer matches on id.");
            Assert.IsFalse(seen.ContainsKey(id),
                $"id '{id}' is shared by '{assetName}' and '{(seen.TryGetValue(id, out var other) ? other : "?")}' — import would skip both.");
            seen[id] = assetName;
        }

        // defender-unit-visibility unit 1 — 숨김 스위치가 생기면서 «목록에 보이는 유닛»과
        // «카탈로그에 있는 유닛»이 갈라졌다. LoadoutGate 는 편성 슬롯을 **정확히** 채워야
        // START 를 열어주므로, 보이는 유닛이 슬롯 수보다 적으면 신규 플레이어가 편성을
        // 완성할 방법이 없다(목록에 없는 유닛은 넣을 수 없다).
        //
        // 저작 실수를 코드로 막지는 않는다(런타임 하한선 없음) — 대신 여기서 잡는다.
        // 실패는 금지가 아니라 **재협상 신호**다: 정말로 유닛을 그만큼 감추고 싶다면
        // SquadPreset.SlotCount 를 함께 낮춰야 한다.
        [Test]
        public void VisibleDefenders_CanFillASquad()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"'{CatalogPath}' 를 찾을 수 없다");

            // 세는 방식은 SquadCharacterPageController.BuildLists 와 **같아야** 한다.
            // catalog.units 를 직접 훑으면 id 가 빈 에셋까지 세어(AllIds 는 그걸 건너뛴다)
            // 정작 목록에는 안 뜨는 유닛으로 정원을 채운 것처럼 통과한다.
            int visible = CountSelectableDefenders(catalog);

            Assert.GreaterOrEqual(visible, SquadPreset.SlotCount,
                $"목록에 보이는 방어유닛이 {visible}기뿐이라 {SquadPreset.SlotCount}슬롯 편성을 채울 수 없다 — " +
                "visible=0 을 되돌리거나 SquadPreset.SlotCount 를 재협상하라.");
        }

        // 시작 편성 저작이 숨김 유닛을 집으면 신규 프로필이 목록에 없는 유닛을 들고
        // 시작한다. 빼고 나면 다시 넣을 수 없으므로 저작 시점에 잡는다.
        [Test]
        public void DefaultSquadUnits_AreAllVisible()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<DefenderCatalog>(CatalogPath);
            Assert.IsNotNull(catalog, $"'{CatalogPath}' 를 찾을 수 없다");

            var authored = catalog.defaultSquadUnits;
            if (authored != null && authored.Length > 0)
            {
                foreach (var unit in authored)
                    if (unit != null)
                        Assert.AreNotEqual(0, unit.visible,
                            $"시작 편성에 숨김 유닛 '{unit.id}' 가 들어 있다 — 목록에 없는 유닛으로 시작하게 된다.");
                return;
            }

            // 저작이 비면 ProfileStore.EnsureDefaultSquad 가 catalog.AllIds() 앞에서부터
            // 집는 폴백으로 떨어진다. 그 폴백은 visible 을 **보지 않으므로**(spec 후속 후보)
            // 여기서 그냥 return 하면 정작 위험한 경우에 테스트가 조용히 통과한다.
            // 폴백이 실제로 집을 앞쪽 슬롯 수만큼을 대신 검사한다.
            int checkedCount = 0;
            foreach (var id in catalog.AllIds())
            {
                if (checkedCount >= SquadPreset.SlotCount) break;
                var unit = catalog.ById(id);
                if (unit == null) continue;
                Assert.AreNotEqual(0, unit.visible,
                    $"defaultSquadUnits 가 비어 폴백이 도는데 앞쪽 '{id}' 가 숨김이다 — " +
                    "신규 프로필이 목록에 없는 유닛으로 시작한다. 저작하거나 숨김을 되돌려라.");
                checkedCount++;
            }
        }

        // BuildLists 와 같은 규칙: AllIds()(빈 id 스킵) ∩ visible != 0.
        private static int CountSelectableDefenders(DefenderCatalog catalog)
        {
            int n = 0;
            foreach (var id in catalog.AllIds())
            {
                var unit = catalog.ById(id);
                if (unit != null && unit.visible != 0) n++;
            }
            return n;
        }

        private static void AssertOutputInvariants<T>(IEnumerable<T> assets, System.Func<T, string> nameOf, System.Func<T, AttackOutput[]> outputsOf)
        {
            foreach (var so in assets)
            {
                var outputs = outputsOf(so);
                if (outputs == null) continue;
                int damage = 0, heal = 0;
                foreach (var o in outputs)
                {
                    if (o.kind == AttackOutputKind.Damage) damage++;
                    else if (o.kind == AttackOutputKind.Heal) heal++;
                }
                Assert.LessOrEqual(damage, 1, $"'{nameOf(so)}' has {damage} Damage outputs. {DamageHint}");
                Assert.LessOrEqual(heal, 1, $"'{nameOf(so)}' has {heal} Heal outputs. {HealHint}");
            }
        }

        private static List<T> LoadAll<T>(string folder) where T : UnityEngine.Object
        {
            var list = new List<T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null) list.Add(asset);
            }
            return list;
        }
    }
}
