# 3 — handoff summary

## Commit

- `45b1d645` unit 0 — 버스터즈 히트스캔 유닛 + 전제 회귀 테스트
- `96720343` units 1-2 — 지속 빔 프레젠터 + 개점 일제 조사
- `f83d8db5` fix — 매치 경계에서 빔 세션 정리

## Implemented

- 버스터즈(`busters`) — Ranger·Epic·코스트 4, HP 160 · 사거리 3 · 쿨다운 0.2 · 틱당 7(지속 35dps)
- **히트스캔**: `projectile` 비움 = 투사체 스폰 게이트(`ProjectileRef` 보유)를 안 타고 직접 데미지
- **BeamPresenter**: 고속 틱 공격 사건을 TTL 세션으로 뭉쳐 지속 빔으로 번역. 심에 빔 개념 0
  - 빔 유닛 판별 = SO 의 `beamVfxPrefab` 유무(id/kind 분기 없음)
  - 끝점 보간(사건은 0.2s 마다만 옴) · 공격자 사망 시 즉시 종료 · 매치 경계 전량 정리
- **개점 일제 조사**: `OnPlaceEffectType.DotNearby` — 반경 2 · 2초 · 틱당 7(총 70) + 대상별 빔
- 벤더 프리팹 사본 `Assets/_Project/VFX/BusterBeam.prefab` — `BeamVfx`/`ParticleSystemStartStopLifetime` 제거

## Key Files

- `Assets/_Project/Data/Defenders/Defender_Busters.asset`
- `Assets/_Project/Scripts/Presentation/BeamPresenter.cs`
- `Assets/_Project/VFX/BusterBeam.prefab` (벤더 사본 — 원본 `Assets/PixPlays/...` 는 불변)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `DrainUnitAttackVisualEvents` 의 빔 연결 · `DotNearby` 분기 · `TryResolveViewMuzzle` · `EnsureBeamPresenter`
- `Assets/_Project/Tests/PlayMode/HitscanDefenderTest.cs` · `OnPlaceDotNearbyTest.cs`

## Verified

- 리그 PlayMode 8/8 green (빔 2종 + 히트스캔 + 넉업/스택/기존 ember·frost 무회귀)
- 에디터 컴파일 클린

## Notes (되돌리지 말 것)

- **`projectile` 이 비어 있는 건 사양이다.** 실수로 보고 채우면 히트스캔이 투사체 유닛으로 바뀐다. `hitDelaySec 0` 도 필수 — 기본값 0.3 은 틱 간격 0.2보다 길어 타격이 밀린다.
- **`tickInterval > 0` 이면 `scalar` 는 틱당 피해다(DPS 아님).** 이 spec 초안이 `7/0.2=35` 로 환산하라고 적어 뒀었고 그대로 했으면 피해가 5배였다. `OnPlaceDotNearbyTest` 가 상한으로 잡는다.
- **벤더 `BeamVfx` 를 되살리지 말 것.** `BaseVfx` 가 `Destroy(gameObject, ...)` 기반이라 풀링과 충돌하고, 재조준을 매 프레임 우리가 쥐어야 한다.
- **빔 끝점은 머즐과 같은 z 로 눕힌다.** 평면 정면뷰라 z 가 다르면 빔이 화면 안쪽으로 기울어 짧아 보인다(`TrySpawnCastVfx` 의 `dir.z = 0` 과 같은 이유).
- **양 끝은 좌표가 아니라 Entity 로 붙든다.** 초판은 사건 시점 좌표를 스냅샷했는데, 그러면 개점 조사(사건 1회 · 2초)가 **2초 내내 배치 순간 좌표를 겨눈다** — 그동안 적은 걸어간다. 설계 리뷰가 잡았고 Entity 추종으로 고쳤다. 이때 **Transform 을 캐시하지 말 것**: 풀이 뷰를 재사용하므로 대상 사망 후 그 자리의 다른 유닛에 빔이 옮겨 붙는다. 매 프레임 엔티티로 조회하고 실패하면 세션을 닫는다.
- **세션 TTL 은 `UnitAttackVisualEvent.attackAnimPeriod`(실발사 주기, `attackSpeedMul` 반영)에서 온다.** 상수로 박으면 공속 버프나 주기가 다른 두 번째 빔 유닛에서 깜빡인다. 남는 상수는 무차원 여유 계수 하나(`BeamSessionTtlMargin`).
- **TTL 은 배틀 도메인 시간으로 깎는다.** 실시간이면 슬로모에서 사건 간격이 TTL 을 넘겨 빔이 깜빡인다.
- 씬 배선은 선택이다 — `beamPresenter` 가 비면 `EnsureBeamPresenter` 가 런타임 생성한다. **이 기능만을 위해 BattleScene 을 저장하지 말 것**(그 시점의 미저장 WIP 가 같이 박힌다). TTL/추종속도를 튜닝하려면 그때 씬에 배선하면 된다.

## Follow-up

- **사용자 Play 시각 확인** — 빔이 유닛→타겟에 지속으로 붙는지, 타겟 전환 시 자연스러운지, 굵기/색/길이 감각. 정지 스크린샷으로는 판정 불가
- **공격 애님/SFX 가 0.2s 마다 재트리거되는지 관측** — unit 0 부터 미확인. 재트리거가 거슬리면 코얼레스 규칙 추가(현재는 빔만 코얼레스)
- 지속 35dps 단일 대상 밸런스 감각(로스터 최고 지속딜, HP 160 으로 상쇄)
- 나머지 후속 후보는 README 참조
