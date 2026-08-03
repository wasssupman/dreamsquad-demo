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

        // 초회 소환 게이트의 소비 여부. false 인 동안엔 **거점 구역 안에 적이 있어야** 소환한다
        // (사용자 결정 2026-08-03: "처음엔 적이 있어야 만들고, 한 번 만들면 유지").
        // 한 번 true 가 되면 이후 재소환은 적 유무를 보지 않는다 — 순찰병이 죽는 이유가
        // 곧 적이 있다는 뜻이라 재게이트는 같은 사실을 두 번 묻는 셈이고, 교전 도중
        // 적이 잠깐 구역을 벗어난 프레임에 재소환이 끊기면 순환이 덜컥거린다.
        //
        // writer 는 Bridge 단독(순찰병이 **실제로 생성된** 시점). AttackSystem 이 요청을
        // stage 할 때 켜면 스냅 실패로 소환이 취소된 경우에도 게이트가 소비된다.
        public bool   hasSummonedOnce;
    }
}
