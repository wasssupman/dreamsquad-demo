using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Presentation;

namespace Wassup.Tests.PlayMode
{
    public class UnitOverheadUiLifecycleTest
    {
        private GameObject _host;
        private UnitOverheadUiLayer _layer;
        private UnitOverheadUiStyle _style;
        private DreamcatcherCard _card;
        private GameObject _bridgeHost;
        private GameObject _legacyHost;

        [SetUp]
        public void SetUp()
        {
            _style = ScriptableObject.CreateInstance<UnitOverheadUiStyle>();
            _card = ScriptableObject.CreateInstance<DreamcatcherCard>();
            _card.type = CardType.Unit;
            _host = new GameObject("UnitOverheadUiLayerTest");
            _layer = _host.AddComponent<UnitOverheadUiLayer>();
            SetPrivate(_layer, "style", _style);
        }

        [TearDown]
        public void TearDown()
        {
            if (_bridgeHost != null) Object.DestroyImmediate(_bridgeHost);
            if (_legacyHost != null) Object.DestroyImmediate(_legacyHost);
            if (_host != null) Object.DestroyImmediate(_host);
            if (_card != null) Object.DestroyImmediate(_card);
            if (_style != null) Object.DestroyImmediate(_style);
        }

        [UnityTest]
        public IEnumerator Reconcile_SharesSprites_FadesOnlyBar_AndClearsDespawnedViews()
        {
            var first = new Entity { Index = 101, Version = 1 };
            var second = new Entity { Index = 102, Version = 1 };
            CardsByHost(_layer)[first] = new List<DreamcatcherCard> { _card };

            _layer.BeginFrame();
            _layer.SetUnit(first, true, 1f, new Vector2(120f, 160f), 80f);
            _layer.SetUnit(second, true, 0.5f, new Vector2(220f, 160f), 80f);
            _layer.EndFrame();

            var active = ActiveViews(_layer);
            UnitOverheadView firstView = active[first];
            UnitOverheadView secondView = active[second];
            var firstBar = firstView.transform.Find("HealthBar");
            var secondBar = secondView.transform.Find("HealthBar");
            Assert.That(firstView.GetComponent<CanvasGroup>(), Is.Null,
                "full-health fade must not affect the card row root");
            Assert.That(firstBar.GetComponent<CanvasGroup>().alpha,
                Is.EqualTo(_style.Defender.fullHealthAlpha).Within(1e-5f));
            Assert.That(firstView.transform.Find("Dreamcatcher0").GetComponentInParent<CanvasGroup>(), Is.Null);
            Assert.That(firstBar.GetComponent<Image>().sprite,
                Is.SameAs(secondBar.GetComponent<Image>().sprite), "bar sprites must be shared per layer");

            _layer.BeginFrame();
            _layer.SetUnit(second, true, 0.5f, new Vector2(220f, 160f), 80f);
            _layer.EndFrame();
            Assert.That(firstView.gameObject.activeSelf, Is.False);
            Assert.That(ActiveViews(_layer).ContainsKey(first), Is.False);

            _layer.Clear();
            yield return null;
            Assert.That(_host.transform.childCount, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator LegacyUnifiedSwitch_IsMutuallyExclusive_AndClearsUnifiedViews()
        {
            _legacyHost = new GameObject("LegacyDcStripTest");
            var legacy = _legacyHost.AddComponent<DcIconStripSpawner>();
            _bridgeHost = new GameObject("BattleBridgeModeTest");
            _bridgeHost.SetActive(false); // Awake의 전체 battle bootstrap 없이 private routing만 검증한다.
            var bridge = _bridgeHost.AddComponent<BattleBridge>();
            SetPrivate(bridge, "dcIconStripSpawner", legacy);
            SetPrivate(bridge, "unitOverheadUiLayer", _layer);

            SetPrivate(bridge, "unitHealthPresentationMode", UnitHealthPresentationMode.UnifiedOverhead);
            InvokePrivate(bridge, "ApplyUnitHealthPresentationMode");
            Assert.That(GetPrivate(legacy, "_presentationEnabled"), Is.False);

            var entity = new Entity { Index = 201, Version = 1 };
            _layer.BeginFrame();
            _layer.SetUnit(entity, true, 1f, new Vector2(120f, 160f), 80f);
            _layer.EndFrame();
            Assert.That(ActiveViews(_layer).Count, Is.EqualTo(1));

            SetPrivate(bridge, "unitHealthPresentationMode", UnitHealthPresentationMode.Legacy);
            InvokePrivate(bridge, "ApplyUnitHealthPresentationMode");
            Assert.That(GetPrivate(legacy, "_presentationEnabled"), Is.True);
            Assert.That(ActiveViews(_layer).Count, Is.EqualTo(0));

            SetPrivate(bridge, "unitHealthPresentationMode", UnitHealthPresentationMode.UnifiedOverhead);
            InvokePrivate(bridge, "ApplyUnitHealthPresentationMode");
            Assert.That(GetPrivate(legacy, "_presentationEnabled"), Is.False);
            yield return null;
        }

        private static Dictionary<Entity, UnitOverheadView> ActiveViews(UnitOverheadUiLayer layer)
            => (Dictionary<Entity, UnitOverheadView>)GetPrivate(layer, "_active");

        private static Dictionary<Entity, List<DreamcatcherCard>> CardsByHost(UnitOverheadUiLayer layer)
            => (Dictionary<Entity, List<DreamcatcherCard>>)GetPrivate(layer, "_cardsByHost");

        private static object GetPrivate(object target, string name)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(target);

        private static void SetPrivate(object target, string name, object value)
            => target.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);

        private static void InvokePrivate(object target, string name)
            => target.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(target, null);
    }
}
