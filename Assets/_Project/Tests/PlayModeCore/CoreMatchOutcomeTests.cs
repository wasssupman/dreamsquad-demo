using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Wassup.BattleCore;
using Wassup.BattleCore.Goals;
using Wassup.BattleCoreUnity;
using Wassup.Data;

namespace Wassup.Tests.PlayMode.Core
{
    // battle-core-rebuild unit 5c — **판이 끝나면 결과가 보이고, 올라갈 판만 올라간다.**
    //
    // ⚠ 여기서 도는 판은 **3분이 아니다.** 시계 길이는 모드 저작이고, 2초짜리 모드를 테스트
    // 모드 강제 칸에 넣어 같은 사슬(만료 → `MatchEnded` → 성적 → 제출 게이트 → 결과 화면)을
    // 3분 대신 2초에 돌린다. 「3분 완주」는 사용자 플레이가 답하는 질문이고, 이 lane 이 답하는
    // 것은 **그 사슬이 이어지는가**다 — 시계에 큰 수를 넣어 테스트를 느리게 만드는 것은
    // 같은 증언을 더 비싸게 사는 일이다.
    //
    // ⚠ **서버를 두드리지 않는다.** 이 판들은 로비 게이트를 거치지 않아 참가 신청(attemptId)이
    // 없고, `TournamentMatchReporter` 는 그 경우 통보를 **생략한다**(그쪽의 정상 경로다).
    // 그래서 가짜 리포터를 끼우지 않고도 네트워크가 나가지 않는다 — 대신 「게이트를 넘어
    // 호출을 걸었나」는 발표자가 직접 증언한다(`Submitted`).
    public sealed class CoreMatchOutcomeTests
    {
        private MatchModeData _mode;

        [TearDown]
        public void TearDown()
        {
            MatchEntryContext.Clear();
            DestroyMode(_mode);
            _mode = null;
        }

        [UnityTest]
        public IEnumerator 판이_끝나면_결과_화면이_한_번_뜬다()
        {
            CoreSceneFixture.BeginErrorWatch();
            BattleDriver driver = null;
            yield return BootShortMatch("test_short_submit", allowSubmit: true, d => driver = d);

            int endedEvents = 0;
            System.Action<CoreEvent> probe = e =>
            {
                if (e.Kind == CoreEventKind.MatchEnded) endedEvents++;
            };
            driver.Subscribe(ViewOrder.Trace, probe);

            // demo-diet unit 0 — 결과 화면은 전투 밖으로 갔다. 전투가 증언하는 것은 「성적 사건이 한 번 나갔다」와
            // 「박자(붕괴 연출)가 규칙대로 돌았다」 둘뿐이다.
            var beat = Object.FindAnyObjectByType<CoreMatchEndBeat>();
            Assert.IsNotNull(beat, "BattleCoreScene 에 종료 박자가 없다");
            int finished = 0;
            MatchOutcome last = default;
            System.Action<BattleDriver, MatchOutcome> onFinished = (d, o) => { finished++; last = o; };
            driver.MatchFinished += onFinished;
            Assert.AreEqual(0, finished, "판이 아직 도는데 성적 사건이 나갔다");

            yield return RunUntilEnded(driver);
            driver.Unsubscribe(probe);
            driver.MatchFinished -= onFinished;

            Assert.IsTrue(driver.Match.Clock.Ended, "2초짜리 판이 안 끝났다");
            Assert.AreEqual(MatchEndReason.Complete, driver.Match.Clock.EndReason);
            Assert.AreEqual(1, endedEvents, "종료 사건은 판당 하나다");
            Assert.AreEqual(1, finished, "성적 사건은 판당 하나다");
            Assert.AreEqual(driver.Match.Outcome.Score, last.Score, "사건이 나른 성적 = 코어의 성적(값 스냅샷)");

            // 만료는 터지는 것이 없어 박자가 없다(`EndHasPresentationBeat` 거짓).
            yield return null;
            Assert.AreEqual(0, beat.BeatCount, "만료 종료에 박자가 돌았다");
            Assert.IsFalse(beat.HoldActive);

            // **종료 뒤 틱 0**(계약 5). 드라이버가 계속 `Update` 를 돌아도 판은 안 움직인다.
            int tickAtEnd = driver.Match.Clock.Tick;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.AreEqual(tickAtEnd, driver.Match.Clock.Tick, "끝난 판이 계속 틱을 먹었다");

            CoreSceneFixture.EndErrorWatch();
            Assert.AreEqual(0, CoreSceneFixture.Errors.Count,
                "콘솔 에러: " + string.Join(" | ", CoreSceneFixture.Errors));
        }

