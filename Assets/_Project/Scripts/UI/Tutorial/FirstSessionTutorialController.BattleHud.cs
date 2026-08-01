using System;
using System.Collections;
using UnityEngine;
using Wassup.Core.TimeControl;
using Wassup.UI;

namespace Wassup.UI.Tutorial
{
    // 첫 판 전투 HUD 안내 체인 (spec units 19·20·21).
    //
    // 각성이 봉인된 첫 판 전투는 안내가 하나도 없는 구간이다. 그 자리에서 스트레스 배지의
    // 의미·패배 조건(정지 + 탭)과 다음 웨이브의 존재(비차단)를 알린다. 첫 판에 플레이어가
    // 관리할 자원은 스트레스 하나뿐이다.
    //
    // 본체에서 분리한 이유는 BattleBridge.BossLeap.cs 와 같다 — 공유 파일에는 lifecycle
    // 호출만 남기고 상태·SerializeField·코루틴은 여기 둔다.
    public sealed partial class FirstSessionTutorialController
    {
        // unit 19 — 사용자 작성 문구. 임의로 고치지 말 것.
        // 한계 수치는 화면 배지와 같은 소스에서 채운다(하드코딩 금지 — 덱마다 다르다).
        // unit 21 — 두 줄을 **한 문단 1탭**으로 낸다(사용자 결정 2026-08-01). 클래스 안내(6줄
        // 한 문단)와 같은 리듬이고 첫 판의 강제 탭 수가 늘지 않는다.
        private const string HudHintStressText = "악몽을 막아 스트레스 관리하세요!";
        // 조사는 사용자 원문의 `이` 를 쓴다(원문: `스트레스가 10이되면 패배합니다.`). 한국어
        // 이/가 는 수치의 읽음에 따라 갈리므로(10=십 → 이, 5=오 → 가) 데이터로 바뀌면 어색해질
        // 수 있다. 자릿수별 조사 계산은 데모 범위에서 과잉이라 현 튜닝(한계 10)에 맞춘다.
        private const string HudHintStressLimitFormat = "스트레스가 {0}이 되면 패배합니다.";

        // unit 20 — 사용자 작성 문구. 임의로 고치지 말 것. 클릭을 요구하지 않는다:
        // 둘째 줄이 "지금 누르지 마"라는 뜻이라 행동 성공 신호로 진행시키면 문구와 모순이고,
        // 첫 판에 웨이브를 겹치면 안내가 스트레스 한계 = 패배를 유도한다.
        private const string HudHintWaveText = "더 높은 점수를 위해 다음 웨이브 호출해보세요";
        private const string HudHintWaveCaveatText = "단, 준비가 되었을때!";

        [Header("Battle HUD hint (unit 19)")]
        [SerializeField] private ScoreHudView scoreHud;
        // unit 20 — 좌하단 `다음 웨이브` 버튼. 레거시 덱(생성 웨이브 아님) 경로에는 버튼이
        // 아예 없으므로 미배선·부재 모두 그 스텝만 생략한다.
        [SerializeField] private NextWaveDock waveDock;

        // unit 19 — 체인 전용 핸들. **`_awakeningRoutine` 을 재사용하지 말 것** — 그 핸들은
        // 각성 0·A·B 단계가 공유하고 ResetAwakeningSession·OnCardPeeked 가 임의로 중단시킨다.
        // 한 핸들에 두 체인을 얹으면 누가 누구를 걷는지 추적이 불가능해진다.
        private Coroutine _hudHintRoutine;
        // 체인이 되돌릴 상태(말풍선 · 앵커 · 정지 lease)를 들고 있는가. 정리 함수가 체인이
        // 없을 때도 불리므로(Placement 진입) 이 가드가 없으면 core 안내가 막 세운 말풍선을
        // 걷어버린다.
        private bool _hudHintActive;
        private bool _hudHintShownThisBattle;
        // unit 21 — 스트레스 안내 동안 전투를 정지시키는 Battle 도메인 lease. **필드 하나가
        // 소유하고 StopBattleHudHint 만 해제한다** — 누수되면 ResetAll(매치 경계)까지 그 판이
        // 영구 정지한다. 중복 획득 금지(획득 전에 기존 lease 를 먼저 해제).
        private TimeLease? _stressHintPause;
        // 탭 소비자가 둘(클래스 안내 · 이 스텝)이라 대기 여부를 명시적으로 든다.
        private bool _stressHintWaitingTap;
        private bool _stressHintTapped;

        // ── lifecycle (본체가 부른다) ────────────────────────────────────────

        private void ResetBattleHudLatches() => _hudHintShownThisBattle = false;

