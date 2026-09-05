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
    // 이 태그가 게이팅하는 것 — **4개 지점이 전부**다: `MovementSystem` 의 사냥 대상 스캔 게이트
    // (`:100`)와 존치된 lookup(`:54`, 소비처 0 · Burst 이유), `DefenderFieldSystem` 의 재빌드
    // skip(`:60`)과 소스 반경 R 산출(`:64`). 사냥 **이동** 게이트는 이제 이 태그가 아니라
    // `DetectedTarget.hunting` 이다(enemy-detection-range unit 3).
    // `BossTag` 에 남는 것: 넉업 면역(AttackSystem) · 어그로 면역(AggroStateSystem) ·
    // CC 면역(CcApplySystem·EffectSpawner) · 등장 경보 · ThreatEntry.
    // 새 소비처를 붙일 때 «보스라서 그런가, 사냥꾼이라서 그런가» 를 먼저 묻는다.
    //
    // enemy-detection-range unit 1 — 태그의 뜻이 **「감지를 쓴다」**로 넓어졌다. 부착 조건이
    // `tier == Boss || huntsDefenders` 에서 **`UsesDetection`**(임계 비교) 하나가 됐고(티어는 더 이상
    // 사냥을 주지 않는다), 무제한 사냥은 그 값의 음수 구간이 됐다.
    //
    // 부착은 `BattleBridge.CreateEnemyEntity` 본문에서 `unitType.UsesDetection` 로 1회
    // (`DetectionRange` 값과 **같은 자리**에서 함께 붙는다 — 갈리면 「태그는 있는데 반경이 없다」).
    // 스폰 이후 **불변**이다 — 전투 중 떼는 시스템이 생기면 맥락 소유권 질문이 다시 열린다
    // (지금은 브리지가 유일 writer, Movement·Effects 가 RO 소비).
    public struct DefenderHunterTag : IComponentData { }
}
