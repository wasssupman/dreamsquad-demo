using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // outgame-login-gate unit 5 — tray collapse/expand state machine.
    public class DevTrayToggleTests
    {
        private GameObject _root;
        private GameObject _content;
        private DevTrayToggle _toggle;
        private Button _button;

        [SetUp]
        public void SetUp()
        {
            _root = new GameObject("DevButtons");
            _content = new GameObject("DevTrayContent");
            _content.transform.SetParent(_root.transform);

            var buttonGo = new GameObject("DevToggleButton");
            buttonGo.transform.SetParent(_root.transform);
            _button = buttonGo.AddComponent<Button>();

            _toggle = _root.AddComponent<DevTrayToggle>();
            Set("toggleButton", _button);
            Set("content", _content);
            // label is left null on purpose — the toggle must not require it.

            // Awake does not run on AddComponent outside play mode; drive the same
            // entry point the component uses.
            _toggle.SetExpanded(false);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_root);

        private void Set(string field, Object value)
        {
            typeof(DevTrayToggle)
                .GetField(field, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .SetValue(_toggle, value);
        }

        [Test]
        public void StartsCollapsed()
        {
            Assert.IsFalse(_toggle.IsExpanded);
            Assert.IsFalse(_content.activeSelf, "the tray content must be hidden by default");
        }

        [Test]
        public void Toggle_ExpandsThenCollapses()
        {
            _toggle.Toggle();
            Assert.IsTrue(_toggle.IsExpanded);
            Assert.IsTrue(_content.activeSelf);

            _toggle.Toggle();
            Assert.IsFalse(_toggle.IsExpanded);
            Assert.IsFalse(_content.activeSelf);
        }

        [Test]
        public void Toggle_WithoutLabel_DoesNotThrow()
        {
            // the label is optional wiring; a missing ref must not break the tray
            Assert.DoesNotThrow(() => _toggle.Toggle());
            Assert.IsTrue(_content.activeSelf);
        }
    }
}
