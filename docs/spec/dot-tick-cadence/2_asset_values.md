# 2 — 캐스터 존 에셋 값 확정 + Play 검증

## 목적

Fire/Poison 1x1 존을 이산 tick 수치로 확정하고, 실제 플레이에서 폰트가 주기당 1회(정수)로 뜨는지 검증한다.

## 변경 대상

- `Assets/_Project/Data/Hazards/Hazard_Fire_1x1.asset`
- `Assets/_Project/Data/Hazards/Hazard_Poison_1x1.asset`

## 구현

`effects[0]`:

| 에셋 | param1 (tick당 데미지) | tickInterval | restDuration |
|---|---|---|---|
| Hazard_Fire_1x1 | **10** | **0.5** | 0.2 (유지) |
| Hazard_Poison_1x1 | **20** | **1.0** | 0.2 (유지) |

> 현재 두 에셋 모두 param1=20/tickInterval 없음(연속) 상태. 본 단위에서 Fire param1 20→10 + tickInterval 0.5, Poison tickInterval 1.0 추가로 확정.

lifetime(6s)·cast 쿨다운(Fire 3.5s/Poison 4s)·range(4)는 불변.

## 완료 기준

- [x] 인스펙터에서 두 에셋의 param1/tickInterval 확인
- [x] Play: 화염 청크 "10" / 독 청크 "20" 정수 폰트만. "1" 스팸 소멸 (배틀로그 dot_damage amount 분포 {10:15, 20:5})
- [x] Play: 머무는 적 Fire 0.5s 정확(gaps 0.484~0.503). 이중발동 없음(동시타=서로 다른 적)
- [x] 첫 tick 진입 즉시(즉발) — 지나가는 적은 접촉당 풀 청크(사용자 의도 확인: "존 트리거→유닛 즉발→인터벌 페이싱")
- [x] 사용자 Play 확인 완료

> 확인 2026-07-18 · 커밋 aedcb66f · Play 배틀로그(session-20260718-102521) 검증. 콘솔 에러 0
