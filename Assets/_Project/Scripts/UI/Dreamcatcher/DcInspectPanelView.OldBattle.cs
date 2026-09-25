using UnityEngine;

namespace Wassup.UI
{
    // battle-core-rebuild unit 8d — **옛 씬 전용 읽기 창.** 소비자는 옛 첫 판 안내(`UI/Tutorial/FirstRunTutorialController`)
    // 하나다. 새 씬은 이 패널을 쓰지 않고(`CoreSelectionPanel`), 안내는 사용자 결정 ④(2026-09-25)로 제거됐다. 옛 씬이
    // unit 9 까지 컴파일돼야 해서 이 부분 파일로 떼었고, unit 9 가 `retire-set.md` 대로 파일째 지운다.
    public partial class DcInspectPanelView
    {
        // first-run-tutorial unit 9 — 온보딩이 「퇴근」 버튼에 구멍을 뚫기 위한 읽기 전용
        // 접근자(`AwakeningGaugeView.HitRect` 선례). 뷰는 여전히 기능을 모른다 — 「액션
        // 슬롯의 rect」 를 줄 뿐이고, 그것이 퇴근인지 이동인지는 컨트롤러가 정한다.
        //
        // 미빌드/미표시면 null 이다. `Show(onAction: null)` 이면 버튼 자체가 꺼지므로
        // activeInHierarchy 까지 본다 — 온보딩의 딤은 **비활성 대상을 구멍에서 버리고
        // 다시 담지 않으므로**(OutgameTutorialOverlay.SetHoles) 꺼진 rect 를 넘기면
        // 구멍 0개 = 전면 차단이 된다.
        // ⚠ `activeInHierarchy` 만으로는 부족하다. Hide() 는 `_visible=false` 만 세우고
        // 루트는 알파가 0.02 밑으로 떨어질 때까지 **켜져 있으며**, 그동안 Update 가
        // `blocksRaycasts=false` 로 입력을 끊는다 — 즉 «보이지만 누를 수 없는» rect 를
        // 몇 프레임 계속 내주게 된다. 그 rect 에 구멍을 뚫으면 온보딩은 «열어줬다» 고
        // 믿고 기다리는데 플레이어는 영영 못 누른다(그 구간은 정지라 판도 안 끝난다).
        // `interactable` 도 같은 이유로 본다 — 잠긴 버튼에 구멍을 뚫을 이유가 없고,
        // 이 접근자가 null 을 내주는 것이 곧 호출측의 «지금은 안 된다» 신호다.
        public RectTransform ActionRect =>
            _built && _visible && _actionButton != null
            && _actionButton.gameObject.activeInHierarchy
            && _actionButtonComp != null && _actionButtonComp.interactable
                ? _actionButton
                : null;
    }
}