        private void StartBattleHudHint()
        {
            // 게이트는 `_awakeningLockedThisMatch`(= 첫 판) 하나다 — 첫 판 Battle 진입은 계정당
            // 한 번이므로 신규 프로필 버전 토큰을 두지 않는다. **`IsCorePending` 으로 판정하면
            // 안 된다** — 호출 직전 CompleteCoreProgress() 가 이미 소비해서 그 시점엔 false 다.
            if (!_awakeningLockedThisMatch || _hudHintShownThisBattle) return;
            if (guidance == null) return;

            _hudHintShownThisBattle = true;
            _hudHintActive = true;
            // 기믹 배너를 억제하지 않는다. GimmickGuideView.RefreshVisibility 의 표시 조건이
            // `_phase == Placement` 라, Battle 에서만 도는 이 체인에는 억제할 대상이 애초에 없다
            // (core 안내는 Placement 라 계속 억제가 필요하다 — OnPlacementReady 쪽은 그대로).
            if (_hudHintRoutine != null) StopCoroutine(_hudHintRoutine);
            _hudHintRoutine = StartCoroutine(BattleHudHintRoutine());
        }

        // 정리 단일 창구: 코루틴 중단 + 정지 해제 + 탭 캐처 해제 + 말풍선·앵커 원복.
        // 호출처 3곳 — OnPhaseChanged 의 비-Battle 경로 · OnDisable · 체인 정상 종료.
        //
        // `_hudHintActive` 가드가 본체다(위 필드 주석 참조).
        private void StopBattleHudHint()
        {
            if (!_hudHintActive) return;
            _hudHintActive = false;
            if (_hudHintRoutine != null)
            {
                StopCoroutine(_hudHintRoutine);
                _hudHintRoutine = null;
            }
            // unit 21 — 정지 해제는 **여기 하나**가 소유한다. 누수되면 ResetAll(매치 경계)까지
            // 그 판이 얼어붙으므로, 이탈 경로를 늘릴 때 이 함수를 타는지 반드시 확인할 것.
            _stressHintWaitingTap = false;
            _stressHintTapped = false;
            _stressHintPause?.Dispose(); // 멱등 — 이중 Dispose·복사본 모두 안전(TimeLease 계약)
            _stressHintPause = null;
            // Hide() 도 캐처를 끄지만 명시적으로 끈다 — 잔류하면 화면 전체가 먹통이다.
            guidance?.SetTapToContinue(false);
            guidance?.Hide();
            // 앵커를 되돌리지 않으면 다음 안내(각성 · 선물)가 배지 아래 엉뚱한 위치에 뜬다.
            guidance?.SetMessageAnchor(TutorialGuidanceView.MessageAnchor.Default);
        }

        // unit 21 — 정지 안내가 대기 중이면 탭은 이 쪽 것이다. 본체의 OnContinueTapped 가
        // 클래스 안내보다 **먼저** 이걸 묻는다(순서를 흐리면 한쪽이 다른 쪽 탭을 삼킨다).
        private bool TryConsumeStressHintTap()
        {
            if (!_stressHintWaitingTap) return false;
            _stressHintTapped = true;
            return true;
        }

        // ── 체인 ─────────────────────────────────────────────────────────────

        private IEnumerator BattleHudHintRoutine()
        {
            yield return StressHintSteps();
            yield return NextWaveHintSteps();
            _hudHintRoutine = null;
            StopBattleHudHint();
        }

        private IEnumerator StressHintSteps()
        {
            yield return WaitForHintTarget(ResolveStressBadgeRect);
            RectTransform badge = ResolveStressBadgeRect();
            if (badge == null || !badge.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[FirstSessionTutorial] 스트레스 배지 미배선/비활성 — 스트레스 안내를 생략합니다.", this);
                yield break;
            }

            // unit 21 — 시작 직후 바로 멈추면 "시작을 눌렀는데 아무 일도 안 일어난" 것처럼 읽힌다.
            yield return WaitUnscaled(guidance.StressHintDelaySeconds);
            if (!_hudHintActive) yield break; // 대기 중 페이즈 이탈

            // 문구는 한 문단이다. 둘째 줄 생략 조건: 엔드리스는 분모를 표기하지 않고 유출로
            // 패배하지도 않는다. `StressLimit > 0` 도 함께 요구한다 — _leakShowLimit 기본값이
            // true 이고 _leakLimit 기본값이 0 이라, 스냅샷(SetLeakStatus)이 아직 안 왔거나
            // ActiveDeck 이 없으면 `스트레스가 0이 되면 패배합니다.` 가 경고 없이 나간다.
            // 안내는 거짓말보다 침묵이 낫다.
            string text = HudHintStressText;
            if (scoreHud.ShowsStressLimit && scoreHud.StressLimit > 0)
                text += "\n" + string.Format(HudHintStressLimitFormat, scoreHud.StressLimit);

            // **전투 정지.** 글로벌 Time.timeScale 이 아니라 Battle 도메인 lease 다 — 시뮬(BattleSimGroup
            // 의 BattleScaledRateManager)·타이머·웨이브 스폰이 함께 멈추고(셋 다 _battleClock 기반),
            // 안내 자신은 unscaled 라 계속 흐른다. 손패 슬로모(0.3x)와 겹쳐도 승자 규칙
            // (priority 동률 → scale asc)상 0 이 이기고, 해제하면 0.3x 로 정확히 복귀한다.
            _stressHintPause?.Dispose(); // 중복 획득 방지 — 소유는 항상 하나
            _stressHintPause = TimeManager.Instance.Request(TimeDomain.Battle, 0f);

            // 말풍선을 배지·링 아래로 내린다. 배지는 우상단 214~278 이고 링은 ~297 까지 온다 —
            // 와이드 화면에서는 수평으로 안 겹치지만 4:3 급에서는 겹친다(TutorialGuidanceStyle 주석).
            // 이 앵커는 체인이 끝날 때까지 유지한다(웨이브 스텝에서 되돌리지 않는다).
            guidance.SetMessageAnchor(TutorialGuidanceView.MessageAnchor.HudHint);
            guidance.ShowMessage(text, showSkip: false);
            guidance.FocusUi(badge);
            guidance.SetTapToContinue(true);

            // 탭 대기. 만료 폴백이 없으면 탭 유실 시 lease 가 남아 **그 판이 영구 정지**한다
            // (ResetAll 안전망은 매치 경계에만 있다). 클래스 안내와 같은 이유의 안전장치다.
            _stressHintTapped = false;
            _stressHintWaitingTap = true;
            float elapsed = 0f;
            float limit = Mathf.Max(0.1f, guidance.StressHintFallbackSeconds);
            while (!_stressHintTapped && elapsed < limit && _hudHintActive)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            _stressHintWaitingTap = false;
            if (!_hudHintActive) yield break; // 정리 경로가 이미 lease·캐처를 걷었다

            guidance.SetTapToContinue(false);
            _stressHintPause?.Dispose();
            _stressHintPause = null;
        }

