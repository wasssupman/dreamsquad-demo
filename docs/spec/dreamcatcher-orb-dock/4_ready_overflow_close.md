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
- **바깥 탭 닫기 (Play 세션으로 이월)**: 손패 뒤 전체화면 캐처 → 빈 영역 탭 시 `Close`.
  손패 backing 은 이미 "cancel region"(드래그 취소, DreamcatcherHandView.cs:822)이라 카드 드래그
  ·드래그취소와의 상호작용 판별이 **Play 검증 필수**. 델리케이트한 손패 시스템에 미검증 상호작용
  코드를 넣지 않는다 — unit 5 Play 세션에서 구현+검증. (현재도 항아리 재탭 → Close 경로 존재.)

## 완료 기준

- **오버플로우**: compile 그린, 상한에서 킬 지속 시 골드 림 플래시(Play/실기 육안).
- **닫기**: unit 5 Play 세션 — 바깥 탭 닫기가 카드 드래그·드래그취소를 깨지 않고, gap 오발이
  수용 가능한지 라이브 확인 후 구현.
