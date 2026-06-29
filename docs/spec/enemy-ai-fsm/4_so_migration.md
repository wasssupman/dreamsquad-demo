# 4 — 적 SO 9종 engageMovement 마이그레이션

## 목적

레거시 `aimMode` 제거 후, 각 적 SO 에 새 `engageMovement` 값을 명시 세팅한다. 0 의 임시 파생(aimMode→engageMovement)을 SO 직접 값으로 확정한다.

## 변경 대상

- `Assets/_Project/Data/Enemies/Enemy_*.asset` (9종).

## 구현

기존 `aimMode` 매핑 + movePause 모순 해소(StopToAttack 인데 movePause=0 이던 적은 이제 `Halt` 로 정상 정지):

| 적 | 기존 aimMode / movePause | 새 engageMovement | 비고 |
|---|---|---|---|
| Vanguard | Stop / 0 (안 멈춤 버그) | **Halt** | 멈춰서 공격(의도) |
| Basic | Stop / 0 | **Halt** | 표준 근접, 멈춤 |
| Runner | Stop / 0 | (확정 필요) | 과속 컨셉 — Halt/Advance 사용자 결정 |
| Swift | Stop / 0 | (확정 필요) | 빠른 근접 — 사용자 결정 |
| Tanker | Stop / 0 | **Halt** | 육중, 멈춤 |
| Rootcaster | Stop / 1.0 | **Halt** | 기존 정지 동작 유지 |
| Sniper | Stop / 0.5 | **Halt** | 기존 정지 동작 유지 |
| Debuffer | Move / 0 | **Advance** | 이동사격 |
| Needler | Move / 0 | **Advance** | 이동사격 |

> Runner/Swift 의 Halt vs Advance 는 콘텐츠 결정 — 구현 단계 진입 시 사용자 확정. 기본값은 Halt(안전: 기존 aimMode=Stop 라벨 존중).

## 완료 기준

- 9종 SO 모두 `engageMovement` 명시값 보유, stale `aimMode`/`movePauseOnAttackSec` 키 없음.
- Vanguard: Play 에서 디펜더 사거리 진입 시 정지+공격, 디펜더 사망 시 행진 재개 육안 확인.
- Advance 적(Debuffer/Needler): 이동하며 공격 육안 확인.
