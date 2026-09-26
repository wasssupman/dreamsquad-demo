# 1 — 발사 요청 조립 한 갈래 (코어 · H2)

## 목적
`IntentApplier.SpawnProjectile` 의 대상/자리 두 갈래를 **궤적 결합 종류**(`MovementBinding.Of` → Entity · Cell · Direction) 하나로. 반경·예고·비행시간은 의도(효과 파라미터)에서. `Wassup.Skills` 입력 형(`SkillTarget`·`SkillParams`)은 **안 바꾼다**(그건 unit 2).

## 변경 대상
- `Scripts/BattleCore/Trigger/IntentApplier.cs` `SpawnProjectile`.
- 자리형 concrete 셋(`TileMeteorSkill` · `SelfAreaBlastSkill` · `DeathSiteBlastSkill`) — 의도에 궤적을 명시(`ProjectileMovement = SkyFall` · `ProjectilePayload = TileAoe`). 오늘 applier 가 「대상 없으면 하늘 낙하」로 강제하던 것을 옮긴다.
- `TargetProjectileSkill` — `Duration` 과 예고 플래그(U1)를 싣는다. 라이브 저작 전부 0/꺼짐 → 무변. (`SimIntent` 는 이미 두 필드를 갖는다 — Skills 형 무변.)

## 구현
- 결합 종류별 채움(한 함수 안의 표):
  - **Entity**: 대상 추적 · `Impact` = 대상 자리 · `TileRange` = 재조준 반경(비수 4 무변).
  - **Cell**: 착탄 = 대상이 있으면 **그 현재 좌표**, 없으면 의도 좌표 · `ImpactTileRange` = `TileRange` · `FlightTime` = `Duration` · 예고 = 의도 `Telegraph` · `OriginBodyRadius` = 의도 값(대상 좌표를 쓴 Cell 은 0 = 자리형).
  - **Direction**: `DistanceOverride` = `TileRange` 칸.
- 궤적 = 의도 명시 > 탄 정의. 발사 자리 = 의도 `Position`(오늘 그대로).
- 발사 명세 경로(`CombatPhase` 버스트 · 전원 손잡이의 `BindingClass.Entity` 조건)는 **손대지 않는다** — 캐논 폭격이 라이브.

## 완료 기준
- 탐침 `타격_운석의_반경은_바인딩_TileRange_다` · `타격_운석에도_착탄_예고가_뜬다`(예고 켠 픽스처) 해제 초록 · 짝 `현행_…` 목표형으로 뒤집거나 삭제.
- unit 0 표 1 의 `SpawnProjectile` 경유 행마다 무변 단언(요청 필드 동치 테스트).
- 헤드리스(클린 export) · 골든 11(Unity) · EditMode Core(선행 외 0) · `EffectWitness` · `core-reviewer` APPROVE.
