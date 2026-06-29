# 1 — AttackSystem fire 재구성 (START / RESOLVE)

## 목적

hit-delay 동작 활성화. `AttackSystem` fire 블록을 **START(공격 시작)** 와 **RESOLVE(타격 판정)** 로 분리하고 `hitDelayRemaining` tick 추가. `hitDelaySec=0` 은 현행과 동일.

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/AttackSystem.cs` — fire 블록(이전 `if (bestTarget && cooldown<=0) { ... }`).

## 구현

- **지연 중**(`hitDelayRemaining > 0`): tick(`-= dt`, 0 clamp). 만료한 프레임에 `doResolve=true`(재판정된 bestTarget로 RESOLVE). 지연 중엔 새 공격 START 안 함.
- **START**(지연 아님 + 쿨다운 0 + 타겟): 애니메이션(`UnitAttackVisualEvent`) + 쿨다운 리셋 + (적·StopToAttack)이동정지 + `hitDelaySec>0` 면 `hitDelayRemaining=hitDelaySec`, `<=0` 면 `doResolve=true`(즉시).
- **RESOLVE**(`doResolve && bestTarget`): damageMul + outputs(데미지/heal/stat/stack) **또는** 투사체 스폰 + 넉백. (애니/쿨다운/이동정지는 START 로 이동.)
- 쿨다운 기산=START, 타겟=RESOLVE 시점 재판정. `hitDelaySec=0` → START+RESOLVE 같은 프레임 = **byte-동일(무회귀)**.

## 완료 기준

- compile 0 · EditMode 26 무회귀.
- Play: `hitDelaySec=0` 전투 정상(데미지 적용) · `hitDelaySec>0` 시 애니메이션 후 `hitDelayRemaining` tick 동안 데미지 보류, 만료 시 타격.

---

확인: 2026-06-29 · compile 0 · EditMode 26/26.
Play(더미 guardian): hd=0 적 즉시 타격으로 guardian Health 1000→587→360(전투 정상=무회귀). hd=3 세팅 적 `hitDelayRemaining=1.57`(3초 윈드업 중, 데미지 보류) — 지연 메커니즘 확인.
