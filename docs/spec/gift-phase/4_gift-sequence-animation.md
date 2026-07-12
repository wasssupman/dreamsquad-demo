# 4 — 선물 연출 시퀀스

## 목적

unit 3 의 정적 레이아웃을 발라트로/솔리테어식 카드 연출로 바꾼다. "X의 선물" 텍스트 → 10장 등장 → +2장 임팩트 → 촤라락 셔플 → 확정 12장 순차 배열 → 확인 홀드 → 각성 버튼으로 날아가며 scale→0 → 배치 진입.

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 시퀀스 로직 추가.
- 참고 패턴: `Assets/_Project/Scripts/UI/Draft/DraftCardFanView.cs`(`Tween.UIAnchoredPosition` arc toss·`PlayDiscardCard`), `PlacementPhaseView.SetStartJuice`(scale/aura pulse), `AwakeningGaugeView`(fly 타깃 `AwakeningPanel` RectTransform, 우하단 `-40,220`).
- 라이브러리: **PrimeTween** (`using PrimeTween;`), `Sequence.Create().Group().OnComplete()`, `Ease.*`, `useUnscaledTime: true`.

## 구현

`GiftConfig` 타이밍 필드로 구동하는 단일 `Sequence`:

| 구간 | 내용 | 기법 |
|---|---|---|
| 4-1 | "X의 선물" 텍스트 등장 후 멋지게 사라짐 | scale/fade in → out, `introTextSec` |
| 4-2a | 보유 10장 화면 등장 | 순차 스폰 + `Tween.Scale`/pos, `baseCardsInSec` |
| 4-2b | `giftAppendDelaySec`(≈1s) 대기 후 선물 2장이 임팩트 있게 덱 뒤에 배열 | punch scale + arc in + 플래시, `giftAppendSec` |
| 4-3 | 12장 촤라락 셔플 → 확정 12장 순차 배열 | 카드 위치 반복 스왑/스프레드 후 확정 순서로 재정렬, `shuffleSec` |
| 4-4 | 유저 덱 확인 홀드 | `holdSec`(≈2s) |
| 4-5 | 12장이 각성 버튼으로 날아가며 scale→0 | `Tween.UIAnchoredPosition`(타깃=`AwakeningPanel` RectTransform 화면좌표) + `Tween.Scale(0)`, stagger, `flyOutSec` |
| 4-6 | `OnComplete` → `PlacementPhaseView.BeginPlacementPhase()` | — |

세부:
- **확정 12장 순서는 unit 1 의 캐시 그대로**(연출은 표현만; 순서를 다시 만들지 않는다). 셔플 애니메이션은 시각 효과이고, 착지 순서는 캐시된 확정 순서.
- 모든 트윈 `useUnscaledTime: true`.
- 각성 버튼 화면좌표: `AwakeningPanel` RectTransform → `RectTransformUtility` 로 GiftPhaseView 캔버스 로컬좌표 변환(캔버스 스케일 상이 대비).
- 셔플 연출은 index 기반 결정론 배열(seeded RNG 지양) — 시각이라 엄격하진 않으나 프로젝트 관례상 index 파생 지터 선호.
- Test 모드 fast-forward(`GiftConfig.fastForwardInTestMode`): 시퀀스 스킵/압축 후 즉시 4-6.
- 중단 안전: Gift 도중 페이즈 강제 전환/파괴 시 `Sequence` 정리(leak 방지, PrimeTween handle 보관 후 Stop).

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] Play 로 4-1~4-6 전 구간 시각 확인(스크린샷 다회) — 텍스트 등장/소멸, 10+2 배열, 셔플, 확정 배열, fly-out scale 0, 배치 진입.
- [ ] 확정 착지 12장 == unit 1 캐시 순서(연출이 순서를 왜곡하지 않음).
- [ ] 타이밍이 `GiftConfig` 값으로 조절됨(하드코딩 없음).
- [ ] 슬로모/timeScale 상황에서도 연출 정상(`useUnscaledTime`).
- [ ] 중단/재시작 시 트윈 leak/중복 없음.
