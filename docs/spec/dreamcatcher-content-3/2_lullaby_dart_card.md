# 2 — 자장가 (lullaby_dart): AttackN × Sleep 온-히트

## 목적

N번째 공격이 적을 재우는 카드. 적 수면은 실드수면(AreaSleep → `EnemyCcEvent{Sleep}`)으로 소비·표현 경로가 검증돼 있고, 수면은 wake-on-hit 사양이라 **다른 유닛이 때리면 깬다** → 광역 유닛과의 상충이 카드의 리스크. 코드는 정의 계층 enum append + 번역/문안 case 뿐.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — `DcCcKind` 에 `Sleep` append (append-only 계약)
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — `MapDcCc` 에 Sleep→`CcKind.Sleep` case
- `Assets/_Project/Scripts/UI/Dreamcatcher/DreamcatcherCardText.cs` — ccKind 문안 switch 에 `Sleep → "수면"` case
- `Assets/_Project/Data/Dreamcatcher/Card_LullabyDart.asset` (신규) + 카탈로그 등록

## 구현

- `DcCcKind { Stun, Impulse, Sleep }` — 기존 카드 에셋은 int 직렬화라 뒤에만 붙인다.
- `AttackSystem` 의 ApplyCcToTarget arm 은 무수정: Impulse 만 벡터 특례고 나머지 ccKind 는 `CcEffect { kind, remainingTime }` 로 그대로 enqueue — Sleep 은 duration 만으로 동작.
- `Card_LullabyDart.asset`: id `lullaby_dart` · displayName `자장가` · axis All · type Unit
  - mechanics[0]: trigger `{ AttackN, period: 5 }` / payload `{ ApplyCcToTarget(10), ccKind: Sleep(2), duration: 2.5 }`
  - 강한 CC 라 period 는 빙결(3)보다 길게 — 초안 5. duration 2.5초는 wake-on-hit 이라 실효 지속이 더 짧은 점을 감안한 값 (Play 튜닝 대상).
  - description: `5번째 공격마다 → 대상 수면 2.5초 (피격 시 깨어남)`
- 확인 항목: 적측 Sleep 소비가 실드수면 경로(광역)와 단일 대상 경로에서 동일하게 동작하는지 — `EnemyCcEvent` 는 대상 단위라 차이가 없어야 정상.

## 완료 기준

- [x] compile 클린 + EditMode 전체 green
- [x] `DreamcatcherCardTextTests` 에 Sleep 문안 케이스 1개 추가·통과
- [ ] Play smoke: 5번째 공격에 적이 잠들고, 다른 유닛의 공격에 즉시 깨는 것 확인

구현 커밋 79d9f844 (2026-07-25). 문안은 "(피격 시 해제)" 명시로 확장. Play smoke 대기.
