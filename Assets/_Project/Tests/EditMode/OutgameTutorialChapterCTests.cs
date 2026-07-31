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

        private void EnterKeyringFocus()
        {
            FieldInfo stepField = Field("_step");
            stepField.SetValue(_controller, Enum.Parse(stepField.FieldType, "KeyringFocus"));
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
