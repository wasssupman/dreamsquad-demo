# 4 — PlayMode 검증

## 목적

오라 메커니즘의 핵심 계약을 PlayMode 로 회귀 방지: 신규 배치 부여 / host·기존 유닛 미부여 /
host 사망 회수 / axis 게이팅(M7).

## 변경 대상
- `Assets/_Project/Tests/PlayMode/` 신규 테스트(예: `PlacementAuraTest.cs`)

## 시나리오 (BattleBridge 직접 구동, 기존 Dreamcatcher PlayMode 패턴 재사용)
1. **미부여(기존/host)**: 유닛 A 배치 → 오라 카드를 host H 에 부착(`ApplyDreamcatcherCardToUnit`,
   handle>0). A·H 의 attackSpeedMul == 1.0 (부착 전 배치·host 자신 미부여).
2. **신규 배치 부여**: 오라 활성 중 유닛 B 배치 → B 의 cooldownRemaining ≈ 2, 몇 프레임 후
   attackSpeedMul == 1.5.
3. **host 사망 회수(Q1)**: H 사망 이벤트 → B 의 attackSpeedMul 1.0 로 복귀 + 이후 배치 C 는 미부여.
4. **axis 게이팅(M7)**: axis=ClassGuardian 인 **테스트 전용 오라**를 별도 host 에 등록 → 신규 배치
   Ranger 는 미부여, Guardian 은 부여. (느린 각성 axis=All 로는 게이팅 반증 불가하므로 합성 오라 사용.)

## 완료 기준
- [ ] PlayMode 4 시나리오 그린. 콘솔 에러 0.
- [ ] 기존 Dreamcatcher/Dreamstone PlayMode 8종 회귀 없음.
- [ ] EditMode CatalogSync/DeckRules/DcTrigger 그린.
