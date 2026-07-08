using UnityEngine;

namespace Wassup.UI
{
    /// 팝업/오버레이 UI 뒤 전체화면 dim 의 단일 소스.
    /// 인게임 배틀 메뉴·전투 결과·드림캐쳐 카드팝업·스쿼드 피커·드림캐쳐 선택 등
    /// 모든 backdrop 이 이 값 하나를 공유한다. 톤(어둡기) 조정은 여기 한 곳에서만.
    public static class UiOverlay
    {
        // 통일 dim — 순수 검정, 좀 더 어둡게. Play 로 톤 확정 후 이 한 줄만 조정.
        public static readonly Color Dim = new Color(0f, 0f, 0f, 0.92f);

        // CanvasGroup fade 로 dim 을 처리하는 오버레이(예: WavePatternStripView)용 알파.
        public static float DimAlpha => Dim.a;
    }
}
