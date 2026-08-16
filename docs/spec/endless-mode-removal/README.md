# endless-mode-removal — 엔드리스 모드 제거

> 상태: **완료 2026-08-16** (자동 검증 통과 · dev 패널 육안 확인만 대기)
> 선행: `three-minute-kill-race`(완료) — 그 spec 이 이 결정을 불러왔다
> 폐기 대상 spec: `docs/spec/endless-mode/`

## 왜 지금인가

`three-minute-kill-race` 가 본 모드를 「3분 고정 · 패배 없음 · 유저 제출」로 바꾸면서
엔드리스와의 차이가 사라졌다. 사용자 결정(2026-08-16): **제거.**

## 조사 결과 — 엔드리스는 「무한 모드」가 아니었다

지우기 전에 실물을 확인했고, 이름과 실제가 달랐다:

- **`Deck_Endless.asset` 의 `timerDurationSec` 는 180 이다.** 다른 덱과 같다.
  타이머를 끈 적이 없으므로 **판 길이가 무한이었던 적이 없다.**
- `_timerDuration <= 0`(진짜 무한)은 **저작 플랜(`WavePlanAsset`) 전용 경로**이고
  `BattleMode.Endless` 와 무관하다. 그 데이터 의미는 **그대로 둔다.**

실제로 `BattleMode.Endless` 가 하던 일은 **두 가지뿐**이었다:

| 실물 | 하는 일 |
|---|---|
| `AttackDeck.battleMode == Endless` → `BattleBridge.IsEndless` | **토너먼트 리포트 스킵** 하나 |
| `DevMapOverride.Endless`(PlayerPrefs) + dev 패널 슬롯 | 전용 `endlessEncounter`(맵+덱) 강제 진입 |

즉 «토너먼트에 안 올라가는 개발용 덱 + 그리로 가는 dev 스위치» 다. 그래서 제거 범위가
모드 로직이 아니라 **진입 경로와 저작 자산**이다.

> ⚠ `three-minute-kill-race` unit 0 문서에 «엔드리스는 `_timerDuration <= 0` 이라 스스로
> 안 끝난다» 고 적었는데 **사실이 아니다**(180초로 정상 만료된다). 이 조사에서 정정했다.

## 작업 단위

| # | 문서 | 작업 구분 | 목적 |
|---|---|---|---|
| 0 | `0_remove.md` | 데이터 + 브리지 + UI + 테스트 | 진입 경로·저작 자산·모드 축 일괄 제거 |

한 커밋이다 — `BattleMode` enum 을 지우면 `AttackDeck` · 브리지 · dev 패널 · 테스트가 **동시에**
컴파일이 깨지므로 중간 커밋이 성립하지 않는다(`gift-phase-removal` units 1~3 선례).

## 계약

- **`timerDurationSec = 0`(저작 플랜의 무한) 의미는 유지한다.** 엔드리스 모드와 다른 축이고,
  테스트 모드 저작이 쓰는 데이터 knob 이다.
- **맵 풀 선택은 byte-identical 로 유지된다.** 엔드리스는 풀 count 를 안 건드리는 별도 분기라
  제거해도 랜덤/토너먼트 맵 배정이 바뀌지 않는다(구 계약 5 그대로).
- `DevMapOverride.Index`(맵 인덱스 강제)와 dev 슬롯은 **남긴다** — 엔드리스와 별개 기능이다.
  스텝 사이클에서 ENDLESS 슬롯 하나만 빠진다.
- `docs/spec/endless-mode/` 는 **지우지 않고 폐기 표시**한다(역사).

## 후속 후보

- **`BattleMode` 재도입 시 주의** [S] · 다시 모드 축이 필요해지면 enum 을 되살리기 전에
  «그 모드가 실제로 무엇을 다르게 하는가» 를 한 줄로 적을 것. 이번 것은 그 답이
  「토너먼트 리포트 스킵」 하나였고, 그건 모드가 아니라 플래그로 충분했다.
