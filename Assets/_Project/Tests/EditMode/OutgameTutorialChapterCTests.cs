using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Wassup.Core;
using Wassup.UI;

namespace Wassup.Tests.EditMode
{
    // outgame-tutorial unit 6 — 챕터 C 오케스트레이션 회귀. TutorialProgressTests 는 순수
    // 진행 상태 레이어만 보므로 이 두 회귀를 관측할 수 없다(리뷰 지적 M2):
    //   ① KeyringFocus 의 dim 탭이 완료를 저장하면 드래그를 한 번도 안 하고 챕터가 소진된다
    //      (바로 위 LoadoutFocus case 는 실제로 CompleteAndEnd() 를 부른다 — 복붙 사고 지점)
    //   ② CompleteAndEnd 의 챕터 분기가 부족하면 C 가 챕터 B 의 플래그를 다시 쓰고
    //      자기 토큰은 0 으로 남아 영원히 pending 이 된다
    //
    // 저장은 ProfileSaver seam 으로 가로채 개발자의 실제 profile.json 을 건드리지 않는다.
    public class OutgameTutorialChapterCTests
    {
        private GameObject _go;
        private OutgameTutorialController _controller;
        private PlayerProfileSO _profileSO;
        private PlayerProfile _profile;
        private List<PlayerProfile> _saved;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("OutgameTutorialChapterCTest");
            _controller = _go.AddComponent<OutgameTutorialController>();
            _profileSO = ScriptableObject.CreateInstance<PlayerProfileSO>();
            _profile = new PlayerProfile();
            // 챕터 B 까지는 끝난 상태 = C 가 pending 인 상태.
            _profile.firstBattleTutorialVersion = TutorialProgress.CoreVersion;
            _profile.lobbyIntroVersion = TutorialProgress.LobbyIntroVersion;
            _profile.lobbyLoadoutHintVersion = TutorialProgress.LobbyLoadoutHintVersion;
            // unit 12·13 — 기준 상태는 **키링 직전**이다: 스쿼드·덱까지 끝나 있고 키링과
            // 스타트가 pending. 덱을 채워두는 것이 중요하다 — 새 순서에서 덱은 키링보다 먼저
            // 완료되므로(ShouldRunLobbyKeyringHint 의 선행), 덱을 0 으로 두면 `키링 완료 &&
            // 덱 0` 이라는 **레거시 계정 형태**가 되어 스타트 스텝이 파생 가드에 막힌다.
            // 앞 스텝을 검증하는 테스트는 자기 토큰만 0 으로 되돌린다.
            _profile.lobbyDeckHintVersion = TutorialProgress.LobbyDeckHintVersion;
            _profileSO.SetLoadedProfile(_profile);

            _saved = new List<PlayerProfile>();
            _controller.ProfileSaver = p => _saved.Add(p);
            WriteField("profileSO", _profileSO);
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            if (_profileSO != null) UnityEngine.Object.DestroyImmediate(_profileSO);
        }

        [Test]
        public void DimTap_OnKeyringFocus_DoesNotCompleteChapter()
        {
            EnterKeyringFocus();

            Invoke("OnOverlayTapped");

            Assert.IsTrue(TutorialProgress.IsLobbyKeyringHintPending(_profile),
                "KeyringFocus 의 dim 탭은 no-op 이어야 한다 — 완료가 저장되면 드래그를 " +
                "한 번도 안 하고 챕터가 소진된다.");
            Assert.IsEmpty(_saved, "dim 탭으로는 프로필을 저장하지 않는다.");
        }

        [Test]
        public void KeyringDragStarted_CompletesOnlyTheKeyringToken()
        {
            EnterKeyringFocus();

            Invoke("OnKeyringDragStarted");

            Assert.IsFalse(TutorialProgress.IsLobbyKeyringHintPending(_profile),
                "실제 드래그가 챕터 C 를 완료시켜야 한다.");
            Assert.AreEqual(TutorialProgress.LobbyLoadoutHintVersion, _profile.lobbyLoadoutHintVersion,
                "챕터 C 의 완료가 챕터 B 의 플래그를 다시 쓰면 안 된다(CompleteAndEnd 분기 부족).");
            Assert.AreEqual(1, _saved.Count, "완료는 정확히 한 번 저장한다.");
        }