        // demo-diet unit 0 — 통보(서버 제출)는 전투 밖(`MatchFinished` 구독자)의 일이 됐다. 전투가 증언하는 것은
        // 「제출 게이트 두 칸의 값이 사건과 정의표에 실려 나간다」는 것이다 — 구독자는 이 둘만 보고 올릴지 정한다.
        [UnityTest]
        public IEnumerator 제출_게이트가_열린_모드는_성적_사건이_제출_대상임을_말한다()
        {
            BattleDriver driver = null;
            yield return BootShortMatch("test_short_submit", allowSubmit: true, d => driver = d);
            MatchOutcome last = default; int finished = 0;
            driver.MatchFinished += (d, o) => { finished++; last = o; };
            yield return RunUntilEnded(driver);

            Assert.AreEqual(1, finished);
            Assert.IsTrue(last.SubmitsReport && driver.Definition.Mode.AllowSubmit,
                "submitsReport && allowSubmit 인 모드인데 사건·정의표가 제출 대상이 아니라고 말한다");
        }

        [UnityTest]
        public IEnumerator 제출을_닫은_모드도_성적_사건은_나가고_게이트_뒤칸만_닫혀_있다()
        {
            BattleDriver driver = null;
            yield return BootShortMatch("test_short_nosubmit", allowSubmit: false, d => driver = d);

            // 게이트의 앞칸(`submitsReport`)은 열어 둔다 — 그래야 이 테스트가 **뒤칸만**
            // 증언한다. 둘 다 닫으면 어느 쪽이 막았는지 알 수 없다.
            Assert.IsTrue(driver.Definition.Mode.SubmitsReport);
            Assert.IsFalse(driver.Definition.Mode.AllowSubmit);

            int finished = 0;
            driver.MatchFinished += (d, o) => finished++;
            yield return RunUntilEnded(driver);

            Assert.AreEqual(1, finished, "제출을 닫아도 성적 사건은 나간다(표시·기록은 구독자 몫)");
            Assert.IsFalse(driver.Definition.Mode.AllowSubmit, "allowSubmit 이 꺼진 모드의 게이트가 열려 있다");
        }

        [UnityTest]
        public IEnumerator 모드_선택은_테스트_강제가_로비_지정을_이긴다()
        {
            var forced = ShortMode("test_forced", allowSubmit: true);
            var lobby = ShortMode("test_lobby", allowSubmit: true);
            try
            {
                MatchEntryContext.Set(new ModeSelection(forced, lobby, 0));
                BattleDriver driver = null;
                yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
                Assert.IsNotNull(driver);

                Assert.AreEqual("test_forced", driver.Definition.Mode.ModeId,
                    "테스트 모드 강제가 로비 지정을 못 이겼다 — 3단 서열이 깨졌다");
                // 선택은 **1회 소비**다. 안 지우면 다음 판이 이 모드를 물려받는다.
                Assert.IsFalse(MatchEntryContext.HasPending, "진입 선택이 소비되지 않았다");
            }
            finally
            {
                MatchEntryContext.Clear();
                DestroyMode(forced);
                DestroyMode(lobby);
            }
        }

        [UnityTest]
        public IEnumerator 배치가_성사되면_배치음이_한_번_난다()
        {
            // 이 판은 **씬 저작 모드 그대로** 돈다(배치 입력이 열려 있는 모드라야 놓을 수 있다).
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver);

            var audio = Object.FindAnyObjectByType<CoreBattleAudio>();
            Assert.IsNotNull(audio, "BattleCoreScene 에 전투 사운드가 없다");
            Assert.IsNotNull(Wassup.Core.SoundManager.Instance, "새 씬에 SoundManager 가 없다");
            Assert.AreEqual(0, audio.PlaceCues);

            driver.Apply(Command.FinishPlacement());

