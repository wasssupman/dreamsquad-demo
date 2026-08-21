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

        [Header("전투 인트로 (B2b)")]
        [Min(0f)]
        [Tooltip("GO! 직후 목표 소개 문구 하나당 노출(초). 두 문구가 이어진다 — 합이 battleFreezeAtSeconds 보다 짧으면 남은 만큼 더 기다린다.")]
        public float goalIntroSeconds = 2.5f;

        [Header("첫 배치 (B3)")]
        [Min(0f)]
        [Tooltip("전투 시작 후 첫 정지까지(초). **적이 화면에 들어와 있어야 의미가 생기므로 실측 튜닝 대상.**")]
        public float battleFreezeAtSeconds = 4f;
        [Min(0)]
        [Tooltip("접근 대기를 끝내는 기준 — 적이 **내 목표에서 이 칸 수 안**까지 들어왔을 때. " +
                 "시간이 아니라 사건이 기준이다. Duel 기준: 목표 x=2, 강 x=10 → 5 면 강 안쪽 3칸(x≈7).")]
        public int approachGoalTiles = 5;
        [Min(0f)]
        [Tooltip("배치 스킬 소개 문구의 노출(초). 배치 안내 직전, 적이 몰려 선 화면을 정지시킨 채 읽는 구간. " +
                 "unit 9 부터 **두 블록이 같은 값을 쓴다**(말파이트 · 샷건맨).")]
        public float skillHintSeconds = 2.5f;
        [Min(0f)]
        [Tooltip("배치 후 정지를 풀어 배치 스킬이 적을 때리는 것을 보여주는 시간(초).")]
        public float onPlaceWatchSeconds = 2f;

        [Header("퇴근 (B3b)")]
        [Min(0f)]
        [Tooltip("퇴근 비행을 보여주는 시간(초). **이 구간은 정지를 푼다** — " +
                 "DefenderRetireFlight 는 Battle 도메인 델타로 도는 3막 1.6초 연출이라 " +
                 "정지한 채 기다리면 유닛이 공중에 멈춘 채 뒤 스텝들을 통과한다.")]
        public float retireWatchSeconds = 1.7f;
        [Min(0f)]
        [Tooltip("유닛 선택 후 인스펙트 패널의 액션 버튼이 뜰 때까지의 **상한** 대기(초). " +
                 "지나도 안 뜨면 퇴근 블록을 건너뛴다 — 조건 대기로 두면 앱이 잠긴다.")]
        public float retirePanelGraceSeconds = 2f;

        [Header("드림캐쳐 부착 (B4)")]
        [Min(0f)]
        [Tooltip("\"충분한 양의 에너지를 모았네요!\" — 각성 항아리를 가리키는 시간(초). 유닛 재선택 안내 직전.")]
        public float jarHintSeconds = 2f;
        [Min(0f)]
        [Tooltip("B3 종료 후 다시 정지할 때까지 판을 정상 속도로 돌리는 시간(초). " +
                 "**안내가 없는 구간이라 길면 «끊긴» 느낌이 난다** — 5초는 길었다. " +
                 "unit 9 에서 2 → 0.5: 바로 앞이 샷건맨 배치 스킬 관람 2초(정지 해제)라 " +
                 "이 창이 메우려던 끊김이 이미 없고, 퇴근 관람이 새로 쓰는 예산을 여기서 반납한다 " +
                 "(spec 계약 19 — 정지 해제 총합 < 2웨이브 첫 스폰 16.0초). " +
                 "부착 대상이 없을 때는 이 시간과 무관하게 살아있는 유닛이 생길 때까지 더 기다린다.")]
        public float resumeBeforeAttachSeconds = 0.5f;
        [Min(0f)]
        [Tooltip("부착 연출 후 마무리 문구까지(초).")]
        public float attachSettleSeconds = 2f;
        [Min(0f)]
        [Tooltip("유닛 선택 후 손패가 딜인돼 카드가 활성화될 때까지의 **상한** 대기(초). " +
                 "지나도 낼 수 있는 카드가 0이면 부착 구간을 건너뛴다 — 조건 대기로 두면 앱이 잠긴다.")]
        public float cardDealInGraceSeconds = 2f;

        [Header("첫 손패 저작")]
        [Tooltip("온보딩 판의 첫 손패에 이 순서로 올린다. **저장 덱에 실제로 든 카드만** 옮겨진다 — " +
                 "없는 카드는 무시하고 경고를 남긴다(편성을 조작하지 않는다). 비우면 기존대로 전량 셔플.")]
        public DreamcatcherCard[] firstHandCards = System.Array.Empty<DreamcatcherCard>();

        [Header("차단막")]
        [Range(0f, 1f)]
        [Tooltip("배틀 구간 차단막의 불투명도. **0 = 보이지 않는 차단막**(입력만 막고 화면은 그대로). " +
                 "판이 도는 동안 화면을 덮는 게 어색해서 0 이 기본이다. 로비는 정지 화면이라 이 값을 쓰지 않는다.")]
        public float dimOpacity;

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
