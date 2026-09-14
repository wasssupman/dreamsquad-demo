using Unity.Entities;
using UnityEngine;

namespace Wassup.Presentation
{
    // sprite-unit-backend unit 1 — 유닛 뷰 백엔드의 공통 표면. **선언만 있다** — 필드도 헬퍼도 0.
    //
    // 왜 인터페이스가 아니라 추상 클래스인가(critic C-1): 정적 타입이 인터페이스면 UnityEngine.Object 의
    // operator== 오버로드가 선택되지 않아 `view != null` 이 참조 비교로 떨어진다. 그러면 파괴된 뷰가
    // 풀(10곳)·소비처(13곳)의 생존 판정을 통과하고 DespawnMissing 의 탐지기가 영영 안 터진다 —
    // 컴파일은 통과하고 버그만 남는 종류다. MonoBehaviour 파생이면 이 문제가 애초에 없고,
    // 소비처가 쓰는 transform/gameObject/GetComponent 도 그대로 닿는다.
    //
    // 멤버는 소비 seam(BattleBridge·DefenderRetireFlight·DefenderRelocationController·PlayMode 테스트)이
    // **실제로 호출하는 것**만이다. SpineUnitView 의 public 표면에서 뺀 것:
    //   Spawn — 백엔드별 시그니처(풀이 concrete 로 부른다)
    //   SetLoopOverride / ClearLoopOverride — Spine 애니 **이름 문자열** API. 스프라이트에 대응 축이 없다.
    //     호출부(소환사 sync) 는 `is SpineUnitView` 로 가른다.
    // 구현체 2개(Spine·Sprite)라 제약 8, 깊이 2(MonoBehaviour → UnitView → 구현)라 제약 7 정합.
    // QuadUnitView 는 편입하지 않는다 — 개발용 폴백을 백엔드로 승격시키는 별개 결정이다.
    public abstract class UnitView : MonoBehaviour
    {
        public abstract Entity Entity { get; }

        // 「지금 무엇을 재생 중인가」 — 테스트가 단언하는 유일한 창구(summon-patrol-defender unit 10).
        // Spine = 트랙0 애니 이름, 스프라이트 = 현재 플립북 에셋 이름.
        public abstract string CurrentAnimationName { get; }

        // time-manager Unit 4 — Battle 도메인 스케일 fan-out(SpineUnitPool.OnBattleScaleChanged).
        public abstract void SetAnimationTimeScale(float scale);

        // 프레임 진입점 — sim 좌표. 위치·facing·로코모션·hop 진행이 여기서 한 번 돈다.
        public abstract void UpdatePosition(Vector3 world);
        public abstract void SetFlightHeight(float viewSpaceHeight);
        public abstract void PlayKnockupHop(float durationSec, float height);
        public abstract void UpdateSortingOrder(Unity.Mathematics.int2 gridSize, float tileSize);

        // 스크린 픽킹·락온·오버헤드 UI 앵커. 카메라 뒤면 false.
        public abstract bool TryGetScreenRect(Camera cam, out Rect rect);
        public abstract float ApproxWorldHeight { get; }

        // 재배치 비행 — view 좌표 직접 배치 + 전경 소팅 + 그림자 기저선 앵커.
        public abstract void SetFlightView(Vector3 viewPos, float lift = 0f, Vector3 groundAnchor = default);

        // 틴트 4축 — hover(RGB 저장/복원) · health(RGB) · flash(RGB lerp) · dim(A).
        public abstract void SetHoverHighlight(bool on, Color tint);
        public abstract void SetHealthTint(Color tint);
        public abstract void FlashWhite(float dur = 0.14f);
        public abstract void SetDimmed(bool transparent, float alpha);

        // 스케일 반응 — 펀치(균등)·착지 스쿼시(비균등).
        public abstract void PlayPunch(float overshoot = 0.28f, float dur = 0.16f);
        public abstract void PlayLandingSquash(float amount, float seconds);

        // 모션 사건 — 공격(발사 주기 압축)·배치·사망(연출 후 자멸)·즉시 파괴.
        public abstract void PlayAttack(float attackAnimPeriod = 0f);
        public abstract bool PlayDeploy();
        public abstract void Kill();
        public abstract void Dispose();

        // facing — 공격 타겟 지정(즉시 반전). 이동 유래 반전은 UpdatePosition 안에서 히스테리시스로.
        public abstract void FaceToward(Vector3 worldPoint);

        // 앵커 — view 공간. 캐스트 VFX / 투사체 발사 원점.
        public abstract Vector3 ResolveCastAnchor();
        public abstract Vector3 ResolveProjectileLaunchAnchor();
    }
}
