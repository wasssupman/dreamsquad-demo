# 1 — 발사 요청 조립 한 갈래 (코어)

## 목적
`IntentApplier.SpawnProjectile` 의 「대상 갈래 / 자리 갈래」를 없애고, **궤적의 결합 종류**(`MovementBinding.Of` → Entity · Cell · Direction) 하나로 요청을 채운다. 반경·예고·비행시간은 의도(효과 파라미터)에서 온다. 케이스 2 의 ①반경 ②예고(코어 몫)를 푼다.

## 변경 대상
- `Scripts/BattleCore/Trigger/IntentApplier.cs` — `SpawnProjectile`.
- `Scripts/Skills/Concrete/TileMeteorSkill.cs` · `SelfAreaBlastSkill.cs` · `DeathSiteBlastSkill.cs` — 「칸에 떨어진다」를 **의도에 명시**(`ProjectileMovement = SkyFall`, `ProjectilePayload = TileAoe`). 지금은 applier 가 대상 없음 → 강제로 정한다.
- `Scripts/Skills/Concrete/TargetProjectileSkill.cs` — `Duration`(낙하·비행 시간)과 예고 플래그를 싣는다(라이브 저작 전부 0 → 무변).
- 원점 산출: `Scripts/BattleCore/Trigger/` 에 트리거 종류 → (좌표, 원점 항, 대상 엔티티) 를 한 곳에서 내는 정적 함수(파일 신설). 오늘 감지자·스킬이 흩어 채우는 `Position`/`EventPosition`/`EventBodyRadius` 의 규칙을 이리로 모은다. 입력·출력은 plain 값(제약 10).

## 구현
- 결합 종류별 채움 규칙(한 함수 안의 표):
  - **Entity**(호밍): `Target` = 대상 · `Impact` = 대상 자리 · `TileRange` = 재조준 반경(오늘 비수 4 그대로).
  - **Direction**(직선·부메랑): `DistanceOverride` = `TileRange` 칸 · 방향 = 의도 방향 또는 대상 쪽.
  - **Cell**(하늘 낙하·포물선): `Impact` = 대상이 있으면 **대상의 현재 좌표**, 없으면 의도 좌표 · `ImpactTileRange` = `TileRange` · `FlightTime` = `Duration` · 예고 = 의도의 `Telegraph`.
- 궤적 출처 우선순위: 의도의 명시 궤적 > 탄 정의. 자리형 스킬 셋이 명시하므로 「대상 없으면 하늘 낙하」 강제는 지운다.
- `OriginBodyRadius` 는 의도 값 그대로(자리형 0 · 몸형 몸). 대상 좌표를 쓴 Cell 은 0(맞은 적 자리 = 자리형, 탐침 `타격_운석의_원점_항은_칸_반폭이다`).
- 착탄 예고(`Telegraph`)는 스킬의 몫(U1): `TileMeteorSkill` 은 오늘처럼 켠다 · `TargetProjectileSkill` 은 파라미터의 예고 플래그를 그대로 싣는다(저작 필드는 unit 4 — 그 전까지 기본 꺼짐이라 무변). 자리형 죽음 계열은 오늘처럼 끔.

## 완료 기준
- 탐침 `타격_운석의_반경은_바인딩_TileRange_다` · `타격_운석에도_착탄_예고가_뜬다`(예고 플래그를 켠 줄로 — 픽스처 수정) `[Ignore]` 해제 초록. 짝 `현행_…` 둘은 목표형으로 뒤집거나 삭제.
- 헤드리스(클린 export) 전부 초록 · 골든 11 Verify 일치(Unity) · EditMode Core 선행 외 0 · `EffectWitness`(Assets lane) 무변.
- 새 원점 산출 함수 단위 테스트(트리거 종류별 1케이스 이상).
