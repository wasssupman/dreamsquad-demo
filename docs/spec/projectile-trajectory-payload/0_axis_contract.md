# 0 — Axis Contract (궤적/페이로드 토대)

## 목적

투사체를 궤적 × 페이로드 두 축으로 분해하기 위한 **데이터 토대**를 놓는다. 이 unit 은 enum discriminator 와 컴포넌트 필드만 추가한다. 실제 switch 소비는 unit 1 이후. **additive** 이므로 기존 홈잉 경로는 코드 수정 없이 그대로 컴파일·동작한다(신규 discriminator default = 홈잉/단일).

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Combat/Projectile/MovementKind.cs`
- 신규 `Assets/_Project/Scripts/Battle/Combat/Projectile/PayloadKind.cs`
- 수정 `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileState.cs`
- 수정 `Assets/_Project/Scripts/Battle/Combat/Projectile/ProjectileSpawnRequest.cs`

`BattleBridge.cs` / `AttackSystem.cs` 는 **건드리지 않는다** — 신규 필드가 default 0 이라 기존 `SpawnProjectile`/RESOLVE 스테이징이 홈잉으로 그대로 동작.

## 구현

- `enum MovementKind : byte { HomingToEntity = 0, BallisticArcToPoint = 1 }` — 궤적 축. default(0)=홈잉.
- `enum PayloadKind : byte { SingleSplash = 0, TileAoe = 1 }` — 페이로드 축. default(0)=기존 단일+splash.
- `ProjectileState` 필드 재구성(기존 필드 보존 + 축별 필드 추가):
  - discriminator: `movement`, `payload`
  - 공통: `damage`, `dataIndex`
  - Homing 궤적: `target`, `speed`, `hitThreshold`
  - BallisticArc 궤적: `origin`, `impact`, `flightTime`, `elapsed`, `arcHeight`
  - SingleSplash 페이로드: `onHitEffect`, `splashRadius`, `splashDamageMul`
  - TileAoe 페이로드: `impactTileRange`
- `ProjectileSpawnRequest` 에 대응 필드 추가: `movement`, `payload`, `impact`, `arcHeight`, `impactTileRange` (기존 `target`/`origin`/`damage`/`speed`/`hitThreshold`/`visualScale`/`dataIndex`/splash 필드 보존). `flightTime`/`elapsed` 는 drain 시 산출/초기화하므로 request 에 없음.
- 단일 struct + discriminator 방식(축별 별도 컴포넌트 아님) — 기존 ProjectileState 가 splash 필드를 일부만 쓰는 선례와 동형. Burst 쿼리 단순성 우선.

## 완료 기준

- [x] 신규 .cs 추가 후 refresh **scope=all** → 컴파일 에러 0 (cascading CS0246 방지).
- [x] 기존 홈잉 투사체 동작 무변경(런타임 검증은 unit 1 에서, 여기선 compile-clean 만).
- [x] `git diff` 가 대상 파일만 (BattleBridge/AttackSystem 무변경 확인).

완료 확인: 2026-07-06 — 컴파일 0 에러, diff 격리 확인. unit 1 과 동일 커밋.
