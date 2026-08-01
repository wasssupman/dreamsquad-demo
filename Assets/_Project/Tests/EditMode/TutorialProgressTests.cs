using NUnit.Framework;
using Newtonsoft.Json.Linq;
using System.IO;
using UnityEngine;
using Wassup.Core;

namespace Wassup.Tests.EditMode
{
    public class TutorialProgressTests
    {
        private PlayerProfileSO _holder;

        [SetUp]
        public void SetUp() => _holder = ScriptableObject.CreateInstance<PlayerProfileSO>();

        [TearDown]
        public void TearDown()
        {
            if (_holder != null) Object.DestroyImmediate(_holder);
        }

        [Test]
        public void NewProfile_AllTutorialsPending()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsDragAttachHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyIntroPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyKeyringHintPending(profile));
        }

        [Test]
        public void LobbyCompletion_IsIndependentAndIdempotent()
        {
            var profile = new PlayerProfile();

            Assert.IsTrue(TutorialProgress.CompleteLobbyIntro(profile));
            Assert.IsFalse(TutorialProgress.IsLobbyIntroPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteLobbyIntro(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));

            Assert.IsTrue(TutorialProgress.CompleteLobbyLoadoutHint(profile));
            Assert.IsFalse(TutorialProgress.IsLobbyLoadoutHintPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteLobbyLoadoutHint(profile));

            Assert.IsFalse(TutorialProgress.CompleteLobbyIntro(null));
            Assert.IsFalse(TutorialProgress.CompleteLobbyLoadoutHint(null));
        }

        // Chapter B must never run alongside chapter A: it requires the in-game core
        // tutorial to be complete, which chapter A always precedes.
        [Test]
        public void LobbyLoadoutHint_RunsOnlyAfterCoreComplete()
        {
            var profile = new PlayerProfile();
            _holder.SetLoadedProfile(profile);

            Assert.IsTrue(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            // Chapter A done, but the first battle has not run yet.
            TutorialProgress.CompleteLobbyIntro(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            // Core complete → chapter B fires exactly here.
            TutorialProgress.CompleteCore(profile);
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            TutorialProgress.CompleteLobbyLoadoutHint(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));
        }

        [Test]
        public void LobbyChapters_RequireLoadedSessionAndNonNullProfile()
        {
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyIntro(_holder),
                "asset default profile is not a loaded session");
            TutorialProgress.CompleteCore(_holder.profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            _holder.SetLoadedProfile(null);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));

            _holder.SetLoadedProfile(new PlayerProfile());
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyIntro(_holder));
        }

        [Test]
        public void Completion_IsIndependentAndIdempotent()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.CompleteCore(profile));
            Assert.IsFalse(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsDragAttachHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteCore(profile));

            Assert.IsTrue(TutorialProgress.CompleteDragAttachHint(profile));
            Assert.IsFalse(TutorialProgress.IsDragAttachHintPending(profile));

            Assert.IsTrue(TutorialProgress.CompleteGiftTutorial(profile));
            Assert.IsFalse(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteGiftTutorial(profile));
        }

        [Test]
        public void GiftCompletion_DoesNotTouchOtherVersions()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.CompleteGiftTutorial(profile));
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsDragAttachHintPending(profile));
        }

        [Test]
        public void CurrentOrNewerVersion_DoesNotRun()
        {
            var profile = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion + 1,
                awakeningHintVersion = TutorialProgress.DragAttachHintVersion + 1,
                giftTutorialVersion = TutorialProgress.GiftTutorialVersion + 1,
            };
            Assert.IsFalse(TutorialProgress.IsCorePending(profile));
            Assert.IsFalse(TutorialProgress.IsDragAttachHintPending(profile));
            Assert.IsFalse(TutorialProgress.IsGiftTutorialPending(profile));
        }

        [Test]
        public void GiftTutorial_RunsOnlyAfterCoreComplete_NeverAlongsideCore()
        {
            var profile = new PlayerProfile();
            _holder.SetLoadedProfile(profile);

            // First run: core pending → the gift walkthrough must not fire.
            Assert.IsTrue(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));

            // Second run: core complete, gift pending → walkthrough fires exactly here.
            TutorialProgress.CompleteCore(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsTrue(TutorialProgress.ShouldRunGiftTutorial(_holder));

            // Third run: gift complete → normal presentation.
            TutorialProgress.CompleteGiftTutorial(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));
        }

        [Test]
        public void Holder_MustHaveBeenLoadedThisSession()
        {
            Assert.IsNotNull(_holder.profile, "asset default profile is intentionally non-null");
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunDragAttachHint(_holder));
            // Even with core complete on the asset default, not-loaded blocks gift too.
            TutorialProgress.CompleteCore(_holder.profile);
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));

            _holder.SetLoadedProfile(new PlayerProfile());
            Assert.IsTrue(_holder.IsLoadedThisSession);
            Assert.IsTrue(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsTrue(TutorialProgress.ShouldRunDragAttachHint(_holder));
        }

        [Test]
        public void NullProfile_NeverRunsOrCompletes()
        {
            _holder.SetLoadedProfile(null);
            Assert.IsFalse(TutorialProgress.ShouldRunCore(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunDragAttachHint(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunGiftTutorial(_holder));
            Assert.IsFalse(TutorialProgress.CompleteCore(null));
            Assert.IsFalse(TutorialProgress.CompleteDragAttachHint(null));
            Assert.IsFalse(TutorialProgress.CompleteGiftTutorial(null));
        }

        [Test]
        public void JsonRoundTrip_PreservesVersions()
        {
            var source = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
                awakeningHintVersion = TutorialProgress.DragAttachHintVersion,
                giftTutorialVersion = TutorialProgress.GiftTutorialVersion,
            };
            var loaded = JsonUtility.FromJson<PlayerProfile>(JsonUtility.ToJson(source));
            Assert.AreEqual(TutorialProgress.CoreVersion, loaded.firstBattleTutorialVersion);
            Assert.AreEqual(TutorialProgress.DragAttachHintVersion, loaded.awakeningHintVersion);
            Assert.AreEqual(TutorialProgress.GiftTutorialVersion, loaded.giftTutorialVersion);
        }

        [Test]
        public void LegacyJson_MissingFields_DefaultsToPending()
        {
            var loaded = JsonUtility.FromJson<PlayerProfile>("{\"schemaVersion\":1}");
            Assert.AreEqual(0, loaded.firstBattleTutorialVersion);
            Assert.AreEqual(0, loaded.awakeningHintVersion);
            Assert.AreEqual(0, loaded.giftTutorialVersion);
            Assert.AreEqual(0, loaded.lobbyIntroVersion);
            Assert.AreEqual(0, loaded.lobbyLoadoutHintVersion);
            Assert.AreEqual(0, loaded.lobbyKeyringHintVersion);
            Assert.AreEqual(0, loaded.gimmickRevealHintVersion);
            Assert.IsTrue(TutorialProgress.IsCorePending(loaded));
            Assert.IsTrue(TutorialProgress.IsDragAttachHintPending(loaded));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(loaded));
            Assert.IsTrue(TutorialProgress.IsLobbyIntroPending(loaded));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(loaded));
            Assert.IsTrue(TutorialProgress.IsLobbyKeyringHintPending(loaded));
            Assert.IsTrue(TutorialProgress.IsGimmickRevealHintPending(loaded));
        }

        [Test]
        public void ResetAll_ClearsOnlyTutorialVersionsAndIsIdempotent()
        {
            var profile = new PlayerProfile
            {
                selectedSquadId = "keep_squad",
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
                awakeningHintVersion = TutorialProgress.DragAttachHintVersion,
                giftTutorialVersion = TutorialProgress.GiftTutorialVersion,
                lobbyIntroVersion = TutorialProgress.LobbyIntroVersion,
                lobbyLoadoutHintVersion = TutorialProgress.LobbyLoadoutHintVersion,
            };

            Assert.IsTrue(TutorialProgress.ResetAll(profile));
            Assert.AreEqual(0, profile.firstBattleTutorialVersion);
            Assert.AreEqual(0, profile.awakeningHintVersion);
            Assert.AreEqual(0, profile.giftTutorialVersion);
            Assert.AreEqual(0, profile.lobbyIntroVersion);
            Assert.AreEqual(0, profile.lobbyLoadoutHintVersion);
            Assert.AreEqual("keep_squad", profile.selectedSquadId);
            Assert.IsFalse(TutorialProgress.ResetAll(profile));
            Assert.IsFalse(TutorialProgress.ResetAll(null));

            // A gift-only stamp must still count as a change.
            profile.giftTutorialVersion = TutorialProgress.GiftTutorialVersion;
            Assert.IsTrue(TutorialProgress.ResetAll(profile));

            // ...and so must a lobby-only stamp.
            profile.lobbyLoadoutHintVersion = TutorialProgress.LobbyLoadoutHintVersion;
            Assert.IsTrue(TutorialProgress.ResetAll(profile));
        }

        [Test]
        public void ResetAllInJson_PreservesKnownAndUnknownAccountData()
        {
            const string source = @"{
                'schemaVersion': 99,
                'firstBattleTutorialVersion': 7,
                'awakeningHintVersion': 3,
                'giftTutorialVersion': 5,
                'selectedSquadId': 'keep_squad',
                'futureAccountData': {
                    'currency': 12345,
                    'entitlements': ['founder', 'season_9']
                },
                'squads': [{ 'id': 'keep_squad', 'futureRank': 42 }]
            }";
            var expected = JObject.Parse(source);
            expected[nameof(PlayerProfile.firstBattleTutorialVersion)] = 0;
            expected[nameof(PlayerProfile.awakeningHintVersion)] = 0;
            expected[nameof(PlayerProfile.awakeningTapAttachHintVersion)] = 0;
            expected[nameof(PlayerProfile.giftTutorialVersion)] = 0;
            expected[nameof(PlayerProfile.lobbyIntroVersion)] = 0;
            expected[nameof(PlayerProfile.lobbyLoadoutHintVersion)] = 0;
            expected[nameof(PlayerProfile.lobbyKeyringHintVersion)] = 0;
            expected[nameof(PlayerProfile.gimmickRevealHintVersion)] = 0;

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed);
            Assert.IsTrue(JToken.DeepEquals(expected, JObject.Parse(result)),
                "Tutorial reset must change only the tutorial version tokens.");
        }

        // outgame-tutorial unit 0 — ProfileStore.ResetTutorialProgressAt gates the
        // backup and the file replacement on `changed`. A lobby token left out of that
        // expression would be written to the JObject and then silently dropped whenever
        // it is the only non-zero token, so RESET TUTORIAL would log "already reset"
        // while the disk still blocks the lobby chapters.
        [Test]
        public void ResetAllInJson_ReportsChanged_WhenOnlyLobbyTokensAreSet()
        {
            const string source = @"{
                'firstBattleTutorialVersion': 0,
                'awakeningHintVersion': 0,
                'giftTutorialVersion': 0,
                'lobbyIntroVersion': 1,
                'lobbyLoadoutHintVersion': 1
            }";

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed, "lobby-only progress must still count as a change");
            var stored = JObject.Parse(result);
            Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyIntroVersion)));
            Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyLoadoutHintVersion)));
        }

        // first-session-tutorial unit 17 — 위와 같은 함정을 신규 토큰에도 건다. 탭 즉발 안내만
        // 완료된 계정(= 항아리를 한 번도 안 써본 플레이어)이 유일한 차이일 때, 이 토큰이
        // `changed` 표현식에서 빠져 있으면 RESET TUTORIAL 이 "이미 초기화됨" 이라고 로그하고
        // 디스크는 그대로 남아 탭 안내가 다시는 안 뜬다.
        [Test]
        public void ResetAllInJson_ReportsChanged_WhenOnlyTapAttachTokenIsSet()
        {
            const string source = @"{
                'firstBattleTutorialVersion': 0,
                'awakeningHintVersion': 0,
                'awakeningTapAttachHintVersion': 1,
                'giftTutorialVersion': 0,
                'lobbyIntroVersion': 0,
                'lobbyLoadoutHintVersion': 0
            }";

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed, "탭 즉발 안내만 완료된 상태도 초기화 대상이다");
            Assert.AreEqual(0, JObject.Parse(result)
                .Value<int>(nameof(PlayerProfile.awakeningTapAttachHintVersion)));
        }

        // unit 17 — 두 부착 안내는 서로를 소비하지 않는다. 이 테스트가 깨지면 항아리로 먼저
        // 열어본 플레이어가 탭 즉발을 영영 못 배우는 원래 버그로 되돌아간 것이다.
        [Test]
        public void AttachHints_AreIndependentPerPath()
        {
            var profile = new PlayerProfile();
            Assert.IsTrue(TutorialProgress.IsDragAttachHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsTapAttachHintPending(profile));

            Assert.IsTrue(TutorialProgress.CompleteDragAttachHint(profile));
            Assert.IsFalse(TutorialProgress.IsDragAttachHintPending(profile));
            Assert.IsTrue(TutorialProgress.IsTapAttachHintPending(profile),
                "드래그 안내를 봤다고 탭 즉발 안내가 소비되면 안 된다");

            Assert.IsTrue(TutorialProgress.CompleteTapAttachHint(profile));
            Assert.IsFalse(TutorialProgress.IsTapAttachHintPending(profile));
            // 멱등
            Assert.IsFalse(TutorialProgress.CompleteTapAttachHint(profile));
            Assert.IsFalse(TutorialProgress.CompleteTapAttachHint(null));
        }

        // unit 17 — 인트로(0·A단계)는 파생이다. `||` 로 쓰면 한쪽만 쓰는 플레이어에게 영원히
        // 떠서 잔소리가 된다 — 하나라도 배우면 "덱 여는 법"은 이해한 것이므로 끝나야 한다.
        [Test]
        public void AwakeningIntro_StopsAfterEitherPathIsLearned()
        {
            _holder.SetLoadedProfile(new PlayerProfile());
            Assert.IsTrue(TutorialProgress.ShouldRunAwakeningIntro(_holder), "둘 다 pending 이면 인트로가 뜬다");

            TutorialProgress.CompleteDragAttachHint(_holder.profile);
            Assert.IsFalse(TutorialProgress.ShouldRunAwakeningIntro(_holder),
                "한쪽을 배우면 인트로는 끝난다(&& 이지 || 가 아니다)");
            Assert.IsTrue(TutorialProgress.ShouldRunTapAttachHint(_holder),
                "인트로가 끝나도 못 배운 경로의 안내는 살아 있어야 한다");
        }

        // 반대 순서도 대칭이어야 한다.
        [Test]
        public void AwakeningIntro_StopsAfterTapPathToo()
        {
            _holder.SetLoadedProfile(new PlayerProfile());

            TutorialProgress.CompleteTapAttachHint(_holder.profile);
            Assert.IsFalse(TutorialProgress.ShouldRunAwakeningIntro(_holder));
            Assert.IsTrue(TutorialProgress.ShouldRunDragAttachHint(_holder));
        }

        // outgame-tutorial unit 6 — 챕터 C 는 챕터 B 완료를 전제로 한다. B 를 전제로 걸어야
        // A·B·C 가 동시에 pending 될 수 없고, A → B → C 순서에 별도 상태가 필요 없다.
        [Test]
        public void LobbyKeyringHint_RunsOnlyAfterLoadoutHintComplete()
        {
            var profile = new PlayerProfile();
            _holder.SetLoadedProfile(profile);

            // 첫 진입: A 만 pending. C 는 B 를 기다린다.
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyIntro(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyKeyringHint(_holder));

            // A 완료 + core 완료 → B 차례. 여기서도 C 는 뜨지 않는다.
            TutorialProgress.CompleteLobbyIntro(profile);
            TutorialProgress.CompleteCore(profile);
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyKeyringHint(_holder));

            // B 완료 → C 가 정확히 여기서 뜬다.
            TutorialProgress.CompleteLobbyLoadoutHint(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyLoadoutHint(_holder));
            Assert.IsTrue(TutorialProgress.ShouldRunLobbyKeyringHint(_holder));

            TutorialProgress.CompleteLobbyKeyringHint(profile);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyKeyringHint(_holder));

            // 세션 가드와 null 프로필도 A·B 와 같게 막는다.
            _holder.SetLoadedProfile(null);
            Assert.IsFalse(TutorialProgress.ShouldRunLobbyKeyringHint(_holder));
        }

        [Test]
        public void LobbyKeyringCompletion_IsIdempotentAndTouchesNothingElse()
        {
            var profile = new PlayerProfile();

            Assert.IsTrue(TutorialProgress.CompleteLobbyKeyringHint(profile));
            Assert.IsFalse(TutorialProgress.IsLobbyKeyringHintPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteLobbyKeyringHint(profile), "멱등");
            Assert.IsFalse(TutorialProgress.CompleteLobbyKeyringHint(null));

            // C 완료가 A·B 를 소비하면 안 된다 — 챕터별로 독립된 토큰이다.
            Assert.IsTrue(TutorialProgress.IsLobbyIntroPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyLoadoutHintPending(profile));

            // 미래 버전으로 앞서 있는 프로필도 pending 이 아니다.
            var ahead = new PlayerProfile
            {
                lobbyKeyringHintVersion = TutorialProgress.LobbyKeyringHintVersion + 1,
            };
            Assert.IsFalse(TutorialProgress.IsLobbyKeyringHintPending(ahead));
        }

        // unit 6 — 신규 토큰은 ResetAll 의 `changed` 표현식에도 들어가야 한다. 빠지면 이
        // 토큰만 1 인 프로필에서 ResetTutorialProgressAt 의 memoryChanged 가 false 가 되고,
        // 디스크 쪽도 같은 누락이면 "이미 초기화됨" 이라 로그하며 리셋이 사라진다.
        [Test]
        public void ResetAll_ReportsChanged_WhenOnlyKeyringTokenIsSet()
        {
            var profile = new PlayerProfile
            {
                selectedSquadId = "keep_squad",
                lobbyKeyringHintVersion = TutorialProgress.LobbyKeyringHintVersion,
            };

            Assert.IsTrue(TutorialProgress.ResetAll(profile),
                "키링 안내만 완료된 상태도 초기화 대상이다");
            Assert.AreEqual(0, profile.lobbyKeyringHintVersion);
            Assert.AreEqual("keep_squad", profile.selectedSquadId);
            Assert.IsFalse(TutorialProgress.ResetAll(profile), "멱등");
        }

        // unit 6 — 디스크 쪽 같은 함정(unit 17 교훈). 이 토큰이 유일한 차이일 때
        // `changed` 가 false 면 ResetTutorialProgressAt 이 백업·파일 교체를 건너뛰어
        // 리셋이 디스크에 영영 안 닿는다. 미지 필드 보존도 같은 자리에서 고정한다.
        [Test]
        public void ResetAllInJson_ReportsChanged_WhenOnlyKeyringTokenIsSet()
        {
            const string source = @"{
                'firstBattleTutorialVersion': 0,
                'awakeningHintVersion': 0,
                'awakeningTapAttachHintVersion': 0,
                'giftTutorialVersion': 0,
                'lobbyIntroVersion': 0,
                'lobbyLoadoutHintVersion': 0,
                'lobbyKeyringHintVersion': 1,
                'selectedSquadId': 'keep_squad',
                'futureAccountData': { 'currency': 12345 }
            }";

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed, "키링 안내만 완료된 상태도 초기화 대상이다");
            var stored = JObject.Parse(result);
            Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyKeyringHintVersion)));
            Assert.AreEqual("keep_squad", stored.Value<string>(nameof(PlayerProfile.selectedSquadId)));
            Assert.AreEqual(12345, stored["futureAccountData"]?["currency"]?.Value<int>());
        }

        // ── unit 23 — 기믹 리빌 홀드 안내 ───────────────────────────────────

        // 이 게이트는 형제와 달리 **아무것도 체인하지 않는다**. `!IsCorePending` 을 무는 형제
        // 게이트(선물·로비 B)는 선행 안내가 fail-open 경로를 타면 뒤 안내가 영영 발화하지
        // 못하는 결함을 갖고 있다. 여기선 리빌 자체가 첫 판에 생략되므로(GimmickPhaseView 가
        // ShouldRunCore 로 판정) 안내를 걸 홀드가 애초에 없어 체인이 필요 없다.
        [Test]
        public void GimmickRevealHint_GateChainsNothingButKeepsSessionGuard()
        {
            // core·선물이 모두 pending 인 계정(= 아직 첫 판도 안 한 계정)이라도 게이트 자체는 열려 있다.
            _holder.SetLoadedProfile(new PlayerProfile());
            Assert.IsTrue(TutorialProgress.ShouldRunGimmickRevealHint(_holder));

            // 선물을 건너뛴 계정(TestMode fast-forward 등)도 막히지 않는다 — 체인이 없다는 뜻.
            var skippedGift = new PlayerProfile
            {
                firstBattleTutorialVersion = TutorialProgress.CoreVersion,
            };
            _holder.SetLoadedProfile(skippedGift);
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(skippedGift), "선물은 여전히 미완료");
            Assert.IsTrue(TutorialProgress.ShouldRunGimmickRevealHint(_holder),
                "선물 미완료가 리빌 안내를 막으면 안 된다(챕터 B 결함 재현)");

            // 세션 가드와 null 프로필은 형제와 같게 막는다.
            _holder.SetLoadedProfile(null);
            Assert.IsFalse(TutorialProgress.ShouldRunGimmickRevealHint(_holder));
            Assert.IsFalse(TutorialProgress.ShouldRunGimmickRevealHint(null));
        }

        [Test]
        public void GimmickRevealHintCompletion_IsIdempotentAndTouchesNothingElse()
        {
            var profile = new PlayerProfile();

            Assert.IsTrue(TutorialProgress.CompleteGimmickRevealHint(profile));
            Assert.IsFalse(TutorialProgress.IsGimmickRevealHintPending(profile));
            Assert.IsFalse(TutorialProgress.CompleteGimmickRevealHint(profile), "멱등");
            Assert.IsFalse(TutorialProgress.CompleteGimmickRevealHint(null));

            // 리빌 완료가 다른 안내를 소비하면 안 된다.
            Assert.IsTrue(TutorialProgress.IsCorePending(profile));
            Assert.IsTrue(TutorialProgress.IsGiftTutorialPending(profile));
            Assert.IsTrue(TutorialProgress.IsLobbyKeyringHintPending(profile));

            var ahead = new PlayerProfile
            {
                gimmickRevealHintVersion = TutorialProgress.GimmickRevealHintVersion + 1,
            };
            Assert.IsFalse(TutorialProgress.IsGimmickRevealHintPending(ahead));
        }

        // unit 17·outgame unit 6 과 같은 함정. 이 토큰이 `changed` 표현식에서 빠지면
        // 리빌 안내만 완료된 계정에서 RESET TUTORIAL 이 "이미 초기화됨" 으로 빠져나간다.
        [Test]
        public void ResetAll_ReportsChanged_WhenOnlyGimmickRevealTokenIsSet()
        {
            var profile = new PlayerProfile
            {
                selectedSquadId = "keep_squad",
                gimmickRevealHintVersion = TutorialProgress.GimmickRevealHintVersion,
            };

            Assert.IsTrue(TutorialProgress.ResetAll(profile),
                "리빌 안내만 완료된 상태도 초기화 대상이다");
            Assert.AreEqual(0, profile.gimmickRevealHintVersion);
            Assert.AreEqual("keep_squad", profile.selectedSquadId);
            Assert.IsFalse(TutorialProgress.ResetAll(profile), "멱등");
        }

        // 디스크 쪽 같은 함정. `changed` 가 false 면 ResetTutorialProgressAt 이 백업·파일
        // 교체를 통째로 건너뛰어 리셋이 디스크에 영영 안 닿는다.
        [Test]
        public void ResetAllInJson_ReportsChanged_WhenOnlyGimmickRevealTokenIsSet()
        {
            const string source = @"{
                'firstBattleTutorialVersion': 0,
                'awakeningHintVersion': 0,
                'awakeningTapAttachHintVersion': 0,
                'giftTutorialVersion': 0,
                'lobbyIntroVersion': 0,
                'lobbyLoadoutHintVersion': 0,
                'lobbyKeyringHintVersion': 0,
                'gimmickRevealHintVersion': 1,
                'selectedSquadId': 'keep_squad',
                'futureAccountData': { 'currency': 12345 }
            }";

            string result = TutorialProgress.ResetAllInJson(source, out bool changed);

            Assert.IsTrue(changed, "리빌 안내만 완료된 상태도 초기화 대상이다");
            var stored = JObject.Parse(result);
            Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.gimmickRevealHintVersion)));
            Assert.AreEqual("keep_squad", stored.Value<string>(nameof(PlayerProfile.selectedSquadId)));
            Assert.AreEqual(12345, stored["futureAccountData"]?["currency"]?.Value<int>());
        }

        [Test]
        public void StoredReset_PatchesOnlyTutorialDataAndSynchronizesLoadedProfile()
        {
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                $"wassup-tutorial-reset-{System.Guid.NewGuid():N}.json");
            string backupPath = null;
            const string source = @"{
                'firstBattleTutorialVersion': 1,
                'awakeningHintVersion': 1,
                'giftTutorialVersion': 1,
                'lobbyIntroVersion': 1,
                'lobbyLoadoutHintVersion': 1,
                'selectedSquadId': 'keep_squad',
                'futureAccountData': { 'currency': 12345 }
            }";
            var loaded = new PlayerProfile
            {
                firstBattleTutorialVersion = 1,
                awakeningHintVersion = 1,
                giftTutorialVersion = 1,
                lobbyIntroVersion = 1,
                lobbyLoadoutHintVersion = 1,
                selectedSquadId = "keep_squad",
            };

            try
            {
                File.WriteAllText(path, source);

                bool changed = ProfileStore.ResetTutorialProgressAt(path, loaded, out backupPath);

                Assert.IsTrue(changed);
                var stored = JObject.Parse(File.ReadAllText(path));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.firstBattleTutorialVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.awakeningHintVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.giftTutorialVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyIntroVersion)));
                Assert.AreEqual(0, stored.Value<int>(nameof(PlayerProfile.lobbyLoadoutHintVersion)));
                Assert.AreEqual("keep_squad", stored.Value<string>(nameof(PlayerProfile.selectedSquadId)));
                Assert.AreEqual(12345, stored["futureAccountData"]?["currency"]?.Value<int>());
                Assert.AreEqual(0, loaded.firstBattleTutorialVersion);
                Assert.AreEqual(0, loaded.awakeningHintVersion);
                Assert.AreEqual(0, loaded.giftTutorialVersion);
                Assert.AreEqual(0, loaded.lobbyIntroVersion);
                Assert.AreEqual(0, loaded.lobbyLoadoutHintVersion);
                Assert.AreEqual("keep_squad", loaded.selectedSquadId);
                Assert.IsTrue(File.Exists(backupPath));
                Assert.AreEqual(source, File.ReadAllText(backupPath));
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                if (!string.IsNullOrEmpty(backupPath) && File.Exists(backupPath)) File.Delete(backupPath);
            }
        }
    }
}
