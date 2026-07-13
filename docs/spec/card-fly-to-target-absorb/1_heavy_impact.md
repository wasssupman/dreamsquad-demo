# 1 — 묵직한 임팩트 반응

## 목적

카드가 찰싹 닿는 순간 타겟(유닛, 월드)에서 묵직한 흡수 반응: Spine 펀치 + 흰 플래시 + 링 충격파/버스트 +
미세 흔들림 + SFX. "카드가 유닛에 꽂혀 흡수됐다"는 타격감의 주역.

## 변경 대상

- `Assets/_Project/Scripts/Presentation/SpineUnitView.cs` — 펀치 스케일 + 흰 플래시 API 추가.
- `Assets/_Project/Scripts/Presentation/VfxSpawner.cs` — 링 충격파/버스트(재사용 또는 `SpawnCardAbsorb` 신설).
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 유닛 반응 게이트(뷰 계층은 EntityManager 안 봄).
- (SFX) SoundManager 흡수 틱.

## 구현

1. **유닛 펀치/플래시**(`SpineUnitView`): `PlayHitPunch(strength, dur)` — 스켈레톤 스케일 펀치(over→back),
   `FlashWhite(dur)` — R/G/B 를 흰색으로 순간 올렸다 복원(`SetHealthTint`/`_savedTint` 저장값과 충돌 안 나게,
   hover/health 틴트 복원 규칙 준수). unscaled 아님 — 전투 도메인 시간 준수(TimeManager 원칙 확인).
2. **링 충격파/버스트**: 도착 월드 좌표에 `VfxSpawner` 링(재사용 `SpawnPlacementRing` or 전용). 묵직함 = 링 + 짧은
   파티클 버스트 1방. VFX 는 카드 메커닉 소유(StatusFx kind 분기 금지, [[feedback_mechanic_vfx_owned_by_mechanic]]).
3. **흔들림**(열린 결정): 미세 카메라 킥 vs 유닛-로컬 흔들림. 묵직하되 전투 산만하지 않게 — 기본은 유닛-로컬 or
   아주 작은 카메라 킥. 착수 시 확정.
4. **SFX**: SoundManager "찰싹/임팩트" 틱(있으면 재사용, 없으면 후속).
5. **게이트웨이**: bridge 에 `PlayCardAbsorbImpact(host)`(유닛) 추가 — 내부에서 뷰 앵커·SpineUnitView 조회 후 위 반응 구동.
   presenter(unit 0)의 도착 콜백이 이걸 호출.

## 완료 기준

- compile 성공, 콘솔 CS 에러 0.
- Play — 카드 도착 순간 유닛이 움찔(펀치) + 흰 플래시 + 링/버스트 + 흔들림 + SFX 로 **묵직하게** 반응.
- 플래시/펀치 후 유닛 틴트·스케일이 원상 복원(hover/health 틴트와 충돌 없음).
- 반응이 전투 시간(슬로모 등)과 일관(TimeManager 준수).
- VFX 가 메커닉 소유 경로로 구동(StatusFx 분기 없음).
