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
| Runner | Stop / 0 | **Advance** | 과속 컨셉 — 이동사격 확정(2026-06-30) |
| Swift | Stop / 0 | **Halt** | 빠른 근접 — 멈춤 확정(2026-06-30) |
| Tanker | Stop / 0 | **Halt** | 육중, 멈춤 |
| Rootcaster | Stop / 1.0 | **Halt** | 기존 정지 동작 유지 |
| Sniper | Stop / 0.5 | **Halt** | 기존 정지 동작 유지 |
| Debuffer | Move / 0 | **Advance** | 이동사격 |
| Needler | Move / 0 | **Advance** | 이동사격 |

> Runner/Swift 는 콘텐츠 결정으로 확정(2026-06-30): Runner=Advance(과속 이동사격), Swift=Halt(빠른 근접·멈춤). 위 표가 source of truth.

## 완료 기준

- 9종 SO 모두 `engageMovement` 명시값 보유, stale `aimMode`/`movePauseOnAttackSec` 키 없음.
- Vanguard: Play 에서 디펜더 사거리 진입 시 정지+공격, 디펜더 사망 시 행진 재개 육안 확인.
- Advance 적(Debuffer/Needler): 이동하며 공격 육안 확인.

---

✅ **데이터 마이그레이션 완료 2026-06-30** — 9종 engageMovement 명시값 설정(Advance: Debuffer/Needler/Runner · Halt: Vanguard/Basic/Tanker/Rootcaster/Sniper/Swift). Runner=Advance·Swift=Halt 사용자 확정. stale `aimMode`/`movePauseOnAttackSec` 키 9종 전부 제거(재직렬화는 기본값 스키마 필드만 기록, 게임플레이 수치 변동 0). 독립 리뷰 APPROVE(매핑 9/9·stale 제거·WIP 무혼입·문서 일관성).
> ⏳ 완료 기준 2·3(Vanguard 정지/공격·Advance 이동사격 **Play 육안 검증**)은 unit 5 에서 수행.
