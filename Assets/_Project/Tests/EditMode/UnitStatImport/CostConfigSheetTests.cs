using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.Data.StatImport;
using Object = UnityEngine.Object;

namespace Wassup.Tests.EditMode.UnitStatImport
{
    // sheet-export-push unit 7 — CostConfig 탭 apply 코어. 네트워크 없이
    // CostConfigRuntimeRefresher.ApplyBody 로 구동한다(DcSheetRuntimeRefreshTests 형제).
    public class CostConfigSheetTests
    {
        private static string Body(string rowsJson) => $"{{ \"success\": true, \"data\": [{rowsJson}] }}";
        private const string ErrorBody = @"{ ""success"": false, ""errorDetail"": { ""errorCode"": ""INTERNAL_SERVER_ERROR"", ""detailMessage"": ""구글 시트 연동 실패"" } }";

        private static CostConfig NewConfig(string id)
        {
            var so = ScriptableObject.CreateInstance<CostConfig>();
            so.id = id;
            so.startingCost = 10;
            so.maxCost = 10;
            so.regenPerSec = 0.35f;
            so.placementPhaseDuration = 30f;
            return so;
        }

        [Test]
        public void ApplyBody_FilledCellsApply_BlankCellsKeepExistingValues()
        {
            var config = NewConfig("cost_default");

            // 시트에서 maxCost/regenPerSec 만 채운 행 — 나머지 열은 빈 셀(키 생략).
            string log = CostConfigRuntimeRefresher.ApplyBody(
                new SheetFetcher.Result(Body(@"{ ""id"": ""cost_default"", ""maxCost"": 20, ""regenPerSec"": 0.75 }"), null),
                config);

            Assert.AreEqual(20, config.maxCost, "채운 셀은 반영된다");
            Assert.AreEqual(0.75f, config.regenPerSec, "채운 셀은 반영된다");
            Assert.AreEqual(10, config.startingCost, "빈 셀은 기존 값을 유지한다(blank=keep)");
            Assert.AreEqual(30f, config.placementPhaseDuration, "빈 셀은 기존 값을 유지한다(blank=keep)");
            StringAssert.Contains("matched 1", log);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void ApplyBody_UnknownId_LeavesConfigUntouched()
        {
            var config = NewConfig("cost_default");

            string log = CostConfigRuntimeRefresher.ApplyBody(
                new SheetFetcher.Result(Body(@"{ ""id"": ""ghost"", ""maxCost"": 99 }"), null), config);

            Assert.AreEqual(10, config.maxCost, "매칭되지 않은 시트 행은 SO 를 건드리지 않는다");
            StringAssert.Contains("no match for id='ghost'", log);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void ApplyBody_DuplicateRows_FirstWinsAndRestSkipped()
        {
            var config = NewConfig("cost_default");

            string log = CostConfigRuntimeRefresher.ApplyBody(
                new SheetFetcher.Result(Body(
                    @"{ ""id"": ""cost_default"", ""maxCost"": 20 }, { ""id"": ""cost_default"", ""maxCost"": 99 }"), null),
                config);

            Assert.AreEqual(20, config.maxCost, "같은 키가 두 행이면 첫 행만 적용된다");
            StringAssert.Contains("duplicate row", log);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void ApplyBody_ConfigWithBlankId_MatchesNothingAndReports()
        {
            var config = NewConfig("");

            string log = CostConfigRuntimeRefresher.ApplyBody(
                new SheetFetcher.Result(Body(@"{ ""id"": ""cost_default"", ""maxCost"": 99 }"), null), config);

            Assert.AreEqual(10, config.maxCost, "id 가 비면 어떤 행과도 매칭되지 않는다");
            StringAssert.Contains("id 가 비어", log);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void ApplyBody_FetchFailed_LeavesConfigUntouched()
        {
            var config = NewConfig("cost_default");

            string log = CostConfigRuntimeRefresher.ApplyBody(
                new SheetFetcher.Result(ErrorBody, null), config);

            Assert.AreEqual(10, config.maxCost, "탭 fetch 실패는 값을 바꾸지 않는다");
            StringAssert.Contains("구글 시트 연동 실패", log);

            Object.DestroyImmediate(config);
        }

        [Test]
        public void Refresh_LockArmedBeforeCallback_DoesNotApplyAndClearsInFlight()
        {
            var go = new GameObject("CostConfigRuntimeRefresher");
            var refresher = go.AddComponent<CostConfigRuntimeRefresher>();
            var config = NewConfig("cost_default");
            Action<SheetFetcher.Result> callback = null;
            string result = null;

            try
            {
                typeof(CostConfigRuntimeRefresher).GetField("config", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(refresher, config);
                refresher.Fetch = (_, done) => callback = done;

                refresher.Refresh(log => result = log);
                Assert.IsTrue(refresher.RequestInFlight);
                Assert.IsNotNull(callback);

                TestModeContext.SetHarnessSeed(9876);
                callback(new SheetFetcher.Result(
                    Body(@"{ ""id"": ""cost_default"", ""maxCost"": 99 }"), null));

                Assert.AreEqual(10, config.maxCost);
                Assert.AreEqual(TestModeContext.RuntimeImportBlockedLog, result);
                Assert.IsFalse(refresher.RequestInFlight);
            }
            finally
            {
                TestModeContext.ClearHarness();
                Object.DestroyImmediate(config);
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void ApplyBody_NullConfig_ReportsAndDoesNotThrow()
        {
            string log = CostConfigRuntimeRefresher.ApplyBody(
                new SheetFetcher.Result(Body(@"{ ""id"": ""cost_default"", ""maxCost"": 99 }"), null), null);

            StringAssert.Contains("config 미할당", log);
        }
    }
}
