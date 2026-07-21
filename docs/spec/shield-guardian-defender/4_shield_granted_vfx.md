# 4 — 실드 부여 원샷 VFX (Effects→Bridge→VfxSpawner)

## 목적

실드가 부여되는 순간 대상 위치에 단발 VFX를 띄운다. 힐 VFX 채널 선례
(`HealAppliedEventsSingleton` → `VfxSpawner.SpawnHealApplied`)를 그대로 따른다.

## 변경 대상

- 신규 `Assets/_Project/Scripts/Battle/Effects/ShieldGrantedEvents.cs` — `ShieldGrantedEvent{float3 position}` + `ShieldGrantedEventsSingleton{NativeQueue queue}`
- `Assets/_Project/Scripts/Battle/Effects/ShieldCastSystem.cs` — 부여 성사 시 대상 위치 enqueue (후보 위치 추적 추가)
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — `shieldGrantedPrefab`/`shieldGrantedScale` 슬롯 + `SpawnShieldGranted` + `ConfigureOneShot`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 큐 lifecycle(필드/생성/Dispose 2곳/DestroyEntitiesByType) + `DrainShieldGrantedEvents`
- `Assets/_Project/Scenes/BattleScene.unity` — VfxSpawner.shieldGrantedPrefab = `VFX_Fire_Green.prefab` 배선
- `CLAUDE.md` — 채널 19→20 (`ShieldGrantedEventsSingleton`)

## 구현

- **경로 = NativeQueue via BattleBridge** (VFX 스킬 decision tree): 실드 부여는 ECS 판정(ShieldCastSystem) 프레임 기준이라 sim-timed → 큐. 즉시 입력 피드백이 아님.
- `ShieldCastSystem`: 부여(`IncomingShield.Add`)가 성사된 대상에 한해 `ShieldGrantedEvent{대상 sim 위치}` enqueue. 싱글턴은 옵셔널(`TryGetSingletonRW` — 테스트 월드 무해).
- `VfxSpawner.SpawnShieldGranted`: prefab null 이면 LogError+return(프로젝트 관례 — 코드 폴백 없음). `BoardSpace.ToView` 로 sim→view 1회, y+0.08 오프셋, scale 적용.
- **단발화 = `ConfigureOneShot`**: 벤더 프리팹 `VFX_Fire_Green` 이 `looping=1`/rateOverTime 지속형이라, 스폰 **인스턴스**의 각 ParticleSystem 에서 `loop=false` + rateOverTime→t0 Burst(4~24 clamp) 치환 → "펑 터지고 페이드". 공유 에셋은 무접촉(lessons 규칙). 자가 파괴 = 최대(duration+startLifetime)+0.2s.

## 계약

- 신규 채널은 값 타입 payload(위치)만 — managed/scene 참조 금지(VFX 스킬).
- MonoBehaviour(VfxSpawner)는 ECS API 직접 접근 안 함 — BattleBridge drain 경유(Iron Law).
- 부여 성사 시에만 enqueue(버퍼 없는 대상 스킵과 동일 게이트) — 헛방 VFX 없음.

## 완료 기준

- [x] compile 클린 · EditMode 1151/1153 (skip 2 = 기존 known-skip).
- [x] BattleScene VfxSpawner.shieldGrantedPrefab 배선 확인(리소스 조회로 경로 해석 검증).
- [ ] Play 시각 검증: 실드 부여 순간 대상 위치에 초록 화염 단발(루프 안 함) · 부여 대상마다 1회 · 콘솔 0.

확인 2026-07-21 · 커밋 `7dc41c5c` (compile+EditMode·씬 배선 검증 / Play 시각은 unit 3 통합 Play 로)