        [Test]
        public void DragStartedAfterChapterEnded_DoesNotSaveAgain()
        {
            EnterKeyringFocus();
            Invoke("OnKeyringDragStarted");
            _saved.Clear();

            // EndChapter 가 _step 을 None 으로 되돌린 뒤 들어온 늦은 신호(낙하 중 재잡기 등).
            Invoke("OnKeyringDragStarted");

            Assert.IsEmpty(_saved, "챕터가 끝난 뒤의 드래그 신호는 아무것도 쓰지 않는다.");
        }

        // ── unit 12: 로드아웃 시퀀스 분리(스쿼드 · 드림캐쳐) ──────────────────
        //
        // 옛 챕터 B(LoadoutFocus)는 **dim 탭으로도 완료**됐다 — "여기 있다"만 알리는 정보
        // 단계였기 때문이다. 두 스텝은 페이지를 실제로 여는 것이 목적이라 무반응이어야 한다.
        // 그 case 를 복붙하면 페이지를 한 번도 안 열고 시퀀스가 통과하는데, 조용히 통과하는
        // 형태라 육안으로는 늦게 발견된다.

        [Test]
        public void DimTap_OnSquadFocus_DoesNotCompleteStep()
        {
            _profile.lobbyLoadoutHintVersion = 0; // 스쿼드 스텝 pending
            EnterStep("SquadFocus");

            Invoke("OnOverlayTapped");

            Assert.IsTrue(TutorialProgress.IsLobbySquadHintPending(_profile),
                "SquadFocus 의 dim 탭은 no-op 이어야 한다 — 완료가 저장되면 스쿼드 페이지를 " +
                "한 번도 안 열고 스텝이 소진된다.");
            Assert.IsEmpty(_saved);
        }

        [Test]
        public void DimTap_OnDeckFocus_DoesNotCompleteStep()
        {
            _profile.lobbyDeckHintVersion = 0; // 덱 스텝 pending
            EnterStep("DeckFocus");

            Invoke("OnOverlayTapped");

            Assert.IsTrue(TutorialProgress.IsLobbyDeckHintPending(_profile),
                "DeckFocus 의 dim 탭도 no-op 이어야 한다.");
            Assert.IsEmpty(_saved);
        }

        [Test]
        public void SquadButtonClick_CompletesOnlyTheSquadToken()
        {
            // 스쿼드가 pending 인 시점은 덱도 pending 이다(체인 순서). 기준 상태는 키링 직전이라
            // 덱이 채워져 있으므로, 이 스텝을 검증하려면 뒤 토큰들도 함께 되돌려야 한다.
            _profile.lobbyLoadoutHintVersion = 0;
            _profile.lobbyDeckHintVersion = 0;
            EnterStep("SquadFocus");

            Invoke("OnFocusedButtonClicked");

            Assert.IsFalse(TutorialProgress.IsLobbySquadHintPending(_profile),
                "실제 클릭이 스쿼드 스텝을 완료시켜야 한다.");
            Assert.IsTrue(TutorialProgress.IsLobbyDeckHintPending(_profile),
                "스쿼드 완료가 다음 스텝의 토큰을 먼저 소비하면 안 된다.");
            Assert.AreEqual(TutorialProgress.LobbyIntroVersion, _profile.lobbyIntroVersion,
                "챕터 A 의 플래그를 다시 쓰면 안 된다.");
            Assert.AreEqual(1, _saved.Count, "완료는 정확히 한 번 저장한다.");
        }

        [Test]
        public void DeckButtonClick_CompletesOnlyTheDeckToken()
        {
            _profile.lobbyDeckHintVersion = 0;
            EnterStep("DeckFocus");

            Invoke("OnFocusedButtonClicked");

            Assert.IsFalse(TutorialProgress.IsLobbyDeckHintPending(_profile),
                "실제 클릭이 드림캐쳐 스텝을 완료시켜야 한다.");
            Assert.IsTrue(TutorialProgress.IsLobbyKeyringHintPending(_profile),
                "덱 완료가 키링 토큰을 소비하면 안 된다.");
            Assert.AreEqual(1, _saved.Count, "완료는 정확히 한 번 저장한다.");
        }

