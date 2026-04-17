using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.Tests.EditMode
{
    public class SkillLoadoutControllerTests
    {
        private GameObject _host;
        private SkillLoadoutController _ctl;
        private List<SkillData> _pool;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("SkillLoadoutHost");
            _ctl = _host.AddComponent<SkillLoadoutController>();
            _pool = new List<SkillData>();
            for (int i = 0; i < 6; i++)
            {
                var s = ScriptableObject.CreateInstance<SkillData>();
                s.id = $"skill_{i}";
                s.displayName = $"Skill {i}";
                _pool.Add(s);
            }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _pool) Object.DestroyImmediate(s);
            _pool = null;
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void Roll_Same_Seed_Produces_Same_Picks()
        {
            _ctl.Configure(_pool, 2, seed: 12345);
            var first = new List<SkillData>(_ctl.Roll());

            _ctl.Configure(_pool, 2, seed: 12345);
            var second = new List<SkillData>(_ctl.Roll());

            CollectionAssert.AreEqual(first, second);
            Assert.AreEqual(2, first.Count);
        }

        [Test]
        public void Roll_Different_Seed_May_Produce_Different_Picks()
        {
            _ctl.Configure(_pool, 2, seed: 1);
            var a = new List<SkillData>(_ctl.Roll());

            _ctl.Configure(_pool, 2, seed: 2);
            var b = new List<SkillData>(_ctl.Roll());

            // With a 6-item pool and k=2, two different seeds can collide, but
            // seeds 1 and 2 on System.Random diverge in early output — assert the
            // diff and pin this as a deterministic regression guard.
            Assert.IsFalse(a[0] == b[0] && a[1] == b[1], "Seeds 1 and 2 should not collide with this pool.");
        }

        [Test]
        public void Roll_Picks_Are_Unique()
        {
            _ctl.Configure(_pool, 2, seed: 42);
            var picked = _ctl.Roll();
            Assert.AreEqual(2, picked.Count);
            Assert.AreNotEqual(picked[0], picked[1]);
        }

        [Test]
        public void Roll_Count_Greater_Than_Pool_Caps_To_Pool()
        {
            _ctl.Configure(_pool, 20, seed: 7);
            var picked = _ctl.Roll();
            Assert.AreEqual(_pool.Count, picked.Count);
            var unique = new HashSet<SkillData>(picked);
            Assert.AreEqual(_pool.Count, unique.Count);
        }

        [Test]
        public void Roll_Empty_Pool_Returns_Empty()
        {
            _ctl.Configure(new List<SkillData>(), 2, seed: 1);
            var picked = _ctl.Roll();
            Assert.AreEqual(0, picked.Count);
            Assert.IsTrue(_ctl.HasRolled);
        }

        [Test]
        public void Seed_Zero_Is_Replaced_With_Nonzero_At_Roll()
        {
            _ctl.Configure(_pool, 2, seed: 0);
            _ctl.Roll();
            Assert.AreNotEqual(0, _ctl.Seed);
        }

        [Test]
        public void ResetRollState_Clears_Picks_And_Seed()
        {
            _ctl.Configure(_pool, 2, seed: 99);
            _ctl.Roll();
            Assert.AreEqual(99, _ctl.Seed);

            _ctl.ResetRollState();
            Assert.AreEqual(0, _ctl.Picked.Count);
            Assert.IsFalse(_ctl.HasRolled);
            Assert.AreEqual(0, _ctl.Seed);
        }
    }
}
