using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Wassup.SheetSync;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // sheet-export-push unit 0 — SheetSync 코어의 봉투 파서 회귀. 동일 wire shape
    // ({success,data,errorDetail})의 분기를 직접 검증(전엔 BuildReport 통해 간접만).
    public class SheetEnvelopeTests
    {
        [Test]
        public void Success_WithObjectData_ReturnsObject()
        {
            bool ok = SheetEnvelope.TryGetData(
                @"{ ""success"": true, ""data"": { ""results"": {} } }", out var data, out var err);
            Assert.IsTrue(ok);
            Assert.IsNull(err);
            Assert.IsInstanceOf<JObject>(data);
        }

        [Test]
        public void Success_WithArrayData_ReturnsArray()
        {
            // import 응답은 data 가 배열 — 같은 파서가 둘 다 받아야 한다.
            bool ok = SheetEnvelope.TryGetData(
                @"{ ""success"": true, ""data"": [ { ""id"": ""a"" } ] }", out var data, out var err);
            Assert.IsTrue(ok);
            Assert.IsInstanceOf<JArray>(data);
        }

        [Test]
        public void EmptyBody_Fails()
        {
            bool ok = SheetEnvelope.TryGetData("   ", out var data, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(data);
            StringAssert.Contains("empty", err);
        }

        [Test]
        public void MalformedJson_Fails()
        {
            bool ok = SheetEnvelope.TryGetData("{ not json", out var data, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(data);
            StringAssert.Contains("valid JSON", err);
        }

        [Test]
        public void SuccessFalse_ComposesErrorDetail()
        {
            bool ok = SheetEnvelope.TryGetData(
                @"{ ""success"": false, ""errorDetail"": { ""errorCode"": ""E1"", ""errorMessage"": ""boom"" } }",
                out var data, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(data);
            StringAssert.Contains("boom", err);
            StringAssert.Contains("E1", err);
        }

        [Test]
        public void SuccessFalse_NoErrorDetail_StillReportsFailure()
        {
            bool ok = SheetEnvelope.TryGetData(@"{ ""success"": false }", out var data, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(data);
            Assert.IsNotNull(err);
        }

        [Test]
        public void SuccessTrue_MissingData_Fails()
        {
            bool ok = SheetEnvelope.TryGetData(@"{ ""success"": true }", out var data, out var err);
            Assert.IsFalse(ok);
            Assert.IsNull(data);
            StringAssert.Contains("data", err);
        }
    }
}
