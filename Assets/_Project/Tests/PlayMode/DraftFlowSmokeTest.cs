using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.PlayMode
{
    // PlayMode smoke for the discard model wired through DraftController.
    // Builds a minimal scene (controller + 10-unit catalog), drives a full
    // discard cycle without the visual orchestrator (DraftView), and asserts
    // DraftConfirmed fires once with a 7-unit pool. The visual timing of
    // strip/fan tweens is exercised separately in editor smoke checks.
    public class DraftFlowSmokeTest
    {
        private GameObject _root;
        private DraftController _controller;
        private List<DefenderUnitData> _catalog;
        private int _confirmedCount;

        [SetUp]
        public void SetUp()
        {
            _catalog = new List<DefenderUnitData>();
            for (int i = 0; i < 10; i++)
            {
                var unit = ScriptableObject.CreateInstance<DefenderUnitData>();
                unit.displayName = $"Unit_{i}";
                unit.health = 10 + i;
                _catalog.Add(unit);
            }

            _root = new GameObject("DraftSmoke_Root");
            _controller = _root.AddComponent<DraftController>();

            var catalogField = typeof(DraftController).GetField(
                "catalog", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            catalogField?.SetValue(_controller, _catalog.ToArray());

            _confirmedCount = 0;
            _controller.DraftConfirmed += OnConfirmed;
        }

        [TearDown]
        public void TearDown()
        {
            if (_controller != null) _controller.DraftConfirmed -= OnConfirmed;
            if (_root != null) Object.Destroy(_root);
            foreach (var u in _catalog) Object.DestroyImmediate(u);
            _catalog = null;
        }

        private void OnConfirmed() => _confirmedCount++;

        [UnityTest]
        public IEnumerator BeginDraft_ThreeDiscards_AutoConfirms_With_Seven_Picks()
        {
            _controller.BeginDraft(seed: 12345);
            yield return null;

            Assert.AreEqual(10, _controller.Session.Pool.Count, "pool size");

            // Discard pool indices 0, 1, 2.
            for (int i = 0; i < 3; i++)
                Assert.IsTrue(_controller.ToggleDiscard(_controller.Session.Pool[i]),
                    $"discard {i} should succeed");

            Assert.IsTrue(_controller.Session.IsFull, "session should be full after 3 discards");
            Assert.AreEqual(7, _controller.Session.PickedArray().Length);

            Assert.IsTrue(_controller.TryConfirm(), "TryConfirm should succeed");
            yield return null;

            Assert.AreEqual(1, _confirmedCount, "DraftConfirmed should fire exactly once");
        }
    }
}
