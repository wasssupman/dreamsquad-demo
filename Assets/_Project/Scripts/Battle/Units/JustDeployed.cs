using Unity.Entities;

namespace Wassup.Battle.Units
{
    // on-place-skill-rework unit 0 — 「이 유닛이 방금 판에 놓였다」는 1프레임 사건 태그.
    //
    // 왜 태그인가: 배치 확정 지점이 브리지에 **셋**이다(D&D `TriggerDeploymentOnPlaceSkill` ·
    // 탭 `TriggerOnPlaceAndSynergy` · 재배치가 재호출하는 `ActivateDeployedDefender`).
    // 배치 스킬 실행을 브리지에 두면 그 셋을 전부 후킹해야 하고, 하나만 놓치면 그 경로에서만
    // 스킬이 안 나가는 채로 테스트가 초록이 된다(기존 on-place PlayMode 는 전부 탭 경로다).
    // 브리지는 **태그만** 붙이고 `DcTriggerKind.OnPlace` 슬롯 소비는 BossPeriodicTriggerSystem
    // 이 한다 — 그 시스템은 이미 진영 중립이고 payload arm 을 전부 갖고 있어 사본이 늘지 않는다.
    //
    // 수명: 붙인 다음 sim 틱에 소비 시스템이 ECB 로 제거한다(반드시 ECB — 소비 루프가
    // DcTriggerSlot 버퍼를 순회 중이라 즉시 RemoveComponent 는 이터레이션을 죽인다).
    //
    // ⚠ 이 태그는 규칙 경로의 1회 보장 권위다. 예전엔 레거시 `OnPlaceEffectType` 경로가 따로 있었고(skill-layer-migration unit 2g 에서 철거) 그쪽은
    // `BattleBridge._onPlaceTriggeredEntities`(managed HashSet)가 소유하고 재배치 재무장까지
    // 책임진다. 둘을 하나로 합치려다 재무장을 깨지 말 것.
    public struct JustDeployed : IComponentData { }
}
