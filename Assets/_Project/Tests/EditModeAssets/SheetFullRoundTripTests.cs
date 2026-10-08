using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Somnia.Battle.Data;
using Somnia.Battle.Data.StatImport;
using Somnia.Battle.Editor.UnitStatImport;

namespace Somnia.Battle.Tests.EditModeAssets
{
    // skill-data-table unit 9 완료 기준 — **전 탭 왕복 = 무변**. 라이브 에셋 → 8탭 전부의 줄(메모리 — export 와 같은 줄 짓기) → 시트 JSON
    // (스네이크 열) → 봉투 파서(계약 밖 헤더 0) → **시트가 싣는 칸을 비운 메모리 사본**에 적용 → ① 사본에서 다시 낸 줄 = 원본 줄(스탯 칸 동일)
    // ② 카드 · 유닛/적 굽기 = 커밋된 스냅샷 두 파일. 디스크 쓰기 0(사본은 저장하지 않는다) · 네트워크 0.
    // 탭: Defenders · Enemies · Cards · DcSkills · DcConfig · CostConfig · Skills · SkillOwners.
    public class SheetFullRoundTripTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private T Copy<T>(T original) where T : ScriptableObject
        {
            var c = Object.Instantiate(original);
            c.name = original.name;
            _made.Add(c);
            return c;
        }

        private static T[] ParseTab<T>(string json, string label)
        {
            string body = "{ \"success\": true, \"data\": " + json + " }";
            var unknown = new HashSet<string>();
            var rows = SheetEnvelopeParser.ParseSheetRows<T>(body, out string error, unknown);
            Assert.IsNotNull(rows, $"{label} 줄을 다시 못 읽었다: {error}");
            Assert.IsEmpty(unknown, $"{label} 헤더가 DTO 계약 밖이다: {string.Join(", ", unknown)}");
            return rows;
        }

        // 시트 줄이 싣는 SO 칸(이름 짝 — DTO 의 C# 이름)을 기본값으로. 키(`id`) · 투영(`atk` · `heal`) · 폐기(`attackDamage`) · 정보 열(`_`)은 뺀다.
        private static void ClearMapped(ScriptableObject so, System.Type dto)
        {
            foreach (var f in dto.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (f.Name.StartsWith("_") || f.Name == "id" || f.Name == "atk" || f.Name == "heal" || f.Name == "attackDamage") continue;
                var target = so.GetType().GetField(f.Name, BindingFlags.Public | BindingFlags.Instance);
                if (target == null) continue;
                // 문자열 = "" — Unity 직렬화는 null 문자열을 "" 로 읽고, 시트의 빈 칸(= "")은 「그대로」라 둘을 가를 수 없다.
                object blank = target.FieldType == typeof(string) ? "" : target.FieldType.IsValueType ? System.Activator.CreateInstance(target.FieldType) : null;
                target.SetValue(so, blank);
            }
        }

        private static Dictionary<string, T> ById<T>(IEnumerable<T> list, System.Func<T, string> id, StringBuilder log) where T : ScriptableObject
            => UnitStatApplier.BuildIndex(list, id, log, typeof(T).Name);

