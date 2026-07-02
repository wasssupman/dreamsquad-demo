# Pilot Arrow Wiring + A/B 검증

**작업 구분**: 2

## 목적

유닛 0/1 을 실제 게임에 연결한다. 스트립된 GA Arrow 를 side-by-side 신규 ProjectileData 로 와이어링하고, GA 데모 씬과 A/B 스크린샷으로 as-is 외형을 검증한다(= spec 검증 질문 응답).

## 변경 대상

- New: `Assets/_Project/Data/Projectiles/Projectile_Arrow_GA.asset` (기존 `Projectile_Arrow` 복제 후 수정)
- 검증 배선: Archer `DefenderUnitData.projectile` → `Projectile_Arrow_GA` (side-by-side 라 되돌리기 안전)
- (필요 시) 유닛 0 툴로 hit/muzzle 프리팹도 스트립

## 구현

- `Projectile_Arrow_GA.asset`:
  - `projectilePrefab` = `Assets/_Project/VFX/Projectiles/GA/vfx_Projectile_Arrow01.prefab` (스트립본)
  - `hitPrefab` = `vfx_Hit_Arrow01`, `castPrefab` = `vfx_Muzzle_Arrow01` (GA 원본. PlayHit/PlayCast 는 Instantiate+auto-despawn 이라 무버 불필요)
  - `facing` = `AlongVelocity`. `speed`/`hitThreshold` 는 기존 `Projectile_Arrow` 값 유지(궤적 밀도가 데모와 다르면 speed 조정은 후속).
  - `visualScale` 튜닝: GA 데모 스케일 ↔ board 스케일 정합(첫 Play 에서 육안 조정).
- hit/muzzle 프리팹이 `Rigidbody`/`Collider`/무버를 포함하면(스폰 위치서 드리프트하면) 유닛 0 툴로 스트립 후 `GA/` 산출본 참조로 교체.
- 검증 배선: Archer 의 projectile 필드를 임시로 `Projectile_Arrow_GA` 로. (SaveScene 은 in-memory WIP 을 베이크하므로, 씬 저장 격리 원칙 준수 — 배선 검증은 in-memory reflection 기법 우선.)

## 완료 기준 (= spec 검증 질문)

- BattleScene Play(에디터 포커스) → Archer 발사 → **비행 화살 + 임팩트** 스크린샷.
- GA 데모 씬(`GabrielAguiarProductions/UniqueProjectilesVol_4/Scenes/…`) 에서 Arrow01 스크린샷 = 레퍼런스.
- A/B 육안 "같은 외형"(궤적·코어·트레일·임팩트). streak 없음, 콘솔 Error/Warning 0, GameObject 누수 없음.
- 통과 확인 후 이 파일 하단에 확인 일자 + 커밋 해시 추가하고 커밋.

진행 2026-07-03 — `Projectile_Arrow_GA` 생성(proj=스트립 GA애로우, hit=vfx_Hit_Arrow01, cast=vfx_Muzzle_Arrow01, preserveVfxColors=true). `Defender_Archer.projectile` → GA 배선. Play 직접 비행 테스트로 트레일 유지+native 색 vivid 확인(스크린샷). **실게임 전투(Archer 발사 via ViewPool) 육안 확인 대기** — 사용자 플레이 세션에서 확정.
