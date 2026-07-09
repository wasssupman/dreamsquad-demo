using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Wassup.Logging;

namespace Wassup.Tests.EditMode
{
    // tournament-play-report Unit 1 — SnapshotJson must serialize the live entry
    // without closing the session; EndSession keeps working afterwards.
    public class BattleLoggerSnapshotTests
    {
        private GameObject _go;
        private BattleLogger _logger;
        private HashSet<string> _preExistingLogs;
        private string _logDir;

        [SetUp]
        public void SetUp()
        {
            // mirror BattleLogger.ResolveLogDirectory (editor branch) so files
            // created by this test can be removed again.
            _logDir = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "GameLogs");
            _preExistingLogs = Directory.Exists(_logDir)
                ? new HashSet<string>(Directory.GetFiles(_logDir))
                : new HashSet<string>();

            _go = new GameObject("BattleLoggerSnapshotTests");
            _logger = _go.AddComponent<BattleLogger>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_go);
            if (!Directory.Exists(_logDir)) return;
            foreach (var file in Directory.GetFiles(_logDir).Where(f => !_preExistingLogs.Contains(f)))
                File.Delete(file);
        }

        [Test]
        public void SnapshotJson_WithoutSession_ReturnsNull()
        {
            Assert.IsNull(_logger.SnapshotJson());
        }

        [Test]
        public void SnapshotJson_ContainsResultAndScore_AndKeepsSessionOpen()
        {
            _logger.StartSession();
            _logger.SetScore(123);
            _logger.SetResult("victory", 0);

            string json = _logger.SnapshotJson();

            Assert.IsNotNull(json);
            StringAssert.Contains("\"score\":123", json);
            StringAssert.Contains("\"outcome\":\"victory\"", json);
            StringAssert.DoesNotContain("\n", json); // compact transport form

            // session must still be open: EndSession writes the file normally.
            _logger.EndSession();
            Assert.IsTrue(Directory.GetFiles(_logDir).Any(f => !_preExistingLogs.Contains(f)),
                "EndSession after SnapshotJson should still write the session file");
        }
    }
}
