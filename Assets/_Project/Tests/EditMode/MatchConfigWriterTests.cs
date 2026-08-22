using System.Globalization;
using System.Threading;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    // battle-sim-extraction M0 unit 3 — 조건 지문의 계약.
    //
    // 이 해시가 하는 일은 하나다: 골든이 갈렸을 때 «코드가 바뀐 건가, 값이 바뀐 건가» 를
    // 먼저 가르는 것. 그래서 지켜야 할 성질도 둘뿐이다 — 같은 조건은 반드시 같은 해시,
    // 다른 조건은 반드시 다른 해시. 아래 테스트가 각각의 실패 모드를 하나씩 막는다.
    public class MatchConfigWriterTests
    {
        private class StatFixture : ScriptableObject
        {
            public float damage = 10f;
            public int cost = 3;
            public bool flying;
            [SerializeField] private float hidden = 1f;   // private + SerializeField 도 조건이다
            public Sprite icon;                            // 아트 — 담기지 않아야 한다
            public Material mat;                           // 〃
            public void SetHidden(float v) => hidden = v;
        }

        private static MatchConfigWriter W()
        {
            var w = new MatchConfigWriter();
            w.Section("s");
            w.Put("i", 7);
            w.Put("f", 1.5f);
            w.Put("b", true);
            w.Put("str", "abc");
            return w;
        }

        [Test]
        public void SameInput_SameHashAndText()
        {
            var a = W().Build();
            var b = W().Build();
            Assert.AreEqual(a.text, b.text);
            Assert.AreEqual(a.hash, b.hash);
            Assert.IsFalse(a.IsEmpty);
        }

        [Test]
        public void OneChangedValue_ChangesHash()
        {
            var baseline = W().Build();
            var w = W();
            w.Put("extra", 0.0001f);
            Assert.AreNotEqual(baseline.hash, w.Build().hash);
        }

        [Test]
        public void NullIsDistinctFromEmptyString()
        {
            var a = new MatchConfigWriter(); a.Put("k", (string)null);
            var b = new MatchConfigWriter(); b.Put("k", string.Empty);
            Assert.AreNotEqual(a.Build().hash, b.Build().hash,
                "«참조가 없다» 와 «이름이 빈 문자열» 은 다른 조건이다");
        }

        [Test]
        public void FloatFormatting_IsCultureInvariant()
        {
            // 쉼표를 소수점으로 쓰는 문화권에서 돌면 해시가 통째로 갈린다 — 빌드 머신과
            // 개발 머신이 다른 로케일일 때 조용히 «조건이 다르다» 가 된다.
            var before = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var de = W().Build();
                Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
                Assert.AreEqual(W().Build().hash, de.hash);
            }
            finally { Thread.CurrentThread.CurrentCulture = before; }
        }

        [Test]
        public void Asset_StatChange_ChangesHash_IncludingPrivateSerializedField()
        {
            var so = ScriptableObject.CreateInstance<StatFixture>();
            so.name = "fixture";
            var w1 = new MatchConfigWriter(); w1.PutAsset("a", so);
            string baseline = w1.Build().hash;

            so.damage = 11f;
            var w2 = new MatchConfigWriter(); w2.PutAsset("a", so);
            Assert.AreNotEqual(baseline, w2.Build().hash, "public 스탯 변경이 해시에 반영돼야 한다");

            so.damage = 10f;
            so.SetHidden(2f);
            var w3 = new MatchConfigWriter(); w3.PutAsset("a", so);
            Assert.AreNotEqual(baseline, w3.Build().hash,
                "[SerializeField] private 도 게임 값이므로 반영돼야 한다");
            Object.DestroyImmediate(so);
        }

        [Test]
        public void Asset_ArtReference_DoesNotChangeHash()
        {
            var so = ScriptableObject.CreateInstance<StatFixture>();
            so.name = "fixture";
            var w1 = new MatchConfigWriter(); w1.PutAsset("a", so);
            string baseline = w1.Build().hash;

            so.mat = new Material(Shader.Find("Sprites/Default"));
            var w2 = new MatchConfigWriter(); w2.PutAsset("a", so);
            Assert.AreEqual(baseline, w2.Build().hash,
                "아트 교체는 «조건이 바뀌었다» 가 아니다 — 담으면 판독 장치가 거짓말을 한다");

            Object.DestroyImmediate(so.mat);
            Object.DestroyImmediate(so);
        }

        [Test]
        public void NullAsset_IsRecordedNotSkipped()
        {
            var w = new MatchConfigWriter();
            w.PutAsset("a", null);
            StringAssert.Contains("a=~", w.Text, "빠진 참조도 조건이다 — 조용히 건너뛰면 안 된다");
        }
    }
}
