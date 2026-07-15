# 0 — SelfBuffLethal 부착 사전검증

## 목적

이미 `LethalTimer`가 있는 호스트에 두 번째 `SelfBuffLethal` 카드가 붙어 기존 사망 시한을 덮어쓰는 것을 막는다. 복합 카드의 일부 효과만 적용되는 실패도 함께 방지한다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
- `Assets/_Project/Tests/PlayMode/DreamcatcherCursedRelicTest.cs`

## 구현

`ApplyDreamcatcherCardToUnit`에서 defender 유효성 확인 후 mechanic을 적용하기 전에 다음을 검사한다.

1. 카드에 `SelfBuffLethal`이 있고 defender에 `LethalTimer`가 이미 있으면 경고 후 `-1`을 반환한다.
2. 검사 전에 modifier enqueue, component 추가, trigger buffer 변경을 하지 않는다.
3. Controller의 기존 `handle < 0` 계약을 사용해 비용 차감과 카드 순환을 막는다.

현재 메서드 안의 짧은 사전순회로 구현한다. 별도 검사 API, 인터페이스, 컴포넌트나 시스템은 만들지 않는다.

## 완료 기준

- [ ] 기존 `LethalTimer` 호스트에 SelfBuffLethal 복합 카드를 붙이면 `-1`을 반환한다.
- [ ] 실패 전후 타이머 값, 공격속도, `DcTriggerSlot` 상태가 동일하다.
- [ ] 실패한 Controller 부착은 비용·손패·부착 레지스트리를 바꾸지 않는다.
- [ ] 타이머가 없는 호스트와 SelfBuffLethal이 없는 기존 카드 동작은 유지된다.
- [ ] compile clean, 관련 PlayMode 테스트 green.
