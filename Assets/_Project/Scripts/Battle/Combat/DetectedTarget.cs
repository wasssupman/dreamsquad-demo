using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // enemy-detection-range unit 2·4 — **감지 상태.** Combat 소유, `DetectionSystem` 이 유일한 writer.
    // `MovementSystem`(Movement)은 `hunting` 하나만 RO 로 읽는다.
    //
    // 스폰 시 `DetectionRange` 와 **같은 자리에서 함께** 부착된다(값만 매 프레임 write — 핫패스에서
    // 구조 변경을 하지 않는다. `enemy-hunter-targeting` 계약 6 의 교훈).
    public struct DetectedTarget : IComponentData
    {
        // 발견한 방어유닛. **로그·트레이스 전용**이다(계약 6).
        //
        // ⚠ **이동도 화면도 이 값을 «목적지»로 쓰지 않는다.** 감지는 직선 최근접 legal 을 고르지만
        // 이동은 공용 사냥판(`DefenderFieldSingleton`)이라 «경로가 가장 가까운 아무 방어유닛» 쪽으로
        // 흐른다 — 실측 5.0% 에서 둘이 갈린다. 화면이 이 값을 가리키면 그 5.0% 에서 거짓말을 한다.
        // 읽기 시작하는 순간 B안(대상 전용 추격판)이 되며, 그건 별도 결정이다.
        public Entity target;

        // 1 = 이번 프레임 감지 성립. **Movement 가 읽는 유일한 값.**
        public byte hunting;

        // unit 4 — 대상을 잃은 뒤에도 사냥을 유지하는 관성(초). > 0 이면 `target` 은 비었지만
        // `hunting` 은 1 이다: 적은 계속 사냥판을 따르며 다음 대상을 찾는다.
        public float graceRemaining;

        // unit 4 — 「사냥 중인데 못 가고 있다」가 이어진 시간(초). 감지는 legal 필터를 지나지만
        // 이동을 만드는 소스 수집은 안 지나므로(`DefenderFieldSystem` 은 faction 필터뿐),
        // 못 때리는 방어유닛 앞에서 영구 정지할 수 있다. 그 구간을 끊는다.
        public float stuckSeconds;

        // unit 4 — 막힘 해제 뒤 재감지를 막는 시간(초). 없으면 다음 프레임에 같은 대상을 다시 물어
        // 제자리에서 깜빡인다.
        public float suppressRemaining;

        // unit 5 — 「발견」 표식 재발화 방지(초). 없으면 **도발이 풀릴 때마다 표식이 다시 뜬다**:
        // 어그로 중에는 감지가 `hunting = 0` 으로 강제되므로(계약 2) 도발 해제마다 0→1 전이가
        // 새로 생기는데, 도발은 반복 메커닉이라 같은 적이 같은 상황에서 계속 「발견!」 한다.
        // 사건의 값은 «처음 봤다» 에 있지 «다시 봤다» 에 있지 않다.
        public float markCooldown;
    }
}
