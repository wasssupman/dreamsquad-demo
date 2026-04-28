# Handoff Summary — Projectile Visual Upgrade

**작성일**: 2026-04-28

## Commits

- `f9a67bc` feat: ProjectileViewPool skeleton + scene wiring (task 1)
- `55c7be5` feat: schema swap + prefab render cutover (task 2)
- `9a6b8d2` feat: facing policy + hit prefab lifecycle (task 3)
- `d04c7ee` feat: tint/jitter/emission variation via MPB (task 4)
- `fc10ba1` feat: editor texture baker + 12 baked variants (task 5)
- `d2e658a` feat: texture variant runtime swap via MPB (task 6)
- `39903fd` feat: Sniper_Crimson demo variant + defender wiring (task 7)
- `ead618e` test: variation EditMode 5/5 + PlayMode smoke 1/1 (task 8)

## Implemented

- `ProjectileViewPool` MonoBehaviour이 BattleBridge 자식 GO로 씬에 와이어링됨
- ECS 엔티티 위치 → prefab view 동기화 (LateUpdate SyncTransforms)
- `AlongVelocity` / `SpinAroundUp` / `FixedUp` 3가지 회전 정책 적용
- CannonBall(Fireball) 적중 시 FireballHit prefab 1회 재생 → lifetime 후 풀 반환
- `tintColor` / `emissionMultiplier` MPB 결정적 노브 동작
- `scaleJitter` / `hueJitter` / `rotationJitter` per-shot 랜덤 노브 동작 (시뮬 RNG 분리)
- `textureVariants` MPB swap — Random / Sequential / First 3가지 selectMode
- 에디터 메뉴 `Wassup/Tools/Generate Projectile Texture Variants`로 12장 베이크
- `Projectile_Sniper_Crimson` 시연 자산: wind 계열 진홍 색조 + textureVariants 와이어링
- `ProjectileData.visualMesh` / `visualMaterial` + `RenderMeshUtility` 경로 완전 제거

## Key Files

- `Assets/_Project/Scripts/Presentation/ProjectileViewPool.cs` — 풀 전체 로직
- `Assets/_Project/Scripts/Data/ProjectileData.cs` — 스키마 전체
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — Spawn / DrainHitEvents 경로
- `Assets/_Project/Editor/ProjectileTextureBaker.cs` — 베이크 도구
- `Assets/_Project/Data/Projectiles/Projectile_Sniper_Crimson.asset` — 시연 자산
- `Assets/_Project/Tests/EditMode/ProjectileVariationTests.cs` — 5개 단위 테스트
- `Assets/_Project/Tests/PlayMode/ProjectileVisualSmokeTest.cs` — PlayMode smoke

## Verified

- compile: 에러 0, 경고 0
- EditMode 테스트: 5/5 통과
- PlayMode smoke: 1/1 통과 (HitPlayback_ReturnsToPool)
- Play mode: BattleScene 진입 시 에러/경고 없음

## Notes

- `ProjectileViewPool.SyncTransforms`는 Dictionary 순회 중 직접 수정 대신 `_posUpdates` 리스트로 2-pass 처리 — foreach 중 수정 금지 때문.
- `MaterialPropertyBlock _mpb`는 MonoBehaviour constructor가 아닌 `Awake()`에서 초기화해야 함 (Unity CreateImpl 제약).
- `DrainProjectileHitEvents`는 Task 3에서 PlayHit 경로 활성화됨. Splash 보조 타겟에는 hit VFX 없음 (의도적).
- `Projectile_Sniper_Crimson`은 `Defender_Sniper`에 와이어링됨. 기존 `bolt` 참조 교체됨.
- 베이크 텍스처는 git에 커밋 (`Assets/_Project/Generated/Projectiles/Textures/`).

## Follow-up

- **Cast prefab (머즐 플래시)**: 후속 후보 (README 참조)
- **Waterball 매핑**: 현재 미사용 키트
- **시각 확인 필요**: Sniper_Crimson의 진홍 색조 + 텍스처 rotation이 실제 게임뷰에서 가시적인지 사용자 Play 검증 필요
- 기존 DraftSessionTests 포함 전체 EditMode 회귀 여부는 CI 또는 사용자 확인 권장
