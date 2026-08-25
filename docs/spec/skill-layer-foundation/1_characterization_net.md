# 1 — 그물: arm 특성화 테스트

## 목적

이전하는 arm 이 **옮기기 전과 같이 동작하는지 말해줄 증인**을 세운다.

⚠ **골든 코퍼스(`LegacyTraceV0`)에 기대지 않는다.** 그 축은 `battle-sim-extraction` 소관이고
**아직 착수도 확정도 되지 않았다**(사용자 판정 2026-08-25). 이 spec 은 그 진행 여부와 무관하게
서야 한다 — 증인은 **arm 별 특성화 테스트**가 전담한다.
골든이 나중에 정비되면 **추가** 그물로 쓸 수 있지만, 이 spec 의 완료 기준에는 넣지 않는다.

## 변경 대상

- `Assets/_Project/Tests/EditMode/` · `Tests/PlayMode/` — arm 특성화 테스트 신설
  (기존 파일 확장 우선, 신규는 차집합만)

## 구현

1. **기존 자산을 먼저 세고 차집합만 쓴다** (실측 2026-08-25 — 기존 커버리지가 보고보다 넓다):

   | 가족 | 이미 있는 것 | 남는 무보호 |
   |---|---|---|
   | 레거시 배치 9 | `ApplyStackNearby`·`DotNearby`·`ForwardProjectile`·`StunNearby` (+`MeleeBurst` 대조군) | `BoostNearbyDefenders`·`BindNearby`·`GainCost`·`ReduceSkillCooldown` **4종** |
   | 방어유닛 규칙 5 | `OnPlaceSkyStrikeTest`·`OnPlaceTauntNearbyTest` **2/5** | `AreaShield`·`OnPlaceBlast`·`BombMan` 3종 |
   | 보스 11행 | `BossLullabyTest`·`BossShieldTest`·`DragonBreathE2ETest` | 궁극기·도약×2·채찍질·경계 자폭 **4종** |
   | 액티브 6 | `ActiveTileCastTest` = **Portal 만** | 나머지 5종 |
   | 캐스트 8 | `HazardCasterTests`(EditMode)·`EnemyShieldTest`·`KindlerFireStackE2ETest` | 볼리 2·폭탄 1 |
   | 소환 1 | `PatrolDefenderPlayTest` | — |
   | 카드 26행 | `DcApplicabilityTests`·`DcTriggerTests`·`DcTriggerArmedTests` (전부 **술어 레벨**) | 동작 특성화 대부분 |

2. **단언은 「내 변경이 의도대로다」가 아니라 「관측 가능한 결과가 같다」로 쓴다.**
   arm 하나당 최소 1개: 발동 조건을 만족시킨 뒤 **그 arm 이 실제로 만든 것**(피해량·CC 부여·
   투사체 수·스택·실드량·소환물)을 단언한다. 이전 전에 **빨간 것을 먼저 본다.**

3. **`TestSkillContext` 가 서면 그쪽으로 이관 가능한 것을 표시한다.** unit 3 이 페이크를 만들면
   무거운 질의(밀집·착지·범위선별·콘·조준)가 전부 순수 코어 재사용이라 **ECS 월드 없이** 돈다.
   지금 PlayMode 로만 가능한 단언 중 어느 것이 EditMode 로 내려올 수 있는지 표시해두면 unit 3
   이후 테스트 비용이 크게 준다. (이 unit 에서 옮기지는 않는다 — 순서상 포트가 뒤다.)

4. **비라이브 행은 우선순위를 낮춘다.** `visible: 0` 카드 **11행/9장**은 라이브 경로에 없다
   (census 실측). 라이브 21행을 먼저 덮는다.

5. **PlayMode 전량 주행은 하지 않는다.** 8분이고 에디터를 독점한다. arm 별로 필요한 것만 돌리고,
   EditMode 로 표현 가능한 것은 EditMode 에 쓴다.

## 완료 기준

- [ ] 위 표의 **남는 무보호**가 전부 특성화 테스트를 갖는다 (레거시 4 · 규칙 3 · 보스 4 ·
      액티브 5 · 캐스트 3 · 카드 라이브 행)
- [ ] 각 테스트가 **이전 전에 초록**이다 — 지금 동작을 박제한 것이지 새 동작을 정의한 게 아니다
- [ ] 단언이 「그 arm 이 만든 결과」를 본다 (스폰·컴포넌트 존재만 보는 단언 금지)
- [ ] EditMode 코어 lane + Assets lane 초록. PlayMode 는 **추가한 것만** 선택 주행
- [ ] 골든 코퍼스에 대한 의존이 **0** — 이 spec 은 그 축의 진행 여부와 무관하게 선다
