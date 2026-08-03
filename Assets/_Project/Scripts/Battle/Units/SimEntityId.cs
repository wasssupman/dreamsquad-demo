using Unity.Collections;
using Unity.Entities;

namespace Wassup.Battle.Units
{
    // battle-sim-extraction unit 1 — 매치 내 비재사용 stable ID (스폰 순 발급, 0부터).
    //
    // Entity.Index/Version 은 할당 순서 산물이라 신 sim(M1)에서 재현 불가 — 타겟팅
    // 동률 tiebreak·발사 패턴 RNG seed·(이후) 커맨드/이벤트/스냅샷/뷰 키의 축을
    // 이 값으로 통일한다. 발급은 BattleBridge 스폰 지점의 단일 카운터
    // (AttachSimEntityId, 매치 경계 리셋은 BeginPlacement — 배치 페이즈에서
    // defender 가 먼저 태어난다). 부착 후 불변.
    public struct SimEntityId : IComponentData
    {
        public int value;

        // 미부착 엔티티 폴백 = Entity.Index. Bridge 미경유 조립 월드(테스트 rig)
        // 에서만 도달하는 경로로, 라이브 스폰은 전부 부착된다 — unit 1 허용 예외 ①.
        public static int Resolve(in ComponentLookup<SimEntityId> lookup, Entity e)
            => lookup.HasComponent(e) ? lookup[e].value : e.Index;
    }
}
