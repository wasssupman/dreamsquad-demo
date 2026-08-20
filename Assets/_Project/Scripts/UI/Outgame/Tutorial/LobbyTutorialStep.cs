using UnityEngine;
using Wassup.Core;
using Wassup.Data;
using Wassup.UI.Tutorial;

namespace Wassup.UI
{
    // first-run-tutorial unit 2 — 로비 강제 포커스(L). 스텝 하나짜리다.
    //
    // 옛 OutgameTutorialController(챕터 A~D 8스텝)를 되살리지 않는다 — 지금 필요한 것은
    // 문구 하나 + 구멍 하나이고, 그 이상은 재설계가 요구하지 않았다.
    //
    // 이 컴포넌트는 아무것도 대신 눌러주지 않는다. START 는 플레이어가 실제로 눌러야 하고,
    // 구멍 밖 탭은 무시된다(overlay.Tapped 를 구독하지 않는 이유 — 탭으로 넘어가는 스텝이 아니다).
    public sealed class LobbyTutorialStep : MonoBehaviour
    {
        private const string IntroText = "누가 더 많은 악몽을 제거 하는지 시작해 보시죠";

        // unit 10 — 온보딩 판을 마치고 돌아온 로비의 배웅. 구조는 인트로와 **완전히 같다**
        // (딤 + START 구멍 + 문구 하나) — 갈리는 것은 문구와 기록 필드뿐이라 스텝을 새로
        // 만들지 않는다.
        private const string OutroText = "이제 진짜 승부를 시작해 보시죠";

        [Tooltip("딤 + 구멍. Guidance 와 **다른 GameObject** 여야 한다 — 둘 다 자기 캔버스를 만들어 sortingOrder 를 다툰다.")]
        [SerializeField] private OutgameTutorialOverlay overlay;
        [SerializeField] private TutorialGuidanceView guidance;

        private bool _shown;

        // 호출 지점이 계약의 일부다: OutgameMenuController 의 Awake 말미(프로필 로드 이후)와
        // ApplyAuthGate(로그인 직후) 양쪽. 로그인 전에는 뜨지 않는다.
        //
        // loadoutReady 가 이 게이트의 두 번째 절반이다 — 아래 주석 참조.
        public void TryShow(PlayerProfileSO profileSO, RectTransform startButton, bool loadoutReady)
        {
            if (_shown) return;
            if (profileSO == null || !profileSO.IsLoadedThisSession) return;
            var profile = profileSO.profile;
            if (profile == null) return;

            // unit 10 — 두 모드. 인트로 = «아직 안 봤다», 아웃트로 = «배틀은 끝냈는데 배웅을
            // 아직 안 봤다». 둘 다 아니면 뜨지 않는다.
            //
            // ⚠ 아웃트로 시점에는 ShouldRun 이 **이미 거짓**이다 — 다음 판은 온보딩 웨이브도
            // 아니고 토너먼트 제출도 정상으로 돈다(계약 16 의 셋이 전부 꺼진 상태). 문구가
            // 「이제 진짜 승부」라고 말하는 것이 사실과 맞는다.
            bool intro = FirstRunTutorialConfig.ShouldRun(profile);
            bool outro = !intro && !profile.firstRunLobbyOutroDone;
            if (!intro && !outro) return;

            // ⚠ 로드아웃이 모자란 계정에는 딤을 띄우지 않는다.
            // START 는 게이트 미충족 시 LoadoutGatePopup 을 띄우고 돌아가는데, **그 팝업은 자체
            // 캔버스가 없어** 로비 루트 캔버스(order 0)에 형제로 붙는다 → 딤(1499) 아래로 깔린다.
            // 그러면 START 를 눌러도 화면이 그대로이고 다른 버튼은 전부 막혀 빠져나갈 길이 없다.
            // 신규 계정은 ProfileStore 시드로 통과하므로 정상 경로에는 영향이 없다 — 위험한 것은
            // RESET 재실행 계정과 id 리네임으로 저장 덱 Validate 가 깨진 계정이다.
            if (!loadoutReady) return;

            if (overlay == null || guidance == null || startButton == null)
            {
                Debug.LogWarning("[LobbyTutorial] overlay/guidance/START 미배선 — 안내를 생략한다.", this);
                return;
            }

            _shown = true;
            overlay.SetSortingOrder(guidance.DimSortingOrder);
            overlay.SetHoles(new[] { startButton });
            overlay.Show();
            guidance.ShowMessage(intro ? IntroText : OutroText, false);
            guidance.FocusUi(startButton);

            // 아웃트로는 **띄운 시점에** 기록한다(계약 18). 배틀 구간처럼 「완주해야 기록」이
            // 아닌 이유: 이 스텝에서 플레이어가 해낼 행동이 START 하나뿐이고 그건 스텝의
            // 완료 조건이 아니라 로비를 떠나는 동작이다 — 완주를 기다리면 기록 시점이 없다.
            if (outro)
            {
                profile.firstRunLobbyOutroDone = true;
                ProfileStore.Save(profile);
            }
        }

        // START 로 씬이 바뀌면 오브젝트째 사라지므로 별도 정리가 필요 없다.
        // 다만 로그아웃처럼 같은 씬에서 상태가 뒤집히는 경로가 있어 명시적 해제를 남긴다.
        public void HideIfShown()
        {
            if (!_shown) return;
            _shown = false;
            if (overlay != null) overlay.Hide();
            if (guidance != null) { guidance.ClearFocus(); guidance.Hide(); }
        }
    }
}
