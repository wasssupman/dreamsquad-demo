# 4. Legacy attackDamage Removal (severable)

## 목적

런타임 미소비 + UI 정정(unit 2) 완료 후에도 SO/Inspector/YAML에 남아 "여기 고치면 되겠지" 오해 표면이 되는 레거시 스칼라를 제거한다. **선행 조건: unit 2·3 완료** (UI가 attackDamage를 읽는 상태에서 삭제 금지). 이 유닛은 severable — 드랍/연기해도 unit 0~3 성립.

## 변경 대상

- `Assets/_Project/Scripts/Data/DefenderUnitData.cs:18` — `attackDamage` 필드 삭제 + `:40` 인근 stale 주석 정리
- `Assets/_Project/Scripts/Data/AttackUnitData.cs:36` — 필드 삭제 + `:34-36`, `:57` 인근 stale 주석 정리
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 베이크 라인 **2곳** 제거: `:2965`(defender `damage = unitData.attackDamage`) + `:3634`(enemy). stale 주석 정리: `:3064-3065`("AttackSystem branches on HasBuffer to decide legacy vs outputs path" — 레거시 경로 이미 없음), `:3619-3621`("attackDamage remains serialized compatibility data")

## 구현

- 필드 삭제 후 `AttackState.damage`는 default(0)로 베이크 — dead read이므로 동작 무변화 (필드 자체 제거는 unit 5).
- **grep 체크리스트** (완료 기준에 결과 기록): `attackDamage` 전 저장소 검색 0건 — 허용 예외는 DTO shim(`UnitStatImportDto`), `aggroAttackDamage`, spec 문서.
- **asset stale YAML**: 필드 삭제 후 `.asset`에 남는 `attackDamage:` 라인은 Unity가 무시하므로 무해. 스크럽(재직렬화)은 필수 아님 — 하려면 **별도 커밋 + git status 확인 + 사용자 승인 게이트** (현재 워크트리에 무관 dirty asset 다수, `Data/{Defenders,Enemies}` 폴더 한정).

## 완료 기준

- [ ] compile 오류 없음 (테스트 포함)
- [ ] grep 체크리스트 0건 기록 (허용 예외 제외)
- [ ] 전체 EditMode 스위트 회귀 없음
- [ ] Play smoke: 전투 데미지/킬 동작 전후 동일, walk-only 경고 패턴 동일, console 무오류
