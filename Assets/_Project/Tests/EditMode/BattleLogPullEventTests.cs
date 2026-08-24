using NUnit.Framework;
using UnityEngine;
using Wassup.Logging;

namespace Wassup.Tests.EditMode
{
    // wave-pull-revival unit 5 — 당김 기록이 **로컬 배틀 로그까지** 흐르는지.
    //
    // ⚠ **서버로는 가지 않는다.** `SnapshotJson()` 을 부르는 프로덕션 코드는 0곳이고
    // (유일한 호출자가 EditMode 테스트다), 서버로 가는 것은
    // `TournamentMatchReporter.ReportResult(score, deckInfoJson, …)` = 점수 int + 덱 정보뿐이다.
    // 배틀 로그의 종착지는 `EndSession()` 이 쓰는 `GameLogs/session-*.json` 이다.
    // 그래서 「나중에 남들과 당김 시점을 비교」(PRD §7.2)는 백엔드 계약이 선행이며 이 테스트가
    // 보장하는 것은 **로컬 기록이 비지 않는다**까지다.
    //
    // 그 범위 안에서 `ForceNextWave` → `RecordWaveEvent("wave_forced", …)` → 직렬화는
    // **이미 이어져 있었다.** 그래서 코드를 더하지 않고 연결만 고정한다 — 기록이 조용히
    // 빠지는 사고는 몇 달 뒤 «데이터가 왜 없지»로만 드러난다.
    public class BattleLogPullEventTests
    {
        private GameObject _go;
        private BattleLogger _logger;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestLogger");
            _logger = _go.AddComponent<BattleLogger>();
            _logger.StartSession();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void 당김_이벤트가_로컬_로그에_시각과_함께_남는다()
        {
            _logger.RecordWaveEvent("wave_forced", 3, 32.5f, forced: true);
            _logger.RecordWaveEvent("wave_forced", 4, 51f, forced: true);

            string json = _logger.SnapshotJson();
            Assert.IsNotNull(json, "스냅샷이 null 이다");

            StringAssert.Contains("wave_forced", json,
                "당김 이벤트가 제출 스냅샷에서 빠졌다 — 나중에 «언제 당겼나» 비교를 만들 수 없다");
            StringAssert.Contains("32.5", json, "당김 **시각**이 빠졌다 — 경과 시간 축이 PRD §8 의 요구다");
            StringAssert.Contains("\"waveIndex\":3", json, "당김 대상 웨이브가 빠졌다");
        }

        // bonus-wave-pull unit 5 — 보너스 당김도 같은 자리에 남아야 한다.
        // 안 남기면 랭킹에 올라간 점수의 일부가 어디서 왔는지 사후에 설명할 수 없다
        // (「덱·맵이 같은데 왜 점수가 다르지」에 답할 근거가 사라진다).
        [Test]
        public void 보너스_당김도_로컬_로그에_남는다()
        {
            _logger.RecordWaveEvent("bonus_pull", 5, 71.25f, forced: true);

            string json = _logger.SnapshotJson();
            StringAssert.Contains("bonus_pull", json,
                "보너스 당김이 제출 스냅샷에서 빠졌다 — 점수의 출처를 사후에 판독할 수 없다");
            StringAssert.Contains("71.25", json, "보너스 당김 **시각**이 빠졌다");
        }

        [Test]
        public void 종료_사유가_시간만료와_붕괴를_구분한다()
        {
            _logger.SetResult("defeat_timeout", 4);
            StringAssert.Contains("defeat_timeout", _logger.SnapshotJson());

            _logger.SetResult("defeat", 9);
            string json = _logger.SnapshotJson();
            StringAssert.Contains("\"outcome\":\"defeat\"", json,
                "시간 만료와 골 붕괴가 같은 문자열이면 종료 사유(PRD §8)를 복원할 수 없다");
        }

        [Test]
        public void 예상선은_스냅샷에_새지_않는다()
        {
            // wave-pull-revival 계약 9 — 진출 예상선은 **가짜 par** 다(서버가 아니라 저작
            // 비율에서 나온다). 기록에 새는 순간 가짜 경쟁 수치가 진짜인 척 저장된다.
            // 누군가 편의로 로거에 얹으면 여기서 빨개진다.
            _logger.RecordWaveEvent("wave_forced", 1, 10f, forced: true);
            string json = _logger.SnapshotJson();

            // 필드 **키** 단위로 본다. 맨 부분문자열이면 덱 id 나 맵 이름에 `space`·`pacer`
            // 가 들어오는 것만으로 오탐이 난다.
            string lower = json.ToLowerInvariant();
            StringAssert.DoesNotContain("\"pace", lower,
                "목표 페이스(pace*) 필드가 로그에 들어갔다 — 표시 전용 계약 위반");
            StringAssert.DoesNotContain("\"baseline", lower,
                "목표 페이스(baseline*) 필드가 로그에 들어갔다 — 표시 전용 계약 위반");
        }
    }
}