        [Test]
        public void AllTabs_Export_Parse_Apply_OntoClearedCopies_SameRowsAndBakes()
        {
            var defenders = BindingBakeSnapshotTests.LiveAssets<DefenderUnitData>();
            var enemies = BindingBakeSnapshotTests.LiveAssets<AttackUnitData>();
            var cards = CardEffectWitnessTests.Cards();
            var skills = BindingBakeSnapshotTests.LiveAssets<SkillData>();
            var awakenings = BindingBakeSnapshotTests.LiveAssets<AwakeningConfig>();
            var deckRules = BindingBakeSnapshotTests.LiveAssets<DeckRuleConfig>();
            var costs = BindingBakeSnapshotTests.LiveAssets<CostConfig>();
            var effects = BindingBakeSnapshotTests.LiveAssets<EffectData>();
            Assert.AreEqual(1, awakenings.Count, "카드 값의 주인(AwakeningConfig)은 하나");

            // ① 원본 → 탭 JSON(메모리)
            string Tabs(List<DefenderUnitData> d, List<AttackUnitData> e, List<DreamcatcherCard> c, List<SkillData> s,
                        List<AwakeningConfig> a, List<DeckRuleConfig> r, List<CostConfig> k, List<EffectData> fx, out string[] tabs)
            {
                var sheet = SkillSheet.Export(fx, c, d, e);
                tabs = new[]
                {
                    UnitStatExporter.ToRowsJson(d.Select(UnitStatExporter.ToDto).ToArray()),
                    UnitStatExporter.ToRowsJson(e.Select(UnitStatExporter.ToDto).ToArray()),
                    DcSheetExporter.ToJson(DcSheetExporter.CardRows(c.OrderBy(x => x.id, System.StringComparer.Ordinal))),
                    DcSheetExporter.ToJson(DcSheetExporter.SkillRows(s.OrderBy(x => x.id, System.StringComparer.Ordinal))),
                    DcSheetExporter.ToJson(DcSheetExporter.ConfigRows(a, r)),
                    DcSheetExporter.ToJson(CostConfigSheetExporter.Rows(k)),
                    SkillSheet.ToJson(sheet.skills),
                    SkillSheet.ToJson(sheet.owners),
                };
                return string.Join("\n", tabs);
            }
            Tabs(defenders, enemies, cards, skills, awakenings, deckRules, costs, effects, out var json);
            string[] names = { "Defenders", "Enemies", DcSheetTabs.Cards, DcSheetTabs.ActiveSkills, DcSheetTabs.Config, "CostConfig", DcSheetTabs.Skills, DcSheetTabs.SkillOwners };

            // ② 봉투 파서(계약 밖 헤더 0 — 스네이크 열이 전부 DTO 짝)
            var defRows = ParseTab<DefenderStatDto>(json[0], names[0]);
            var enemyRows = ParseTab<EnemyStatDto>(json[1], names[1]);
            var cardRows = ParseTab<DcCardDto>(json[2], names[2]);
            var skillRows = ParseTab<DcSkillDto>(json[3], names[3]);
            var configRows = ParseTab<DcConfigDto>(json[4], names[4]);
            var costRows = ParseTab<CostConfigDto>(json[5], names[5]);
            var skillPayload = new SkillSheetPayload
            {
                skills = ParseTab<SkillRowDto>(json[6], names[6]),
                owners = ParseTab<SkillOwnerRowDto>(json[7], names[7]),
            };

            // ③ 시트가 싣는 칸을 비운 사본
            var paths = new Dictionary<Object, string>();
            var defCopies = defenders.Select(x =>
            {
                var c = Copy(x);
                ClearMapped(c, typeof(DefenderStatDto));
                AttackOutputStats.TrySetUniqueMagnitude(c.outputs, AttackOutputKind.Damage, 0f);
                AttackOutputStats.TrySetUniqueMagnitude(c.outputs, AttackOutputKind.Heal, 0f);
                c.bindings = System.Array.Empty<BindingSpec>();
                paths[c] = AssetDatabase.GetAssetPath(x);
                return c;
            }).ToList();
            var enemyCopies = enemies.Select(x =>
            {
                var c = Copy(x);
                ClearMapped(c, typeof(EnemyStatDto));
                AttackOutputStats.TrySetUniqueMagnitude(c.outputs, AttackOutputKind.Damage, 0f);
                c.bindings = System.Array.Empty<BindingSpec>();
                paths[c] = AssetDatabase.GetAssetPath(x);
                return c;
            }).ToList();
            var cardCopies = cards.Select(x => { var c = Copy(x); ClearMapped(c, typeof(DcCardDto)); c.bindings = System.Array.Empty<BindingSpec>(); return c; }).ToList();
            var skillCopies = skills.Select(x => { var c = Copy(x); ClearMapped(c, typeof(DcSkillDto)); return c; }).ToList();
            var awakeningCopies = awakenings.Select(x => { var c = Copy(x); ClearMapped(c, typeof(DcConfigDto)); return c; }).ToList();
            var deckRuleCopies = deckRules.Select(x => { var c = Copy(x); ClearMapped(c, typeof(DcConfigDto)); return c; }).ToList();
            var costCopies = costs.Select(x => { var c = Copy(x); ClearMapped(c, typeof(CostConfigDto)); return c; }).ToList();
            var effectCopies = effects.Select(e =>
            {
                var c = Copy(e);
                c.values = SkillSheetRoundTripTests.BlankSheetColumns(e.values);
                c.deprecated = false;
                EffectSlots.UsedColumns(e.values.kind, out var used);
                if ((used & EffectColumns.ProjectileId) != 0) c.projectile = null;
                if ((used & EffectColumns.PatternId) != 0) c.pattern = null;
                if ((used & EffectColumns.HazardId) != 0) c.hazard = null;
                return c;
            }).ToList();

            // ④ 적용(사본 · 쓰기 콜백 없음) — 로그인 자동 import 와 같은 순서(평면 탭 → Skills/SkillOwners)
            var log = new StringBuilder();
            UnitStatApplier.Apply(UnitStatApplier.BuildPayload(defRows, enemyRows),
                ById(defCopies, x => x.id, log), ById(enemyCopies, x => x.id, log), null, log);
            var configsById = new Dictionary<string, ScriptableObject>();
            foreach (var a in awakeningCopies) configsById[a.id] = a;
            foreach (var r in deckRuleCopies) configsById[r.id] = r;
            DcSheetApplier.Apply(new DcSheetPayload { cards = cardRows, skills = skillRows, configs = configRows },
                ById(cardCopies, x => x.id, log), ById(skillCopies, x => x.id, log), configsById, null, log);
            CostConfigSheetApplier.Apply(costRows, ById(costCopies, x => x.id, log), null, log);
            var index = SkillSheetIndex.Build(effectCopies,
                BindingBakeSnapshotTests.LiveAssets<ProjectileData>(), BindingBakeSnapshotTests.LiveAssets<ProjectilePatternData>(),
                BindingBakeSnapshotTests.LiveAssets<HazardSO>(), cardCopies, defCopies, enemyCopies, log);
            SkillSheet.Import(skillPayload, index, apply: true, onApplied: null, log);
            string result = log.ToString();
            foreach (var bad in new[] { "no match", "no effect for", "not found", "owner skipped", "no owner for", "U20", "ignored.", "duplicate", "skipped." })
                StringAssert.DoesNotContain(bad, result, $"왕복 로그에 「{bad}」 — 싣지 못한 줄이 있다");

            // ⑤ 사본에서 다시 낸 줄 = 원본 줄(스탯 · 설정 · 카드 · 효과 · 소유 줄 칸 전부)
            Tabs(defCopies, enemyCopies, cardCopies, skillCopies, awakeningCopies, deckRuleCopies, costCopies, effectCopies, out var again);
            for (int i = 0; i < json.Length; i++)
                Assert.AreEqual(json[i], again[i], $"{names[i]} 탭 — 왕복 뒤 줄이 다르다");

            // ⑥ 굽기 = 커밋된 스냅샷(카드 값의 주인도 되짚은 사본)
            string cardNow = CardBakeSnapshotTests.Bake(cardCopies, awakeningCopies[0]);
            Assert.IsNull(CardBakeSnapshotTests.FirstDiff(CardBakeSnapshotTests.Committed(), cardNow),
                "전 탭 왕복 뒤 카드 굽기가 스냅샷과 다르다: " + CardBakeSnapshotTests.FirstDiff(CardBakeSnapshotTests.Committed(), cardNow));
            string bindingNow = BindingBakeSnapshotTests.Bake(defCopies, enemyCopies, cardCopies, o => paths[o]);
            string committed = System.IO.File.ReadAllText(BindingBakeSnapshotTests.SnapshotPath).Replace("\r\n", "\n");
            Assert.IsNull(BindingBakeSnapshotTests.FirstDiff(committed, bindingNow),
                "전 탭 왕복 뒤 유닛 · 적 굽기가 스냅샷과 다르다: " + BindingBakeSnapshotTests.FirstDiff(committed, bindingNow));
        }

        [Test]
        public void 반증_비운_사본은_원본_줄과_다르다()
        {
            // 위 테스트의 초록이 「사본이 원본 값을 들고 있어서」가 아님을 보인다.
            var cards = CardEffectWitnessTests.Cards();
            var copies = cards.Select(x => { var c = Copy(x); ClearMapped(c, typeof(DcCardDto)); return c; }).ToList();
            Assert.AreNotEqual(DcSheetExporter.ToJson(DcSheetExporter.CardRows(cards)), DcSheetExporter.ToJson(DcSheetExporter.CardRows(copies)));
            var defenders = BindingBakeSnapshotTests.LiveAssets<DefenderUnitData>();
            var defCopies = defenders.Select(x => { var c = Copy(x); ClearMapped(c, typeof(DefenderStatDto)); return c; }).ToList();
            Assert.AreNotEqual(UnitStatExporter.ToRowsJson(defenders.Select(UnitStatExporter.ToDto).ToArray()),
                               UnitStatExporter.ToRowsJson(defCopies.Select(UnitStatExporter.ToDto).ToArray()));
        }
    }
}
