# 3 — Handoff Summary

## Commit
- `13fb73d5` unit 0 — 능력 서브에셋 SO 타입 계층 (additive)
- `829ab8ea` unit 1 — ability 에셋 7개 저작 + 유닛 배선 (data only)
- unit 2 — 코드 cut-over + flat 필드 삭제 + 테스트 갱신 (이 커밋)

## Implemented
- `DefenderUnitData` 능력별 flat 필드 25개(volley 4·hazard 8·shield 4·bomb 9) → `Data/Abilities/` 서브에셋으로 이관·삭제.
- `DefenderAbilityData`(추상 base, id 시트키 + `RequiresFacing`) + 구체 4종: `DirectionalVolleyAbility`·`HazardCastAbility`·`ShieldCastAbility`·`BombThrowAbility`.
- 유닛은 `List<DefenderAbilityData> abilities` + `GetAbility<T>()`/`RequiresFacing` 헬퍼만 보유.
- bake(`CreateDefenderEntity`) 4곳 게이트를 `GetAbility<T>() != null` 로 전환 — ECS 컴포넌트(VolleyFireState/HazardCastState/ShieldCastState/BombLauncherState) bake 결과 **동일**(등가성).
- `directionalAttack` flag → 능력이 선언하는 `RequiresFacing`. 소비처(DragPlacement·Selector·Tutorial·DirectionAimController·SetAimGuide·SetPlacementRange) 전환.
- `UnitKitSummary` 자동 요약문 = ability 조회로 재작성(문구/순서 불변).
- ability 에셋 7개(id 슬러그 = 시트 매칭키 예약). 유닛 abilities[0] 배선.

## Key Files
- `Assets/_Project/Scripts/Data/Abilities/*.cs` (신규 5)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` (flat 삭제 + 헬퍼)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (bake 4 + aim/placement guide)
- `Assets/_Project/Data/Abilities/*.asset` (능력 데이터 7)
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs`, `Tests/EditMode/UnitKitSummaryTests.cs`

## Verified
- compile 0 · 전체 EditMode **1193 passed / 0 failed / 2 skipped**(기존 known-skip 2).
- flat 필드 참조 0 (grep 전수). 사용자 Play 확인(7유닛 동작 동일) 후 마감.

## Notes
- **ECS/시뮬 무변경**: 이 spec 은 authoring 데이터 재편일 뿐. bake 번역자만 재작성.
- 유닛 `.asset` 의 orphan flat 키(삭제된 필드)는 Unity 재저장 시 자연 소멸 — 하드 정리 안 함(무해).
- `Defender_Guardian.hazardCastEnabled=1` 은 유령 플래그였음(kind 0·참조 null) → 능력 미배선 → no-op HazardCastState 소멸. 동작 무변화(range 0 = 캐스트 안 함).
- `BombThrowAbility`/`DirectionalVolleyAbility` 만 `RequiresFacing=true`. 폭탄병 조준은 SetAimGuide/SetPlacementRange 가 `GetAbility<BombThrowAbility>()` 로 착지 후보 4셀만 그림.

## Follow-up
- 라이더 3그룹(knockback·sleepOnHit·onPlacePush/Effect) 이관 · 능력종별 시트 탭 임포터/익스포터 · 적(AttackUnitData) 동형 재구조화 — 상세는 README 후속 후보.
- UnitKitSummary 사격 문구는 현재 volley 능력에만 귀속(폭탄 등 다른 facing 능력 문구는 미작성 — 해당 유닛은 authored desc 사용 중).
