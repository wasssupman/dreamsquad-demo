# 4 · ready / 오버플로우 / 닫기

## 목적

상태 신호 3종을 완성한다: (a) ready(affordability) — unit 1 의 rim 이 이미 구동, (b) 오버플로우
낭비 경고 — 상한에서 획득분 소멸 시 시끄러운 손실 신호, (c) 손패 바깥 탭 닫기.

## 변경 대상 / 진행

- **오버플로우 (완료)**: `DreamcatcherHandController.cs` — `AwakeningOverflowed(int lost)` 이벤트
  추가, `GainAwakening` 이 상한에 막힌 손실량을 발화(Mono 전용, ECS 무관). `AwakeningGaugeView.cs`
  — 구독 → `OverflowFlashRoutine`(골드 림 3회 감쇠 깜빡임 + 짧은 통 흔들림). 상시 pulse 금지
  계약과 달리 이벤트 반응이라 허용.
- **ready (완료, unit 1)**: `Gauge ≥ 최저 코스트` 시 rim 발화. 이번 유닛의 신규 작업 아님.
- **바깥 탭 닫기 (완료)**: `DreamcatcherHandView._dismissCatcher` — 손패 뒤 전체화면 캐처를
  카드(`_panel`)보다 낮은 sibling 으로 두어 카드·backing(드래그 취소) 입력은 안 가로채고 손패
  패널 바깥(보드) 탭만 `Close`. Open/Close/ForceClose 에서 토글. 항아리 독·NextWaveDock(order7)은
  이 order5 캐처 위에서 정상. 항아리 재탭 → Close 도 유지.

## 완료 기준

- **오버플로우**: compile 그린. 라이브 하네스 — 패널 활성 시 `GainAwakening@max` →
  `OverflowFlashRoutine` 기동, 숨김(Result 등)엔 가드 억제. 실기 골드 림 플래시 육안만 남음.
- **닫기**: 라이브 배틀 하네스 — State UnitStrip→Hand→(캐처클릭)→UnitStrip, 캐처 active
  True→False. gap 오발/실기 그립 육안은 후속.
