using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.StatImport;

namespace Wassup.Tests.EditModeAssets
{
    // skill-data-table unit 5 — **시트 왕복 = 굽기 무변**(5 완료 기준 「export → import 왕복 후 굽기 스냅샷 동치」).
    //
    // 라이브 에셋을 새 두 탭(`Skills` · `SkillOwners`)의 줄로 내보내고 → 시트 페이로드 JSON 으로 직렬화 → 봉투 파서로 다시 읽어 →
    // **값을 비운 메모리 사본**(효과 값 · 참조 · 소유 줄 전부 0)에 import 한 뒤 굽는다. 결과가 커밋된 스냅샷 두 파일
    // (`card_bake_snapshot.txt` · `binding_bake_snapshot.txt`)과 글자까지 같아야 한다 — 시트 두 탭이 효과와 소유 줄을 **빠짐없이** 싣는다는 증거.
    // 디스크 쓰기 0(사본은 저장하지 않는다) · 네트워크 0.
    public class SkillSheetRoundTripTests
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
            c.name = original.name;   // 굽기 로그 · 스냅샷이 이름을 쓴다
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

        [Test]
        public void Export_Import_RoundTrip_OntoBlankCopies_BakesIdenticalSnapshots()
        {
            var effects = BindingBakeSnapshotTests.LiveAssets<EffectData>();
            var cards = CardEffectWitnessTests.Cards();
            var units = BindingBakeSnapshotTests.LiveAssets<DefenderUnitData>();
            var enemies = BindingBakeSnapshotTests.LiveAssets<AttackUnitData>();
            Assert.IsNotEmpty(effects, "효과 에셋이 없다");

            // ① export(원본 · 메모리) → 시트 JSON → 봉투 파서
            var exported = SkillSheet.Export(effects, cards, units, enemies);
            TestContext.WriteLine($"Skills {exported.skills.Length} 줄 · SkillOwners {exported.owners.Length} 줄");
            Assert.AreEqual(effects.Count, exported.skills.Length, "효과 하나 = Skills 한 줄");
            var payload = new SkillSheetPayload
            {
                skills = ParseTab<SkillRowDto>(SkillSheet.ToJson(exported.skills), DcSheetTabs.Skills),
                owners = ParseTab<SkillOwnerRowDto>(SkillSheet.ToJson(exported.owners), DcSheetTabs.SkillOwners),
            };

            // ② 값을 비운 사본(효과 id · 뷰 칸만 남긴다 · 소유 줄 0 — 빈 배열: 시트에 줄이 없는 소유자 = 소유 줄 없음)
            var paths = new Dictionary<Object, string>();
            var effectCopies = effects.Select(e =>
            {
                var c = Copy(e);
                c.values = default;
                c.deprecated = false;
                c.projectile = null;
                c.pattern = null;
                c.hazard = null;
                return c;
            }).ToList();
            var cardCopies = cards.Select(x => { var c = Copy(x); c.bindings = System.Array.Empty<BindingSpec>(); return c; }).ToList();
            var unitCopies = units.Select(x => { var c = Copy(x); c.bindings = System.Array.Empty<BindingSpec>(); paths[c] = AssetDatabase.GetAssetPath(x); return c; }).ToList();
            var enemyCopies = enemies.Select(x => { var c = Copy(x); c.bindings = System.Array.Empty<BindingSpec>(); paths[c] = AssetDatabase.GetAssetPath(x); return c; }).ToList();

            // ③ import(사본 · 쓰기 콜백 없음)
            var log = new StringBuilder();
            var index = SkillSheetIndex.Build(effectCopies,
                BindingBakeSnapshotTests.LiveAssets<ProjectileData>(), BindingBakeSnapshotTests.LiveAssets<ProjectilePatternData>(),
                BindingBakeSnapshotTests.LiveAssets<HazardSO>(), cardCopies, unitCopies, enemyCopies, log);
            string result = SkillSheet.Import(payload, index, apply: true, onApplied: null, log);
            TestContext.WriteLine(result.Split('\n')[0]);
            StringAssert.DoesNotContain("no effect for", result, "없는 효과 id 가 없어야 한다");
            StringAssert.DoesNotContain("not found", result, "없는 탄 · 패턴 · 장판 id 가 없어야 한다");
            StringAssert.DoesNotContain("owner skipped", result, "건너뛴 소유자가 없어야 한다");
            StringAssert.DoesNotContain("no owner for", result, "없는 소유자가 없어야 한다");

            // ④ 굽기 = 커밋된 스냅샷(픽스처 무변)
            string cardNow = CardBakeSnapshotTests.Bake(cardCopies);
            Assert.IsNull(CardBakeSnapshotTests.FirstDiff(CardBakeSnapshotTests.Committed(), cardNow),
                "시트 왕복 뒤 카드 굽기가 스냅샷과 다르다: " + CardBakeSnapshotTests.FirstDiff(CardBakeSnapshotTests.Committed(), cardNow));

            string bindingNow = BindingBakeSnapshotTests.Bake(unitCopies, enemyCopies, cardCopies, o => paths[o]);
            string committed = System.IO.File.ReadAllText(BindingBakeSnapshotTests.SnapshotPath).Replace("\r\n", "\n");
            Assert.IsNull(BindingBakeSnapshotTests.FirstDiff(committed, bindingNow),
                "시트 왕복 뒤 유닛 · 적 굽기가 스냅샷과 다르다: " + BindingBakeSnapshotTests.FirstDiff(committed, bindingNow));
        }

        [Test]
        public void 반증_사본을_비우기만_하고_import_안_하면_굽기가_다르다()
        {
            // 위 테스트의 초록이 「사본이 원본 값을 들고 있어서」가 아님을 보인다 — 비운 사본은 스냅샷과 달라야 한다.
            var cards = CardEffectWitnessTests.Cards();
            var cardCopies = cards.Select(x => { var c = Copy(x); c.bindings = System.Array.Empty<BindingSpec>(); return c; }).ToList();
            Assert.IsNotNull(CardBakeSnapshotTests.FirstDiff(CardBakeSnapshotTests.Committed(), CardBakeSnapshotTests.Bake(cardCopies)),
                "소유 줄을 비운 카드 사본이 스냅샷과 같다 — 왕복 테스트가 아무것도 증언하지 못한다");
        }

        [Test]
        public void 라이브_export_는_옛_탭_어휘를_쓰지_않는다()
        {
            var exported = SkillSheet.Export(BindingBakeSnapshotTests.LiveAssets<EffectData>(), CardEffectWitnessTests.Cards(),
                BindingBakeSnapshotTests.LiveAssets<DefenderUnitData>(), BindingBakeSnapshotTests.LiveAssets<AttackUnitData>());
            var kinds = exported.owners.Select(o => o.ownerKind).Distinct().OrderBy(k => k).ToArray();
            CollectionAssert.AreEquivalent(new[] { SkillSheet.OwnerCard, SkillSheet.OwnerDefender, SkillSheet.OwnerEnemy }, kinds,
                "U19 — owner_kind = card · defender · enemy");
            foreach (var r in exported.skills)
                Assert.IsFalse(string.IsNullOrEmpty(r.kindKo), $"'{r.id}' 한국어 표시 열이 비었다");
            foreach (var o in exported.owners)
                Assert.IsFalse(string.IsNullOrEmpty(o.effectId), $"{o.ownerKind}/{o.ownerId} slot {o.slot} 효과 id 가 비었다");
        }
    }
}
