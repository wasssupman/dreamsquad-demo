# 1 — 배치 스킬: OnPlaceEffectType.StunNearby 변종 (착지 충격)

## 목적

배치 순간 반경 내 적 전원 넉업(심 = Stun). `BindNearby` 미러. 호핑 연출은 unit 3 이
이 경로에 뒤에서 붙인다 — 이 unit 은 심만.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `OnPlaceEffectType.StunNearby` enum 멤버(맨 뒤)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — on-place 체인 분기 1개 + 호핑 구동
- `Assets/_Project/Tests/EditMode/` — on-place 케이스 추가

## 구현

1. 분기: `onPlaceRange` 내 적 수집(BindNearby 공간 질의 재사용) → 각 대상에
   `EnemyCcEvent { kind = Stun, remainingTime = onPlaceDuration }` enqueue.
2. enum 멤버 맨 뒤 추가 = 기존 에셋 무형 롤아웃.
3. 병행 spec 주의: bleed-fighter 가 `ApplyStackNearby`, beam-ranger 가 `DotNearby` 를 같은 enum 에
   추가한다 — **커밋 순서 무관하게 각자 맨 뒤에 추가**하고, 충돌 시 나중 커밋이 번호만 재조정한다
   (멤버 이름 기준 병합, 값 재사용 금지).
4. EditMode 케이스: 반경 필터·Stun duration 일치.

## 완료 기준

- [ ] compile clean + 신규 EditMode 케이스 green (Play 확인은 unit 2 에셋 이후)
