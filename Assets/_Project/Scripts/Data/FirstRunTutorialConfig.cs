using UnityEngine;
using Wassup.Core;

namespace Wassup.Data
{
    // first-run-tutorial unit 0 — 온보딩 시퀀스의 «얼마나 기다리는가» 를 한 자리에 모은다.
    // 문구는 여기 두지 않는다 — 컨트롤러의 const 다(옛 OutgameTutorialController 관용구).
    //
    // 배선은 컨트롤러의 [SerializeField] 하나면 된다. GameManager 에 노출하지 않는다
    // (TutorialGuidanceView.style 과 같은 형태).
    [CreateAssetMenu(menuName = "Wassup/Config/FirstRunTutorialConfig", fileName = "FirstRunTutorialConfig")]
    public sealed class FirstRunTutorialConfig : ScriptableObject
    {
        [Header("맵 설명 (B1)")]
        [Min(0f)]
        [Tooltip("가능/불가 한 면을 보여주는 시간(초).")]
        public float briefingHoldSeconds = 1.2f;
        [Min(0)]
        [Tooltip("가능 ↔ 불가 왕복 횟수. 0 이면 목표 문구만 띄우고 넘어간다.")]
        public int briefingCycles = 2;
        [Min(0f)]
        [Tooltip("\"게임목표\" 문구 노출 시간(초).")]
        public float goalMessageSeconds = 2.5f;

        [Header("첫 배치 (B3)")]
        [Min(0f)]
        [Tooltip("전투 시작 후 첫 정지까지(초). **적이 화면에 들어와 있어야 의미가 생기므로 실측 튜닝 대상.**")]
        public float battleFreezeAtSeconds = 4f;
        [Min(0)]
        [Tooltip("\"악몽이 배치 영역 안으로 들어오면!\" 을 끝내는 기준 — 적이 강(Env 타일)에서 이 반경 안까지 왔을 때. 시간이 아니라 사건이 기준이다.")]
        public int riversideTiles = 1;
        [Min(0f)]
        [Tooltip("배치 후 정지를 풀어 배치 스킬이 적을 때리는 것을 보여주는 시간(초).")]
        public float onPlaceWatchSeconds = 2f;

        [Header("드림캐쳐 부착 (B4)")]
        [Min(0f)]
        [Tooltip("B3 종료 후 다시 정지할 때까지 판을 정상 속도로 돌리는 시간(초).")]
        public float resumeBeforeAttachSeconds = 5f;
        [Min(0f)]
        [Tooltip("부착 연출 후 마무리 문구까지(초).")]
        public float attachSettleSeconds = 2f;

        [Header("안전망")]
        // 스텝 타임아웃은 두지 않는다(사용자 결정). 안내가 요구한 행동을 할 때까지 기다린다 —
        // 흘려보낸 판은 어차피 완료로 기록되지 않아 다음 판에 처음부터 다시 뜨기 때문이다.
        // 아래 홀드 상한은 성격이 다르다: 컨트롤러가 죽어 Release 를 못 부르면 카운트다운이
        // 3에서 영영 멈추고 전투가 시작조차 못 한다 — 이건 플레이어가 풀 수 없는 상태다.
        [Min(1f)]
        [Tooltip("카운트다운 홀드 상한(초). 컨트롤러가 죽어 Release 를 못 불러도 스스로 풀린다.")]
        public float introHoldMaxSeconds = 30f;

        // 판정을 여기 두는 이유: 소비자가 로비 스텝과 배틀 컨트롤러 둘이라 공용 자리가
        // 필요하지만, 그것 하나 때문에 새 타입을 세우면 제약 8(구현체 2개 미만 추상 금지)과
        // 부딪힌다. 선례 OutgameMenuController.IsFirstMatch 와 같은 급의 static 하나로 충분하다.
        //
        // ⚠ 이건 게이트의 **절반**이다. 호출부는 여기에 profileSO.IsLoadedThisSession 을
        // 곱한다 — 미로드 프로필의 빈 인스턴스가 false 로 읽혀 이미 튜토리얼을 본 유저에게
        // 다시 뜨는 것을 막는다. 세션 가드는 SO 상태라 이 순수 함수로 겨눌 수 없다.
        public static bool ShouldRun(PlayerProfile profile)
            => profile != null && !profile.firstRunTutorialDone;
    }
}
