# 4 — 연출 코어 (정합·fly-out)

## 목적

unit 3 의 정적 배열을 **correctness-critical 연출**로 바꾼다. 이 단계는 계약 3(정착 12장 == 캐시 순서)과 흐름 종결(fly-out → `BeginPlacementPhase`)을 검증 가능한 최소 형태로 확립한다. 화려한 셔플/임팩트 juice 는 unit 5 로 분리(critic m5).

## 변경 대상

- `Assets/_Project/Scripts/UI/Dreamcatcher/GiftPhaseView.cs` — 시퀀스 로직.
- 참고: `DraftCardFanView`(`Tween.UIAnchoredPosition` arc), `PlacementPhaseView.SetStartJuice`(scale pulse), `AwakeningGaugeView`(우하단 `-40,220` 좌표 관례).
- 라이브러리: **PrimeTween**(`using PrimeTween;`), `Sequence.Create().Group().OnComplete()`, `useUnscaledTime: true`.

## 구현

`GiftConfig` 타이밍으로 구동하는 단일 `Sequence`(코어 구간만):

| 구간 | 내용 | 기법 |
|---|---|---|
| 4-1 | "X의 선물" 텍스트 등장→소멸 | scale/fade in→out, `introTextSec` |
| 4-2a | 보유 10장 등장 | 순차 스폰 + scale/pos, `baseCardsInSec` |
| 4-2b | `giftAppendDelaySec` 대기 후 선물 2장 덱 뒤 배열 | arc in, `giftAppendSec` (임팩트 플래시는 unit 5) |
| (착지) | 확정 12장 최종 배열 = **캐시 순서 그대로** | 위치 = ordered12 인덱스 매핑 |
| 4-4 | 확인 홀드 | `holdSec` |
| 4-5 | 12장이 각성 버튼으로 fly + scale→0 | `Tween.UIAnchoredPosition`(고정 좌표) + `Tween.Scale(0)`, `flyOutSec` |
| 4-6 | `OnComplete` → `PlacementPhaseView.BeginPlacementPhase()` | — |

세부:
- **정착 순서는 unit 1 캐시(ordered12) 그대로** — 연출은 표현만, 순서를 재생성하지 않는다.
- **fly 타깃 = 고정 스크린 좌표**(critic m4): `AwakeningPanel` 은 배치 전까지 `SetActive(false)` 라 rect 해석 취약. 각성 버튼 앵커(우하단 `-40,220`, `sizeDelta 250×96`)에 대응하는 좌표를 `GiftPhaseView` 캔버스 로컬로 직접 계산(`RectTransformUtility`/앵커 산식). active 상태 무관.
- 모든 트윈 `useUnscaledTime: true`.
- 이 단계 셔플 구간(4-3)은 **단순 재배열**(즉시 또는 짧은 이동)로 대체 — 촤라락 비주얼은 unit 5.

## 완료 기준

- [ ] 컴파일 통과, `read_console` 에러 0.
- [ ] Play: 4-1→4-2→(착지)→4-4→4-5→4-6 전 구간 진행, 배치 정상 진입.
- [ ] **정착 착지 12장 == unit 1 캐시 순서**(수동/로그 대조) — 연출이 순서 왜곡 없음(계약 3).
- [ ] fly-out 이 각성 버튼 위치로 수렴 + scale 0(고정 좌표, 패널 inactive 여도 정상).
- [ ] 타이밍이 `GiftConfig` 값으로 조절(하드코딩 없음).
- [ ] 슬로모/timeScale 상황에서도 정상(`useUnscaledTime`).
