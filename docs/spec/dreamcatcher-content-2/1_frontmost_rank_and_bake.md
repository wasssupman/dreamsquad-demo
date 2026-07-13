# 1 — Frontmost 랭킹 순수 helper + Bridge per-kind bake

## 목적

`끝을 보는 눈`의 두 토대를 만든다: (a) "최전방" 선택을 결정하는 **아키텍처-blind 순수 랭커**와 그 EditMode 테스트, (b) Bridge의 attackMod 검증을 **kind별로 분기**해 `FrontmostTarget`(count 미사용)을 정상 bake하고 부착 자격(양수 Damage output)을 강제. 아직 AttackSystem에 배선하지 않는다(unit 2).

## 변경 대상

- `Assets/_Project/Scripts/Battle/Combat/FrontmostTargeting.cs` — **신규** 순수 static helper.
- `Assets/_Project/Tests/EditMode/FrontmostTargetingTests.cs` — **신규** EditMode.
- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs` — attackMod 루프 per-kind 분기 + `HasPositiveDamageOutput` helper.

## 구현

### FrontmostTargeting (순수)

- `Candidate { int flowDist; float sqDist; int entityIndex; int entityVersion; }`.
- `RanksBefore(a, b)`: flowDist ↑ → sqDist ↑ → entityIndex ↑ → entityVersion ↑ (README 계약 1).
- `SelectFrontmost(NativeArray<Candidate>, count)`: `flowDist == UnreachableDist(int.MaxValue)` 제외, 최선 index 또는 -1.
- ECS 타입 무의존(int/float/NativeArray만). PastGoal/Dead/mask/사거리 필터는 **호출자(AttackSystem)** 책임 — helper는 unreachable sentinel + 랭킹만.

### Bridge per-kind bake

- 전역 guard `m.count <= 0` 제거 → `None || damageMul <= 0` 만 공통 거절.
- `ProjectileBounce`: `count > 0` + `ProjectileRef` 요구(기존 계약 유지).
- `FrontmostTarget`: `HasPositiveDamageOutput(defender)` 요구(힐러/output없는 caster 거절), `FrontmostAttackLock` 최초 1회 추가(idempotent). `count/tileRange` 무시.
- `HasPositiveDamageOutput`: `AttackOutputElement` 버퍼에 `AttackOutputKind.Damage && magnitude > 0` entry 존재 여부.

## 완료 기준

- [x] compile green (error 0). — 2026-07-14 확인
- [x] `FrontmostTargetingTests` EditMode 6/6 green(rig 배치): flow-dist 우선, sqDist tie, entity idx/version tie, unreachable 제외, 전부 unreachable→-1, 빈 후보→-1. — 2026-07-14
- [x] 기존 EditMode 스위트 무회귀: total 729 / passed 727 / failed 0 / skipped 2(기존 ignored). — 2026-07-14
- [ ] `FrontmostTarget(count=0, damageMul=1.2)` 카드가 실제로 bake됨(과거 전역 guard였다면 skip됐을 것) — unit 2/4 배선 후 재확인.
