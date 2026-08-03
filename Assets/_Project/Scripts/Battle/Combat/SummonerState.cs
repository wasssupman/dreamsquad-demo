using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // summon-patrol-defender unit 3 — 소환사의 소환 상태. Combat 소유.
    //
    // 쿨다운을 자체 보유하지 않는다 — AttackState.cooldownRemaining 이 소환 주기다
    // (폭탄맨 선례). current 가 유효한 동안 소환을 스킵하고 쿨다운만 돈다.
    //
    // patrolDataIndex 는 Bridge 측 DefenderUnitData 레지스트리 인덱스다. SO 는 managed 라
    // 컴포넌트에 담을 수 없어서 RegisterZoneHazardSO/GetOrCreateProjectileDataIndex 와 같은
    // 인덱스 등록 관용구를 따른다.
    public struct SummonerState : IComponentData
    {
        public int    patrolDataIndex;
        public int    leashTileRadius;
        public Entity current;   // 살아있는 순찰병. Entity.Null = 없음
    }
}
