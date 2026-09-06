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
        // 흘렀다 — 실측 5.0% 에서 둘이 갈렸다.
        // ⚠ **unit 8 에서 «유한 반경» 한정으로 이 경고가 풀렸다.** 유한 감지는 이제 «그 대상»
        // 까지 구운 추격판(`DetectionChaseDist`/`Flow`)을 따라가므로 대상과 도착지가 **일치**한다.
        // **무제한 감지는 여전히 갈린다** — 공용 사냥판(「아무 방어유닛이나」)을 타기 때문이고,
        // 그건 결함이 아니라 그쪽의 정확한 질문이다. 화면이 이 값을 쓰려면 **감지 종류로 갈라야**
        // 한다(무제한에서 가리키면 여전히 거짓말이다).
        // (「읽기 시작하는 순간 B안이 된다」던 옛 경고는 unit 8 이 **그 B안을 채택**하면서 끝났다.
        //  이동은 이제 이 값 자체가 아니라 이 값으로 구운 추격판을 읽는다 — 대상은 여전히
        //  `DetectionSystem` 만 쓰고, Movement 는 접근 보정에서만 위치를 참조한다.)
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

        // unit 8 — **대상 지향 추격판의 캐시 키.** 「어느 대상까지 / 어느 장애물 상태로」 구운
        // `DetectionChaseDist`/`Flow` 인가. 둘 중 하나라도 어긋나면 다시 굽는다.
        //
        // ⚠ **매 프레임 굽지 않기 위한 것이다.** 추격판은 그리드 전체 BFS 라 사냥 중인 적
        // 전원이 매 프레임 구우면 Android 에서 실질 비용이 된다. 대상은 방어유닛이라 **움직이지
        // 않으므로**(어그로 추격판이 기대는 것과 같은 전제) 대상이 바뀔 때만 다시 구우면 된다.
        // 방어유닛이 이동하는 저작이 생기면 이 전제가 깨진다 — 그때는 두 추격판을 같이 고친다.
        public Entity chaseBuiltFor;

        // 장애물 변경 무효화. `FlowFieldSingleton.blockedSignature` 의 사본이다.
        // 어그로는 `FlowFieldRebuildSystem`(Effects)이 버퍼를 떼 주지만, 이쪽은 **Combat 이 자기
        // 맥락 안에서** 비교해 다시 굽는다(Effects 가 Combat 컴포넌트를 쓰지 않게).
        public uint chaseSignature;
    }
}
