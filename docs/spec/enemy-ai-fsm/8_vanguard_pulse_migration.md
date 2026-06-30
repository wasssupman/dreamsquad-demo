# 8 — Vanguard Pulse 전환 + Play 검증

## 목적

Vanguard 를 진동형(Pulse)으로 전환하고, 진동이 보이도록 `hitDelaySec` 값을 부여한다. 비어그로 시 걸어 들어오며 공격(B), 어그로 시 가디언 앞 캠프(A)를 라이브로 확인한다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_Vanguard.asset`

## 구현

- `engageMovement = Pulse` (2)
- `hitDelaySec = 0.3` (현재 0 → 진동 정지 구간 확보. 동시에 공격 텔레그래프로 작동)

> 멈춤 시간 = 타격지연으로 묶임(option a). 추후 분리 필요 시 `attackMotionSec` 별도화(별도 spec).
> 다른 Halt 적(Basic/Tanker/Sniper/Rootcaster)은 이번 범위 밖 — 원거리 캠퍼는 Halt 유지가 적절. Pulse 확대는 콘텐츠 결정으로 후속.

## 완료 기준

- Vanguard SO: `engageMovement=Pulse`, `hitDelaySec=0.3` 직렬화 확인.
- Play 육안(에디터 **포커스**):
  - 비어그로: Vanguard 가 디펜더에게 걸어 들어가며 공격(제자리 캠프 아님), 붙으면 제자리 연타.
  - 어그로: 가디언 추격→사거리 도달 시 **정지** 공격(캠프), 가디언 사망 시 행진 복귀.
  - 콘솔 에러/leak 0.

---

✅ **데이터 마이그레이션 완료 2026-06-30** — Enemy_Vanguard.asset `engageMovement: 0→2`(Pulse), `hitDelaySec: 0→0.3`. diff 클린(타 게임플레이 값 변동 0). 메커니즘은 unit 7(MovementSystemTests 15/15 PASS)이 잠금.
> ⏳ **라이브 육안 검증 미완**: 완료 기준 Play(비어그로 pulse 전진 / 어그로 camp)는 에디터 **포커스** Play 필요 — 기반 FSM unit 5 검증과 함께 사용자 일괄 확인 대기.