            int defIndex;
            Unity.Mathematics.int2 anchor;
            Assert.IsTrue(TryFindPlaceable(driver, out defIndex, out anchor),
                "이 판 어디에도 놓을 수 없다");
            var receipt = driver.Apply(Command.PlaceDefender(defIndex, anchor));
            Assert.IsTrue(receipt.Accepted, "판정이 통과한 자리인데 거절됐다: " + receipt.Reason);

            // 소리는 단언할 수 없지만 **호출은 셀 수 있다** — 「사건당 한 번」이 그 계약이다.
            Assert.AreEqual(1, audio.PlaceCues, "배치 한 번에 배치음이 한 번이어야 한다");
            yield return null;
            Assert.AreEqual(1, audio.PlaceCues, "다음 프레임에 같은 배치가 또 울렸다");
        }

        // ── 공용 ─────────────────────────────────────────────────────────────

        private IEnumerator BootShortMatch(string modeId, bool allowSubmit,
                                           System.Action<BattleDriver> found)
        {
            _mode = ShortMode(modeId, allowSubmit);
            // ⚠ **씬 로드 «전»에** 놓는다 — 드라이버가 `Start` 에서 소비한다.
            MatchEntryContext.Set(ModeSelection.ForTest(_mode));
            BattleDriver driver = null;
            yield return CoreSceneFixture.LoadAndBoot(d => driver = d);
            Assert.IsNotNull(driver, "BattleCoreScene 에 BattleDriver 가 없다");
            Assert.AreEqual(modeId, driver.Definition.Mode.ModeId, "강제한 모드로 판이 서지 않았다");
            found(driver);
        }

        // 배치 페이즈가 **아예 없는** 2초 모드. 전투로 시작해 만료로 끝난다.
        private static MatchModeData ShortMode(string modeId, bool allowSubmit)
        {
            var m = ScriptableObject.CreateInstance<MatchModeData>();
            m.modeId = modeId;
            m.goalKind = GoalKind.KillScoreTimed;
            m.clockKind = ClockKind.FixedLimit;
            m.durationSec = 2f;
            m.submitUnlockSec = 0f;
            m.allowSubmit = allowSubmit;
            m.submitsReport = true;
            m.waveSourceKind = WaveSourceKind.GeneratedFromDeck;
            m.placementPhaseEnabled = false;   // 입력 없음
            m.autoStartCountdownSec = 0f;      // 기다릴 시간도 없음 → 배치 페이즈 자체가 없다
            m.gimmickEnabled = false;
            // 배치 자원 저작은 **필수**다 — 없으면 빌더가 loud 오류를 낸다(2026-09-24 드리프트
            // 감사: 옛 배치 창 폴백 30초가 SO 폴백 0초로 조용히 뒤집혀 있었다). 이 모드는 배치
            // 입력이 꺼져 있어 창 길이는 카운트다운(0)이 정한다 — 값은 코드 기본값 그대로다.
            m.costConfig = ScriptableObject.CreateInstance<CostConfig>();
            return m;
        }

        private static void DestroyMode(MatchModeData m)
        {
            if (m == null) return;
            if (m.costConfig != null) Object.DestroyImmediate(m.costConfig);
            Object.DestroyImmediate(m);
        }

        // 2초 판이라 프레임 상한은 넉넉히 준다(틱 발행은 실시간을 따른다).
        private static IEnumerator RunUntilEnded(BattleDriver driver)
        {
            for (int i = 0; i < 900 && !driver.Match.Clock.Ended; i++) yield return null;
            Assert.IsTrue(driver.Match.Clock.Ended, "판이 시간 안에 안 끝났다");
            yield return null;   // 종료 사건 배달 뒤 한 프레임
        }

        private static bool TryFindPlaceable(BattleDriver driver, out int defIndex,
                                             out Unity.Mathematics.int2 anchor)
        {
            var placement = driver.Match.Placement;
            var size = driver.GridSize;
            for (int i = 0; i < driver.Definition.Units.Length; i++)
            {
                if (!placement.InRoster(i)) continue;
                for (int y = 0; y < size.y; y++)
                for (int x = 0; x < size.x; x++)
                {
                    var c = new Unity.Mathematics.int2(x, y);
                    if (placement.Judge(i, c) != RejectReason.None) continue;
                    defIndex = i;
                    anchor = c;
                    return true;
                }
            }
            defIndex = -1;
            anchor = default;
            return false;
        }
    }
}
