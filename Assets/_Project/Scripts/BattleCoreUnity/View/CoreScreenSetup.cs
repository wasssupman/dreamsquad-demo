using UnityEngine;

namespace Wassup.BattleCoreUnity.View
{
    // battle-core-rebuild unit 8b — **전투 씬 화면 초기화**(rule-holders G17 의 씬 몫 · G18). 옛 `GameManager.Awake`(세로 1080 캡)와
    // `GameManager.Start → CalibrateDragThreshold`(탭/드래그 임계 DPI)를 옮겼다. 규칙은 없다 — 화면·입력 장치의 사정이다.
    //
    // G20(「씬이 꺼지면 전투를 멈추고 기록 세션을 닫는다」)의 새 자리는 여기가 아니다: 판의 수명이 드라이버의 수명이라
    // 씬이 내려가면 틱 발행(`BattleDriver.Update`)이 스스로 멎고 스테이지는 `OnDestroy` 가 치운다. 닫을 기록 세션(JSON 로그)은
    // 새 씬에 없다(8b 「배틀 JSON 로그 파일」 판정).
    [DisallowMultipleComponent]
    public sealed class CoreScreenSetup : MonoBehaviour
    {
        // 옛 값 그대로 — 세로 캡(네이티브보다 위로 올리지 않는다) · 임계 = DPI/6 ≈ 4mm.
        private const int MaxHeight = 1080;
        private const float DpiDivisor = 6f;

        private void Awake()
        {
            // 비율 고정 + 영역만 확장: 세로를 1080 으로 캡하고 가로는 물리 화면 aspect 로 계산한다.
            // 과거 SetResolution(1920,1080,true) 은 기기 aspect(20:9 등)와 달라 모든 오브젝트가 가로로 찌그러졌다.
            int sysW = Display.main.systemWidth;
            int sysH = Display.main.systemHeight;
            if (sysW > 0 && sysH > 0)
            {
                int targetH = Mathf.Min(MaxHeight, sysH);
                int targetW = Mathf.RoundToInt(targetH * ((float)sysW / sysH));
                Screen.SetResolution(targetW, targetH, true);
            }
        }

        private void Start() => CalibrateDragThreshold();

        // G18 — 탭↔드래그 임계 DPI 보정(1회). EventSystem 기본 10px 는 고DPI 에서 ~0.6mm 라 탭 중 미세 흔들림이 드래그로
        // 오인식된다. **Max 로 적용해 절대 낮추지 않는다.** `Screen.dpi` 가 0(미지원)이면 기본값 유지.
        private static void CalibrateDragThreshold()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null || Screen.dpi <= 0f) return;
            es.pixelDragThreshold = Mathf.Max(es.pixelDragThreshold, Mathf.RoundToInt(Screen.dpi / DpiDivisor));
        }
    }
}
