using Unity.Entities;
using Unity.Mathematics;

namespace Wassup.Battle.Combat
{
    // summon-patrol-defender unit 3 — 소환 요청(Combat→Bridge).
    //
    // 신규 NativeQueue 채널을 만들지 않는다. ProjectileRequestCarrier 와 같은 **전용
    // 캐리어 엔티티** 관용구다 — AttackSystem 에서 Bridge 스폰을 요청하는 관용구가 이미
    // 그 자리에 있고, 싱글턴 배선도 CLAUDE.md 채널 목록 갱신도 불요하다.
    //
    // ownerCell 은 소환사 셀 그대로다. unit 9 이후 이 셀이 **곧 거점**이고, 순찰병이 그
    // 칸을 밟을 수 없을 때만 Bridge 가 통행 가능 셀로 퇴화 스냅한다 — 그 판정이 셀 층을
    // 보는 Mono 측 API(GeneratedMap)이기 때문이다.
    public struct PatrolSpawnRequest : IComponentData
    {
        public Entity owner;
        public int2   ownerCell;
        public int    patrolDataIndex;
        // unit 9 — 담당 구역 반경. 소환사 AttackState.range 에서 파생한 타일 수다.
        public int    coverTileRadius;
    }

    // 캐리어 태그. 드레인이 통째로 파괴하고, 매치 경계 정리도 이 타입으로 건다
    // (드레인 사이에 전투가 멈춘 낙오분 회수 — 투사체 캐리어가 같은 이유로 등재돼 있다).
    public struct PatrolRequestCarrier : IComponentData { }
}
