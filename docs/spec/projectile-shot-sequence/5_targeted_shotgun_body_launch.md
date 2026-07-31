# unit 5 — 자동 조준 샷건 + 몸체 발사점

## 목적

샷건너의 배치 후 방향 지정 페이즈를 제거하고 일반 유닛처럼 드롭 즉시 배치를 확정한다.
공격은 일반 사거리 타겟팅으로 START하되, 선택한 적 방향을 한 번 고정해 기존 공용
`EmitterInstance`의 10발 spread trigger를 그대로 사용한다.

동시에 유닛이 발사한 모든 투사체의 첫 표시 위치를 ECS 셀 중심+고정 높이가 아니라 실제
무기/몸체 앵커로 맞춘다. 화면 상단에서 탄환이 머리 위에서 생기는 인상을 제거하되 시뮬
원점·이동·충돌·수명은 변경하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Abilities/DirectionalVolleyAbility.cs`
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs`
- `Assets/_Project/Scripts/Battle/Combat/{AttackComponents,AttackSystem}.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Scripts/Presentation/{SpineUnitView,SpineUnitPool,ProjectileViewPool}.cs`
- `Assets/_Project/Data/Abilities/Ability_Volley_{Shotgunner,MachineGunner}.asset`
- `Assets/_Project/Data/Defenders/Defender_Shotgunner.asset`
- 관련 EditMode·PlayMode 테스트와 현재 계약을 설명하는 spec README

## 구현

- `DirectionalVolleyAbility`에 데이터 주도 `requiresFacing`을 둔다. 머신거너는 `true`,
  샷건너는 `false`를 명시한다. 따라서 샷건너는 `DefenderUnitData.RequiresFacing == false`로
  일반 D&D 확정 경로를 사용하며 머신거너의 2페이즈 조준은 그대로다.
- facing 없는 `DirectionalLinear` pattern 공격은 START 때 선택된 `bestTargetPos - atkPos`를
  정규화해 `AttackState`에 저장한다. RESOLVE 때 타겟이 죽거나 사거리 밖으로 이동해도 이
  방향으로 emitter instance를 한 번 만들고, RESOLVE 뒤 스냅샷을 비운다.
- 샷건 pattern은 trigger마다 host와 영속 fire count에서 결정론적 seed를 만들고, 10개의
  `directionT`와 첫 탄 이후 interval을 각각 새로 스냅샷한다. 방향은 `-30°~+30°`,
  interval은 SO의 `0.006~0.018초` 범위 안이며 동시에 몰아쏘는 고정 `5-3-2` 재생을 제거한다.
  탄당 6·4타일 maxDistance·쿨다운과 기존 emitter/캐리어 흐름은 변경하지 않는다.
  Dreamcatcher host 분류는 pattern 보유 기준을 유지한다.
- 비행 VFX는 프로젝트에 정리돼 있는 GA `vfx_Projectile_Shard01`을 scale `0.7`로 사용한다.
  vendor 원본의 이동 script/Rigidbody/Collider는 사용하지 않으며 기존 hit/cast VFX는 유지한다.
- `BattleBridge`는 request carrier가 아니라 `req.owner`를 우선해 발사자 view를 찾고,
  `SpineUnitPool`의 projectile 전용 anchor 조회로 plain `Vector3`를 얻어 Pool에 넘긴다.
- defender는 저작된 `WEAPON` bone을, enemy Spine은 renderer body center를 사용한다.
  앵커를 못 찾은 fallback view는 기존 카메라 평면 높이 위치를 유지한다.
- `ProjectileViewPool`은 spawn 프레임의 즉시 `SyncTransform`이 앵커를 덮지 않도록 첫 sync를
  한 번 보류한다. prefab particle/trail은 앵커에서 reset·재생되고 다음 프레임부터 기존
  `BoardSpace.ToView + HeadAnchor.Lift` 궤적을 따른다.

## 완료 기준

- Unity 컴파일 오류가 없다.
- EditMode에서 샷건 `RequiresFacing=false`, 머신거너 `true`, 설명문 분기를 검증한다.
- AttackSystem 통합 테스트가 facing 없는 샷건의 타겟 방향 10발, START 후 타겟 소실 발사,
  trigger별 랜덤 각도·개별 interval의 범위/재현성/변화, 4타일 수명 보존을 검증한다.
- Pool 테스트가 spawn 직후와 첫 sync에서 몸체 앵커를 유지하고 다음 sync부터 기존 투영
  궤적을 따르는지 검증한다. 앵커 미제공 경로는 무회귀다.
- PlayMode projectile visual smoke와 Unity EditMode 전체 스위트를 실행한다.
- Play에서 보드 상·중·하단 샷건너의 첫 pellet/trail이 무기/몸체에서 시작하고,
  드롭 후 방향 지정 UI 없이 자동 조준하며 짧은 Shard trail이 10발에서 뭉치지 않는지 확인한다.
- ECS 리뷰에서 Combat 소유 쓰기, BattleBridge 단일 경계, sim/view 분리 위반이 없다.

> 사용자 Play 확인: 통과 (2026-07-31)
