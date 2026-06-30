# 9 — Rootcaster Pulse 전환 + Play 검증

## 목적

Rootcaster 를 Halt 캠퍼에서 **Pulse(진동)** 로 전환한다. Vanguard 와 동일 메커니즘(unit 7) — 비어그로 `Engaging` 에서 쏘며 전진(이동-우세), 어그로 `Standoff` 에서 캠프. 새 필드 없이 SO 값만 변경.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Rootcaster.asset`

## 구현

- `engageMovement = Pulse` (2)
- `hitDelaySec = 0.6` (현재 0 → 진동 정지 구간 확보. Rootcaster 쿨 2.2s 기준 멈춤 비율을 Vanguard(0.3/0.8≈37%)와 유사하게 잡되, 투사체 텔레그래프가 과도하지 않게 0.6(≈27%)으로 절충. 튜닝 가능)

> **사용자 결정(2026-06-30)**: "Vanguard 랑 같은 방식". option a(hitDelaySec 연동) 유지 — attackMotionSec 분리(option b)는 채택 안 함.
> **동작 결과(고지됨)**: 이동-우세라 Rootcaster 가 쏘며 전진 → 시간이 지나면 디펜더 근접까지 접근(원거리 사거리 6 캠프 정체성은 약화). 의도된 선택.

## 완료 기준

- Rootcaster SO: `engageMovement=Pulse`, `hitDelaySec=0.6` 직렬화 확인. 타 게임플레이 값 변동 0.
- Play 육안(에디터 **포커스**):
  - 비어그로: 디펜더 사거리 진입 시 멈춰 발사 후 전진하는 진동 반복(쏘며 접근).
  - 어그로: 가디언 앞 캠프, 가디언 사망 시 행진 복귀.
  - 콘솔 에러/leak 0.
