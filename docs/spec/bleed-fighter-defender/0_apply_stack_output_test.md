# 0 — outputs ApplyStack 경로 회귀 테스트

## 목적

난도질꾼이 처음 밟게 될 `AttackSystem` RESOLVE 의 `AttackOutputKind.ApplyStack` 분기
(`AttackSystem.cs:1170` 부근)를 실사용 전에 고정한다. 체인의 나머지(큐→슬롯→임계→DoT→데미지)는
2026-07-29 리그 실측 green — 이 구간만 사용 유닛 0 + 테스트 Ignored 스텁 상태다.

## 변경 대상

- `Assets/_Project/Tests/PlayMode/DefenderApplyStackOutputTest.cs` (신규)

## 구현

`DreamcatcherOnHitTest`(PlayMode) 하네스를 미러하되 **카드 없이** 구동한다:

1. Setup·PlaceGuardian·SpawnDummyEnemy 헬퍼를 복제/재사용. 단 defender 는 카탈로그 유닛이 아니라
   테스트용 `DefenderUnitData` 인스턴스(`ScriptableObject.CreateInstance`)로 만들어
   `outputs = [Damage, ApplyStack(Bleed, mag 1, duration 4, max 5)]` 를 직접 저작 — 유닛 에셋(unit 2)에
   선행 의존하지 않는다.
2. 단언 2개:
   - 적이 `StackModifierSlot(kind=Bleed)` 를 얻는다 (ember 테스트와 동일 폴링).
   - 이후 적 `CcEffect(kind=DoT)` 가 생긴다 — 임계 발화까지 이 테스트에서 관측
     (ember 테스트가 안 덮는 반 발짝을 마저 덮는다).
3. `ModifierFrameworkTests.AttackOutput_AllFourKinds_EnqueueToCorrectChannels` Ignored 스텁은
   건드리지 않는다 — full combat world 요구 사유가 그대로이며, 본 테스트가 실질 커버를 대신한다는
   주석 한 줄만 스텁에 추가.

## 완료 기준

- [ ] 신규 PlayMode 테스트 green (리그 배치 실행 가능)
- [ ] mutation 확인 1회: `case ApplyStack` 의 enqueue 를 주석 처리하면 테스트가 실제로 실패하는지 (검출력 증명 후 원복)