        // unit 20 — ③④ 다음 웨이브. 점수를 위해 당길 수 있다는 선택지와 그 조건을 알린다.
        // **클릭을 요구하지 않는다**(사용자 결정 2026-08-01) — 문구 자체가 "지금 누르지 마"이고,
        // 첫 판에 웨이브를 겹치게 만들면 안내가 패배를 유도한다.
        private IEnumerator NextWaveHintSteps()
        {
            yield return WaitForHintTarget(ResolveWaveButtonRect);
            // 미배선과 버튼 부재는 다른 사건이다. 앞은 씬 배선 버그(경고), 뒤는 레거시 덱
            // (생성 웨이브 아님)의 **정상** 경로다 — 후자를 경고로 올리면 정상 플레이가 콘솔을
            // 더럽히고 "경고 0" 완료 기준과 모순된다.
            if (waveDock == null)
            {
                Debug.LogWarning("[FirstSessionTutorial] waveDock 미배선 — 웨이브 안내를 생략합니다.", this);
                yield break;
            }
            RectTransform button = ResolveWaveButtonRect();
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                Debug.Log("[FirstSessionTutorial] 다음 웨이브 버튼 부재(생성 웨이브 아님) — 웨이브 안내를 생략합니다.", this);
                yield break;
            }

            // 앵커는 스트레스 스텝이 내려둔 HudHint 를 **그대로 유지한다.** Default(184)로 되돌리면
            // 방금 가르친 스트레스 배지를 이 두 줄이 덮는다(4:3 급 화면). 대상이 좌하단이라
            // 말풍선이 아래에 있어도 지시 관계는 흐려지지 않는다. 원복은 StopBattleHudHint 가 한다.
            guidance.ShowMessage(HudHintWaveText, showSkip: false);
            guidance.FocusUi(button);
            yield return WaitUnscaled(guidance.HudHintLineSeconds);

            guidance.ShowMessage(HudHintWaveCaveatText, showSkip: false);
            yield return WaitUnscaled(guidance.HudHintLineSeconds);
        }

        private RectTransform ResolveStressBadgeRect() =>
            scoreHud != null ? scoreHud.StressBadgeRect : null;

        private RectTransform ResolveWaveButtonRect() =>
            waveDock != null ? waveDock.WaveButtonRect : null;

        // FocusUi 는 대상이 activeInHierarchy 가 아니면 **링을 조용히 끈다**(0단계가 빠졌던
        // 함정). HUD 뷰들은 자기 Update 에서 lazily 구독·활성화되고 PhaseChanged 구독자 순서는
        // 보장되지 않으므로, 한 프레임 양보 후 짧게 폴링한다. 시간이 지나도 비활성이면 호출자가
        // 그 스텝만 생략한다(fail-open — 전투 플레이를 잠그지 않는다).
        // rect 를 값이 아니라 함수로 받는 이유: 대상 자체가 나중에 생성되는 뷰도 있다(unit 20).
        private IEnumerator WaitForHintTarget(Func<RectTransform> resolve)
        {
            yield return null;
            float limit = guidance != null ? guidance.HudHintTargetWaitSeconds : 0f;
            float elapsed = 0f;
            while (elapsed < limit)
            {
                var rect = resolve();
                if (rect != null && rect.gameObject.activeInHierarchy) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }
    }
}