        // ── unit 13: 키링 착지 → 재출발(START) ────────────────────────────────

        // 잡는 순간 dim 은 걷히지만 시퀀스는 끝나지 않는다. 여기서 EndChapter 로 _step 을
        // None 에 둔 채로 남기면 착지 폴링이 영영 돌지 않아 마지막 스텝이 사라진다.
        [Test]
        public void KeyringDragStarted_EntersSettlingWithStartStepStillPending()
        {
            EnterKeyringFocus();

            Invoke("OnKeyringDragStarted");

            Assert.AreEqual("KeyringSettling", CurrentStepName(),
                "드래그 시작은 챕터를 끝내는 게 아니라 착지 대기로 넘어간다.");
            Assert.IsFalse(TutorialProgress.IsLobbyKeyringHintPending(_profile));
            Assert.IsTrue(TutorialProgress.IsLobbyStartHintPending(_profile),
                "재출발 안내는 아직 pending 이어야 한다 — 착지 뒤에 뜬다.");
            Assert.AreEqual(1, _saved.Count, "키링 완료만 저장한다.");
        }

        [Test]
        public void DimTap_OnStartFocus_DoesNotCompleteStep()
        {
            _profile.lobbyKeyringHintVersion = TutorialProgress.LobbyKeyringHintVersion;
            EnterStep("StartFocus");

            Invoke("OnOverlayTapped");

            Assert.IsTrue(TutorialProgress.IsLobbyStartHintPending(_profile),
                "StartFocus 의 dim 탭은 no-op 이어야 한다 — START 를 직접 눌러야 끝난다.");
            Assert.IsEmpty(_saved);
        }

        // 챕터 A 의 IntroFocus 가 같은 startButton 을 쓴다. CompleteAndEnd 에 StartFocus
        // case 가 없으면 이 스텝이 A 의 플래그를 다시 쓰고 자기 토큰은 0 으로 남는다.
        // **"lobbyIntroVersion 값이 안 변했다" 로는 그 결함을 못 잡는다** — 하네스가 이미 1 을
        // 넣어두므로 CompleteLobbyIntro 가 멱등 return false 로 값을 안 바꾼다. 저장 호출 수와
        // 자기 토큰 값으로 본다.
        [Test]
        public void StartButtonClick_CompletesOnlyTheStartToken()
        {
            _profile.lobbyKeyringHintVersion = TutorialProgress.LobbyKeyringHintVersion;
            EnterStep("StartFocus");

            Invoke("OnFocusedButtonClicked");

            Assert.AreEqual(TutorialProgress.LobbyStartHintVersion, _profile.lobbyStartHintVersion,
                "재출발 스텝은 자기 토큰을 채워야 한다(분기 누락 시 0 으로 남는다).");
            Assert.AreEqual(1, _saved.Count, "완료는 정확히 한 번 저장한다.");
        }

        private string CurrentStepName() => Field("_step").GetValue(_controller).ToString();

        private void EnterKeyringFocus() => EnterStep("KeyringFocus");

        private void EnterStep(string stepName)
        {
            FieldInfo stepField = Field("_step");
            stepField.SetValue(_controller, Enum.Parse(stepField.FieldType, stepName));
            // minStepSeconds 게이트를 지나도록 진입 시각을 과거로 둔다.
            Field("_stepEnteredAt").SetValue(_controller, -100f);
        }

        private void WriteField(string name, object value) => Field(name).SetValue(_controller, value);

        private static FieldInfo Field(string name)
        {
            FieldInfo field = typeof(OutgameTutorialController).GetField(name,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(field, $"필드 '{name}' 이 사라졌다 — 테스트가 가리키는 이름을 갱신할 것.");
            return field;
        }

        private void Invoke(string method)
        {
            MethodInfo info = typeof(OutgameTutorialController).GetMethod(method,
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(info, $"메서드 '{method}' 이 사라졌다 — 테스트가 가리키는 이름을 갱신할 것.");
            info.Invoke(_controller, null);
        }
    }
}
