using TMPro;
using UnityEngine;

namespace Wassup.UI.Tutorial
{
    [CreateAssetMenu(fileName = "TutorialGuidanceStyle", menuName = "Wassup/UI/Tutorial Guidance Style", order = 60)]
    public sealed class TutorialGuidanceStyle : ScriptableObject
    {
        [Header("Typography")]
        public TMP_FontAsset koreanFont;
        public float messageFontSize = 42f;
        public float skipFontSize = 28f;

        [Header("Canvas Layering")]
        public int dimSortingOrder = 1499;
        public int guidanceSortingOrder = 1500;
        public int elevatedSortingOrder = 1501;

        [Header("Message")]
        public Vector2 messageSize = new Vector2(880f, 116f);
        public float messageTopOffset = 184f;
        // unit 19 — 첫 판 전투 HUD 안내 전용 앵커. 스트레스 배지는 **화면 우상단**이고
        // (`ScoreHudView` 패널이 anchor (1,1) · pivot (1,1) · 폭 360 · cornerPadding 36 인셋)
        // 세로 구간은 36+20+148+10 = 214 ~ 278, 포커스 링은 pulseScale 까지 먹여 ~297 까지 내려온다.
        //
        // 말풍선은 top-center 폭 880 이라 **와이드 화면에서는 수평으로 안 겹친다**(1920 기준
        // 말풍선 520~1400 vs 배지 1524~1884). 겹치는 건 4:3 급 좁은 화면(1440 기준 116px)이다.
        // 그래서 이 앵커는 4:3 방어이고, 값은 링 도달선(297)을 넘겨야 의미가 있다.
        [Tooltip("전투 HUD 안내 전용 말풍선 상단 오프셋. 4:3 화면에서 우상단 스트레스 배지·포커스 링(~297)과 겹치지 않게 그 아래로 둔다.")]
        public float hudHintMessageTopOffset = 310f;
        public Color messageFill = new Color(0.025f, 0.045f, 0.09f, 0.96f);
        public Color messageBorder = new Color(1f, 0.78f, 0.24f, 1f);
        public Color messageText = new Color(1f, 0.97f, 0.88f, 1f);

        [Header("Focus")]
        public Color focusColor = new Color(1f, 0.82f, 0.25f, 0.95f);
        public float focusPadding = 14f;
        public float focusBorder = 5f;
        public float pulsePeriod = 0.78f;
        public float pulseScale = 1.10f;
        public float pointerBob = 10f;

        [Header("World Markers")]
        public Color spawnMarkerColor = new Color(1f, 0.34f, 0.24f, 1f);
        public Color goalMarkerColor = new Color(1f, 0.82f, 0.25f, 1f);
        public float worldMarkerFontSize = 24f;
        public float worldMarkerLabelOffset = 76f;
        public float worldMarkerMessageTopOffset = 320f;

        // unit 11 — 읽고 넘기는 스텝의 차단 dim. 완전 투명이면 무반응이 버그로 읽힌다.
        [Range(0f, 1f)] public float tapCatcherDimAlpha = 0.35f;

        [Header("Timing (unscaled)")]
        [Range(4f, 6f)] public float goalBeatSeconds = 5f;
        [Range(3f, 4f)] public float awakeningPromptSeconds = 3.5f;
        public float cardInstructionSeconds = 3f;
        // unit 11 — 탭이 유실되면 Start 잠금이 안 풀려 첫 판이 Skip 외 탈출 불가가 된다.
        [Range(6f, 30f)] public float classHintFallbackSeconds = 12f;
        // unit 19 — 전투 HUD 안내 한 줄의 노출 시간. 이 구간은 비차단이라(전투 중 배치 입력을
        // 막지 않는다) 탭이 아니라 시간 경과가 유일한 진행 신호다.
        [Range(2f, 6f)] public float hudHintLineSeconds = 3f;
        // unit 19 — 포커스 대상(스트레스 배지 · 웨이브 버튼)은 자기 Update 에서 lazily 켜진다.
        // 이 시간만 폴링하고 그래도 비활성이면 그 스텝을 생략한다(fail-open).
        [Range(0f, 3f)] public float hudHintTargetWaitSeconds = 1f;
    }
}
