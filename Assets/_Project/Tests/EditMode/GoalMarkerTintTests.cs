using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // map-diorama-stage unit 6 — 골 마커 비주얼이 포탈 프랍(파티클, 머티리얼 _Color = HDR 밝기)이 되면서
    // 스트레스 틴트가 저작 색을 **덮지 않고 곱해야** 한다. 스트레스 0 = 저작 그대로, 1 = 저작 × stressTint.
    public class GoalMarkerTintTests
    {
        static readonly Color Base = new Color(2.4f, 2.4f, 2.4f, 1f);   // Portal_Circle 과 같은 꼴의 HDR 부스터

        [Test]
        public void StressTint_MultipliesAuthoredMaterialColor_InsteadOfOverwriting()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            Assume.That(shader, Is.Not.Null, "URP Unlit 셰이더가 없는 환경");
            var host = new GameObject("goal");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Quad);
            var mat = new Material(shader);
            try
            {
                visual.transform.SetParent(host.transform, false);
                mat.SetColor("_BaseColor", Base);
                mat.SetColor("_Color", Base);
                var r = visual.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                var marker = host.AddComponent<GoalMarker>();
                marker.visualRoot = visual.transform;
                marker.stressTint = new Color(0.5f, 0.25f, 0f, 1f);

                var mpb = new MaterialPropertyBlock();
                marker.SetStressTint(0f, 1.5f);   // 스트레스 0 — 심박 배율도 k=0 이라 1
                r.GetPropertyBlock(mpb);
                AssertColor(mpb.GetColor("_BaseColor"), Base, "스트레스 0 에서 저작 색(HDR) 그대로");

                marker.SetStressTint(1f, 1f);
                r.GetPropertyBlock(mpb);
                AssertColor(mpb.GetColor("_Color"), new Color(1.2f, 0.6f, 0f, 1f), "스트레스 1 = 저작 × stressTint");
            }
            finally
            {
                Object.DestroyImmediate(mat);
                Object.DestroyImmediate(host);
            }
        }

        static void AssertColor(Color actual, Color expected, string why)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1e-3f), why + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1e-3f), why + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1e-3f), why + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(1e-3f), why + " (a)");
        }
    }
}
