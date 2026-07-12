# 0 — 페이즈 모델 (additive)

## 목적

선물 페이즈의 **토대 타입만** 추가한다. `GamePhase.Gift` enum 값, `GiftKind` enum, `GiftConfig` SO. 이 단계는 **순수 additive** — 동작/라우팅 변경 0, 컴파일·기존 흐름 무회귀. 실제 라우팅 hand-off 는 GiftPhaseView 가 존재하는 unit 3 에서 한다(critic M2: unit 0 이 구독을 끊으면 뷰 부재 구간에 flow dead).

## 변경 대상

- `Assets/_Project/Scripts/Core/GameManager.cs` — `GamePhase` enum(line 10)에 `Gift` 추가.
- (신규) `Assets/_Project/Scripts/Data/Dreamcatcher/GiftConfig.cs` — `[CreateAssetMenu]` SO + `GiftKind` enum.
- (신규) `Assets/_Project/Data/Dreamcatcher/GiftConfig_Default.asset`.

## 구현

1. `enum GamePhase { None, Draft, Placement, Battle, Result }` → **`Gift` 를 `Placement` 앞에** 삽입: `{ None, Draft, Gift, Placement, Battle, Result }`.
   - enum 값 삽입은 직렬화 정수 순서를 바꾼다 — 씬/에셋에 `GamePhase` 를 **직렬화하는 필드가 있는지** 확인(대개 런타임 상태라 없음). 있으면 append(끝에 추가)로 전환 고려. 없으면 논리적 위치(Placement 앞)가 가독성상 우수.
2. `GiftKind { Lucid, Rim }` enum.
3. `GiftConfig` SO 필드(placeholder 값, 하드코딩 금지):
   - 이벤트: `float lucidWeight = 1f`, `float rimWeight = 1f`
   - 연출 타이밍(unit 4/5 소비): `float introTextSec`, `float baseCardsInSec`, `float giftAppendDelaySec`, `float giftAppendSec`, `float shuffleSec`, `float holdSec`, `float flyOutSec`
   - `bool fastForwardInTestMode = true`
4. 기존 phase view 들의 `OnPhaseChanged` 가 `phase == Placement` 등 **명시 비교**인지 확인 — `Gift` 를 만나도 자기 페이즈 아님으로 안전히 숨겨지는지 점검(대부분 안전, 예외 발견 시 여기서 방어).

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] 기존 Draft/Squad/Test/Restart 흐름 **무회귀**(이 단계는 동작 변경 없음 — 기존과 동일하게 배치까지 도달).
- [ ] `GamePhase.Gift` 삽입 후 기존 phase view 에서 enum 관련 경고/오류 0.
- [ ] `GiftConfig_Default.asset` 생성·기본값 세팅, 로드 성공.
