using Unity.Entities;

namespace Wassup.Battle.Units
{
    /// <summary>
    /// heart-stress-axis unit 6 — **본능이 마음의 방패다.**
    ///
    /// 맵에 **살아있는 방어 본능(`Faction.DefenderInstinct`)이 하나라도 있으면** 마음에 이 태그가
    /// 붙는다. 마지막 본능이 무너지는 순간 태그가 떨어지고, 그때부터 마음이 깎이기 시작한다.
    ///
    /// **왜 «무적» 이 아니라 «후보 제외» 인가.** 피해만 막으면 적이 마음 앞에 붙어 아무 일도
    /// 일어나지 않는 그림이 된다 — 플레이어에겐 버그로 읽힌다. 이 태그는 `AttackSystem` 과
    /// `EnemyAiStateSystem` 의 **타겟 후보 쿼리에서 마음을 빼서**, 적이 애초에 본능·방어유닛을
    /// 조준하게 만든다. 방패가 «막는» 게 아니라 «시선을 돌린다».
    ///
    /// **writer 는 `BattleBridge.SyncGoalStability` 하나다.** 어떤 ECS 시스템도 이 태그를 쓰지 않는다
    /// (구조 변경이 `MonoBehaviour.Update` → `BattleSimGroup` 순서에 의존한다 — 그쪽 주석 참조).
    ///
    /// ⚠ **소비처가 여섯이다. 하나라도 빠지면 규칙이 샌다** — 새 소비처를 만들면 여기 추가할 것:
    ///
    /// | # | 어디 | 무엇을 막나 | 빠지면 생기는 증상 |
    /// |---|---|---|---|
    /// | 1 | `Combat/AttackSystem` 후보 쿼리 | 조준 | 적이 마음을 때린다 |
    /// | 2 | `Combat/EnemyAiStateSystem` 후보 쿼리 | AI 상태(1의 미러) | 멈춰 서서 안 쏜다 |
    /// | 3 | `Movement/StructureDestinationSystem` 후보 수집 | **경로** | 마음 앞에 눌러앉아 대기 |
    /// | 4 | `Bridge/BattleBridge` 스폰 예고선 거점 선택 | 예고 | 예고선이 거짓말한다 |
    /// | 5 | `Units/DamageApplicationSystem` 버퍼 드랍 | **부수 피해** | 골 근처 광역이 마음을 깎는다 |
    /// | 6 | `Bridge/BattleBridge.DrainGoalEvents` 의 `!_coreShielded` | **도달** | 돌격형이 마음을 직격한다 |
    ///
    /// 1·2 는 조준을, 3 은 **갈 곳**을 막는다. 조준만 막으면 본능은 «벽» 이 아니라 «타이머» 가
    /// 된다 — 적이 마음 앞에 모여 대기하다 방패가 깨지는 순간 일제히 친다.
    ///
    /// 5·6 은 **조준으로 못 막는 것**을 맡는다:
    /// - 5 — 마음을 «겨눈» 게 아니라 옆에 떨어진 광역(`ProjectileHitSystem` 의 TileAoe.
    ///   피해자 마스크가 `Factions.AnyDefender` = `DefenderCore` 포함. 라이브 생산자 2곳 —
    ///   보스 임계 barrage · 궁극기 슬램). **`ProjectileHitSystem` 에 중복 필터를 넣지 말 것** —
    ///   생산자마다 거르는 대신 5 한 곳에서 떨어뜨리는 것이 이 설계의 선택이고, 그래야
    ///   새 피해 경로(DoT·미래 페이로드)가 자동으로 덮인다.
    /// - 6 — 돌격형(`attackMethod: None`)은 조준이 아니라 **도달**로 온다. 쿼리로는 못 막는다.
    ///
    /// **방어 본능이 0 인 맵은 이 태그가 한 번도 안 붙는다** = 현행과 완전히 동일(무형 롤아웃).
    /// 라이브 9맵 중 방어 본능이 저작된 곳은 Isle·Ford·Duel 셋뿐이다(2026-08-23).
    /// </summary>
    public struct CoreShielded : IComponentData { }
}
