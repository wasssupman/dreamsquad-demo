# 9 — Meteor GA 비주얼: 하늘에서 떨어지는 낙하감 (visual update)

## 목적

수렴된 Meteor 투사체에 GA(GabrielAguiar) 프리팹 1종을 입혀 "하늘에서 떨어지는" 낙하를 다이내믹하게 만든다. 기존 절차적 `MeteorFall` 스트릭을 은퇴시키고 표준 `ProjectileViewPool` 뷰로 대체. **units 7~8 과 달리 이 unit 은 의도된 시각 변경** — 커밋 분리 이유.

## 변경 대상

- GA 후보 스트립 프리팹 → **unit 7 의 `Projectile_Meteor.asset` 에 prefab 할당** (스폰 배선은 unit 7 의 `SkillData.projectile` 경유라 추가 배선 없음 — critic 지적 반영)
- `ProjectileViewPool`(SyncTransforms) — SkyFall 뷰 렌더 (view-공간 낙하) + **초기 배치 t=0 낙하 오프셋**
- `ProjectileData` — skyfall 시각 파라미터 (dropHeight 등, 신규 필드 최소)
- 은퇴: `Assets/_Project/Scripts/Presentation/MeteorFall.cs` + `VfxSpawner.SpawnMeteorFall`(~L42)
- 유지: 경고 링(`SpawnMeteorWarningVisual`) — 게임플레이 가독성(착탄 위치 예고)

## 구현

1. **후보 선정 — 스크린샷 먼저**(lessons/03 벤더 VFX 교훈): 화염/에너지 계열 후보 2~3종(예: `vfx_Projectile_ExplosiveBullet01~03`, `vfx_Projectile_Rock01`, vol 내 화염계)을 Play 에서 낙하 연출로 캡처 비교 → **사용자 육안 픽**. 예측 튜닝을 앞질러 쌓지 않는다.
2. **스트립**: `GaProjectileStripper` 로 무버/RB/Collider 제거 + `TrailRenderer.autodestruct=false`(풀링 필수) + `ParticleSystem.emitterVelocityMode=Transform`. 색 씻김 있으면 `preserveVfxColors=true`.
3. **SkyFall 뷰 렌더**: **sim-Y 금지** (`BoardSpace.ToView` 가 drop — lessons/03). `ProjectileViewPool.SyncTransforms` 에서 BallisticArc view-y 패턴(`b84f2da`, `ProjectileViewPool.cs:132-137`)대로 `view.y += (1-t)·dropHeight` (+필요 시 대각 view 오프셋으로 사선 낙하). 낙하 방향이 velocity 에 접혀 트레일/스트레치가 아래로 향하는지 확인. **첫 프레임 스트릭 방지**: `Spawn` 은 지면 배치 후 첫 Sync 에서 dropHeight 만큼 점프 — 풀링 TrailRenderer 가 지면→하늘 스트릭을 그림. 초기 뷰 배치에 t=0 오프셋을 포함하거나 오프셋 후 `ResetVfx`(ga-reskin 가드 선례) 적용.
4. **스폰 배선**: unit 7 의 viewless 스폰 → `Projectile_Meteor_GA` 뷰 스폰으로 교체. `SpawnMeteorFall` 호출 제거, `MeteorFall.cs` 삭제.
5. (선택) 버스트를 GA hit 프리팹으로 업그레이드 — 기본은 기존 `SpawnMeteorBurst` 유지, 후보 비교 시 함께 보여주고 사용자 픽.

## 완료 기준

- Play: meteor 캐스트 → GA 투사체가 하늘에서 착탄 셀로 낙하(트레일 방향 정상) → 버스트 → 데미지 무회귀.
- 검증법: 슬로모(`TimeManager.Request(Battle, 0.2f)`) + 연속 스크린샷으로 낙하 시퀀스 캡처(lessons/01) → **사용자 육안 확정**이 게이트.
- 풀 재사용 2회차 스폰에서 트레일 잔존/소실 없음(autodestruct 함정 — 런타임에서만 드러남).
- `rg "MeteorFall"` 코드 0 매칭.
