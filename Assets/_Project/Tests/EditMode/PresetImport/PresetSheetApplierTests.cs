using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Wassup.Data;
using Wassup.Data.PresetImport;

namespace Wassup.Tests.EditMode.PresetImport
{
    // preset-sheet-import unit 1 — PresetSheetApplier 순수 재구성 회귀 커버리지.
    // 해석기는 in-test 딕셔너리 Func. SO 는 참조 동일성으로만 검증(.id 세터 불필요).
    public class PresetSheetApplierTests
    {
        private const int MaxUnits = 7;

        private DefenderUnitData _uA, _uB, _uC;
        private DreamcatcherCard _cX, _cY;
        private Dictionary<string, DefenderUnitData> _units;
        private Dictionary<string, DreamcatcherCard> _cards;
        private SquadPresetCollection _collection;
        private StringBuilder _log;

        private System.Func<string, DefenderUnitData> ResolveUnit
            => id => _units.TryGetValue(id, out var v) ? v : null;
        private System.Func<string, DreamcatcherCard> ResolveCard
            => id => _cards.TryGetValue(id, out var v) ? v : null;

        [SetUp]
        public void Setup()
        {
            _uA = ScriptableObject.CreateInstance<DefenderUnitData>();
            _uB = ScriptableObject.CreateInstance<DefenderUnitData>();
            _uC = ScriptableObject.CreateInstance<DefenderUnitData>();
            _cX = ScriptableObject.CreateInstance<DreamcatcherCard>();
            _cY = ScriptableObject.CreateInstance<DreamcatcherCard>();
            _units = new Dictionary<string, DefenderUnitData> { { "uA", _uA }, { "uB", _uB }, { "uC", _uC } };
            _cards = new Dictionary<string, DreamcatcherCard> { { "cX", _cX }, { "cY", _cY } };
            _collection = ScriptableObject.CreateInstance<SquadPresetCollection>();
            _log = new StringBuilder();
        }

        [TearDown]
        public void Teardown()
        {
            foreach (var o in new Object[] { _uA, _uB, _uC, _cX, _cY, _collection })
                if (o != null) Object.DestroyImmediate(o);
        }

        private bool Apply(params PresetDto[] rows)
            => PresetSheetApplier.Apply(rows, ResolveUnit, ResolveCard, MaxUnits, _collection, _log);

        private static PresetDto Row(string name, string squad, string dc)
            => new PresetDto { presetName = name, squad = squad, dreamcatcher = dc };

        [Test]
        public void Apply_TwoRows_RebuildsPresets()
        {
            bool ok = Apply(Row("A", "uA,uB", "cX"), Row("B", "uC", "cX,cY"));

            Assert.IsTrue(ok);
            Assert.AreEqual(2, _collection.presets.Count);
            Assert.AreEqual("A", _collection.presets[0].presetName);
            CollectionAssert.AreEqual(new[] { _uA, _uB }, _collection.presets[0].units);
            CollectionAssert.AreEqual(new[] { _cX }, _collection.presets[0].cards);
            CollectionAssert.AreEqual(new[] { _uC }, _collection.presets[1].units);
            CollectionAssert.AreEqual(new[] { _cX, _cY }, _collection.presets[1].cards);
        }

        [Test]
        public void Apply_UnresolvedUnit_NullSlotPreservesOrder()
        {
            Apply(Row("A", "uA,BAD,uC", "cX"));

            CollectionAssert.AreEqual(new[] { _uA, null, _uC }, _collection.presets[0].units);
        }

        [Test]
        public void Apply_UnresolvedCard_Skipped()
        {
            Apply(Row("A", "uA", "cX,BAD,cY"));

            CollectionAssert.AreEqual(new[] { _cX, _cY }, _collection.presets[0].cards);
        }

        [Test]
        public void Apply_ExcessUnits_ClampedToMax()
        {
            Apply(Row("A", "uA,uA,uA,uA,uA,uA,uA,uA,uA", "cX")); // 9 tokens

            Assert.AreEqual(MaxUnits, _collection.presets[0].units.Length);
        }

        [Test]
        public void Apply_WhitespaceCsv_TrimmedAndEmptyDropped()
        {
            Apply(Row("A", " uA , , uB ", " cX "));

            CollectionAssert.AreEqual(new[] { _uA, _uB }, _collection.presets[0].units);
            CollectionAssert.AreEqual(new[] { _cX }, _collection.presets[0].cards);
        }

        [Test]
        public void Apply_BlankSquad_EmptyUnits()
        {
            Apply(Row("A", null, "cX"));

            Assert.AreEqual(0, _collection.presets[0].units.Length);
            Assert.AreEqual("A", _collection.presets[0].presetName);
        }

        [Test]
        public void Apply_NullRows_KeepsExistingReturnsFalse()
        {
            _collection.presets = new List<SquadPreset> { new SquadPreset { presetName = "SENTINEL" } };

            bool ok = PresetSheetApplier.Apply(null, ResolveUnit, ResolveCard, MaxUnits, _collection, _log);

            Assert.IsFalse(ok);
            Assert.AreEqual(1, _collection.presets.Count);
            Assert.AreEqual("SENTINEL", _collection.presets[0].presetName);
        }

        [Test]
        public void Apply_EmptyRows_KeepsExistingReturnsFalse()
        {
            _collection.presets = new List<SquadPreset> { new SquadPreset { presetName = "SENTINEL" } };

            bool ok = Apply(); // 빈 배열

            Assert.IsFalse(ok);
            Assert.AreEqual(1, _collection.presets.Count);
            Assert.AreEqual("SENTINEL", _collection.presets[0].presetName);
        }
    }
}
