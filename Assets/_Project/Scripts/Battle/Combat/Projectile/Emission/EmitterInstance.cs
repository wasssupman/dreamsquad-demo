using Unity.Entities;
using Wassup.Data;

namespace Wassup.Battle.Combat.Projectile.Emission
{
    // projectile-emission-pattern unit 2 — 진행 중인 한 번의 발사. 트리거가 push 하고
    // ProjectileEmitterSystem 이 tick 해서 완주하면 제거한다.
    //
    // 층 배치가 계약 1·2 를 지키는 방식: 순수 부분(spec·runtime)은 이 ECS 컴포넌트
    // 안에 **값으로 박히기만** 하고 아키텍처 타입을 참조하지 않는다. ECS 바인딩은
    // template·lockedTarget 둘뿐이며, Mono 이식 시 그 자리에 Mono 용 발사 파라미터가
    // 들어간다. 스케줄 상태가 template 을 **품는** 형태(VolleyFireState)는 순수 코어를
    // 이식 불가로 만들기 때문에 쓰지 않는다.
    [InternalBufferCapacity(2)]
    public struct EmitterInstance : IBufferElementData
    {
        // 시작 시점 값 스냅샷 — 발사 도중 SO/버프가 바뀌어도 7번째 탄이 1번째와
        // 달라지지 않는다(defender volley 의 template 스냅샷 보증을 구조에서 얻는다).
        public PatternSpec spec;
        public EmitterRuntime runtime;

        // bake 가 조립한 발사 요청 원본. 타겟 의존 필드(target/impact/swingIndex)만
        // 비어 있고 emitter 가 발마다 채운다.
        public ProjectileSpawnRequest template;

        // reselectPerShot == false 일 때 잠근 대상. **index 가 아니라 Entity 다** —
        // 후보 스냅샷은 프레임-로컬이라 index 를 잠그면 프레임을 넘는 버스트에서
        // 같은 index 가 다른 유닛을 가리킨다(spec-review H1).
        public Entity lockedTarget;
    }
}
