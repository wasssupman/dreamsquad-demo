using Unity.Entities;

namespace Wassup.Battle.Combat
{
    // bonus-wave-pull unit 0 — 「배치된 방어유닛을 찾아다니며 사냥한다」는 성질.
    //
    // 이 성질은 원래 `BossTag` 가 겸직했다(boss-defender-field). 그 README 가 후속 후보로
    // 「두 번째 수요가 생기면 SO 플래그로 게이트 교체」를 적어뒀고, 보너스 당기기의 보너스 적이
    // 그 수요다 — 저체력 잡몹이 사냥은 하되 **보스 특권은 받으면 안 된다**.
    //
    // ★**사냥 성질과 보스 특권의 분리선이 이 태그의 존재 이유다.**
    // 이 태그가 게이팅하는 것: `MovementSystem` 의 사냥 이동, `DefenderFieldSystem` 의
    // 재빌드 skip 과 소스 반경 R 산출 — **3개 지점이 전부**다.
    // `BossTag` 에 남는 것: 넉업 면역(AttackSystem) · 어그로 면역(AggroStateSystem) ·
    // CC 면역(CcApplySystem·EffectSpawner) · 등장 경보 · ThreatEntry.
    // 새 소비처를 붙일 때 «보스라서 그런가, 사냥꾼이라서 그런가» 를 먼저 묻는다.
    //
    // 부착은 `BattleBridge.CreateEnemyEntity` 본문에서 `tier == Boss || huntsDefenders` 로 1회.
    // 스폰 이후 **불변**이다 — 전투 중 떼는 시스템이 생기면 맥락 소유권 질문이 다시 열린다
    // (지금은 브리지가 유일 writer, Movement·Effects 가 RO 소비).
    public struct DefenderHunterTag : IComponentData { }
}
