using UnityEngine;

namespace Wassup.Data.BattleView
{
    // battle-core-rebuild unit 5a — 도약 연출 노브. 옛 브리지 partial 두 개
    // (`BattleBridge.BossLeap.cs` 10 · `BattleBridge.UltimateLeap.cs` 5)의 새 주인.
    //
    // ⚠ **도약의 규칙은 코어가 이미 끝냈다.** 피해도 순간이동도 코어의 것이고, 이 자산과
    // 그 소비자(`CoreLeapPresenter`)가 가진 것은 **뷰가 그 사이를 어떻게 나는가**뿐이다.
    // 그래서 여기에는 피해·반경·사거리가 없다 — 하나라도 생기면 뷰가 규칙을 갖게 된다.
    //
    // 기하 4종은 드롭 하마(D&D)와 값이 **의도적으로 동일**하다(사용자 지시 2026-07-29):
    // 같은 함수를 쓰는데 값까지 같으면 두 연출이 한 몸짓으로 읽힌다. 다만 `DragSwaySettings`
    // 를 참조하지는 않는다 — UI 튜닝이 전투 연출을 조용히 바꾸면 안 된다.
    [CreateAssetMenu(menuName = "Wassup/BattleView/Leap Visual Config", fileName = "LeapVisualConfig")]
    public sealed class LeapVisualConfig : ScriptableObject
    {
        [Header("보스 도약 (일반)")]
        [Tooltip("도약 총 시간(초). 배틀 도메인 기준 — 슬로모 중엔 함께 느려진다.")]
        [SerializeField, Min(0.05f)] private float bossTotalSeconds = 0.83f;

        [Tooltip("웅크리는 반동 시간(초).")]
        [SerializeField, Min(0f)] private float bossRecoilSeconds = 0.14f;

        [Tooltip("반동으로 내려앉는 거리(월드). = dropRecoilDip")]
        [SerializeField] private float bossRecoilDip = 0.35f;

        [Tooltip("아치 높이 = 이동거리 × 이 계수. = dropArcHeightFactor")]
        [SerializeField] private float bossArcHeightFactor = 0.5f;

        [Tooltip("아치 제어점 높이 하한(view 공간). = dropArcMinHeight")]
        [SerializeField] private float bossArcMinHeight = 6f;

        [Tooltip("발사 제어점 (x=진행비율, y=아치높이배수). = dropLaunchControl")]
        [SerializeField] private Vector2 bossLaunchControl = new Vector2(0.25f, 1f);

        [Tooltip("착지 제어점 높이배수. 작을수록 수직으로 내리찍는다. = dropLandingHeight")]
        [SerializeField] private float bossLandingHeight = 0.25f;

        [Tooltip("비행 구간 시간 리듬. 1 = 등속. 낮출수록 초반 급상승→체공→급하강. = dropHangPower")]
        [SerializeField, Range(0.3f, 1f)] private float bossHangPower = 0.7f;

        [Tooltip("착지 눌림 세기(0 = 없음). = dropLandingSquash")]
        [SerializeField, Range(0f, 0.4f)] private float bossLandingSquash = 0.1f;

        [Tooltip("착지 눌림 복귀 시간(초). = dropLandingSquashSeconds")]
        [SerializeField, Range(0.02f, 0.3f)] private float bossLandingSquashSeconds = 0.05f;

        [Header("궁극기 도약 (이탈 → 예고 → 강습)")]
        // ⚠ 예고 시간은 **여기 없다.** 그것은 코어 시퀀스가 소유하고 이벤트로 온다.
        // 뷰가 복제하면 두 시계가 갈리고, 그 어긋남은 슬로모 중에만 보인다.
        [Tooltip("판 밖으로 빠지는 시간(초).")]
        [SerializeField, Min(0.05f)] private float ultimateAscendSeconds = 0.45f;

        [Tooltip("강습으로 내려오는 시간(초).")]
        [SerializeField, Min(0.05f)] private float ultimateDescendSeconds = 0.25f;

        [Tooltip("이탈 높이(view 공간).")]
        [SerializeField] private float ultimateHeight = 14f;

        [SerializeField, Range(0f, 0.4f)] private float ultimateLandingSquash = 0.14f;
        [SerializeField, Range(0.02f, 0.3f)] private float ultimateLandingSquashSeconds = 0.06f;

        // battle-core-rebuild unit 8a2 행 2 — 착지 예고 링의 색. 옛 `TilemapMapView.landingTelegraphColor`
        // (`TilemapMapView.cs:40` · 옛 씬 `BattleScene.unity:588` = (1, 0.45, 0.08, 0.42))의 새 주인.
        // **알파는 「채움」 세기**이고 선은 불투명하게 올린다(옛 `SetTelegraphRing` `:699-703` 규약 그대로).
        [Tooltip("궁극기 착지 예고 링 색. 알파 = 내부 채움 세기(선은 불투명). 배치 사거리 링과 **색으로** 갈린다.")]
        [SerializeField] private Color landingTelegraphColor = new Color(1f, 0.45f, 0.08f, 0.42f);

        public float BossTotalSeconds => bossTotalSeconds;
        public float BossRecoilSeconds => bossRecoilSeconds;
        public float BossRecoilDip => bossRecoilDip;
        public float BossArcHeightFactor => bossArcHeightFactor;
        public float BossArcMinHeight => bossArcMinHeight;
        public Vector2 BossLaunchControl => bossLaunchControl;
        public float BossLandingHeight => bossLandingHeight;
        public float BossHangPower => bossHangPower;
        public float BossLandingSquash => bossLandingSquash;
        public float BossLandingSquashSeconds => bossLandingSquashSeconds;

        public float UltimateAscendSeconds => ultimateAscendSeconds;
        public float UltimateDescendSeconds => ultimateDescendSeconds;
        public float UltimateHeight => ultimateHeight;
        public float UltimateLandingSquash => ultimateLandingSquash;
        public float UltimateLandingSquashSeconds => ultimateLandingSquashSeconds;
        public Color LandingTelegraphColor => landingTelegraphColor;
    }
}
