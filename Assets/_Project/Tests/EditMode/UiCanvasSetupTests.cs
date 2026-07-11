using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using Wassup.UI.Layout;

namespace Wassup.Tests.EditMode
{
    public class UiCanvasSetupTests
    {
        private GameObject _host;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("UiCanvasSetupHost", typeof(RectTransform));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host);
        }

        [Test]
        public void Ensure_ConfiguresHeightMatchedOverlayCanvas()
        {
            var roots = UiCanvasSetup.Ensure(_host, sortingOrder: 17);
            var scaler = _host.GetComponent<CanvasScaler>();

            Assert.AreEqual(RenderMode.ScreenSpaceOverlay, roots.Canvas.renderMode);
            Assert.AreEqual(17, roots.Canvas.sortingOrder);
            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(UiCanvasSetup.ReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(CanvasScaler.ScreenMatchMode.MatchWidthOrHeight, scaler.screenMatchMode);
            Assert.AreEqual(1f, scaler.matchWidthOrHeight);
            Assert.IsNotNull(_host.GetComponent<GraphicRaycaster>());
        }

        [Test]
        public void Ensure_RepairsExistingScalerConfiguration()
        {
            var scaler = _host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            scaler.referenceResolution = new Vector2(800f, 600f);
            scaler.matchWidthOrHeight = 0f;

            UiCanvasSetup.Ensure(_host, sortingOrder: 2);

            Assert.AreEqual(CanvasScaler.ScaleMode.ScaleWithScreenSize, scaler.uiScaleMode);
            Assert.AreEqual(UiCanvasSetup.ReferenceResolution, scaler.referenceResolution);
            Assert.AreEqual(1f, scaler.matchWidthOrHeight);
        }

        [Test]
        public void Ensure_ReusesRootsAndComponents()
        {
            var first = UiCanvasSetup.Ensure(_host, sortingOrder: 3);
            var second = UiCanvasSetup.Ensure(_host, sortingOrder: 4);

            Assert.AreSame(first.Canvas, second.Canvas);
            Assert.AreSame(first.FullBleedRoot, second.FullBleedRoot);
            Assert.AreSame(first.SafeAreaRoot, second.SafeAreaRoot);
            Assert.AreEqual(1, _host.GetComponents<Canvas>().Length);
            Assert.AreEqual(1, _host.GetComponents<CanvasScaler>().Length);
            Assert.AreEqual(1, _host.GetComponents<GraphicRaycaster>().Length);
            Assert.AreEqual(1, second.SafeAreaRoot.GetComponents<UiSafeAreaFitter>().Length);
            Assert.AreEqual(4, second.Canvas.sortingOrder);
        }

        [Test]
        public void Ensure_FullBleedRootStretchesWithoutOffsets()
        {
            var roots = UiCanvasSetup.Ensure(_host, sortingOrder: 0);

            Assert.AreEqual(Vector2.zero, roots.FullBleedRoot.anchorMin);
            Assert.AreEqual(Vector2.one, roots.FullBleedRoot.anchorMax);
            Assert.AreEqual(Vector2.zero, roots.FullBleedRoot.offsetMin);
            Assert.AreEqual(Vector2.zero, roots.FullBleedRoot.offsetMax);
            Assert.AreEqual(Vector3.one, roots.FullBleedRoot.localScale);
        }
    }
}
