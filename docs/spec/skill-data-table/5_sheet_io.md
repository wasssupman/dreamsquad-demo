# 5 — 새 시트 연결 (저작)

## 목적
unit 0 의 표 구조로 시트 export/import 를 새로 만든다. ~~기존 `DcSheetApplier`(카드 전용) 는 은퇴.~~ → 정정: `DcSheetApplier` 는 **남는다** — 은퇴한 것은 카드 규칙 탭(`DcMechanics`)과 카드 자식 탭(unit 8 의 `DcCardEffects` · `DcAttackMods`)이고, 평면 탭(`Cards` · `DcSkills` · `DcConfig` — `DcSheetImportDto`)은 계속 그것이 적용한다. 효과 · 소유 줄은 새 임포터 `SkillSheet`.

## 변경 대상
- `Editor/UnitStatImport/` · `Scripts/Data/StatImport/` — 표별 export/import(효과 · 소유 줄 · 탄 · 패턴 · 장판 · 소유자). 임포터 하나 · 표마다 id 조회.
- 로그인·런타임 임포트 경로 — `Scripts/UI/Outgame/LoginAutoImport.cs` · `Core/Dreamcatcher/DcSheetRuntimeRefresher` · `AllRuntimeRefresher` 를 새 임포터로.
- 임포트 전 diff 표 로그(dry-run 없음 → 에셋 쓰기 전에 무엇이 바뀌는지 먼저).

## 완료 기준
- export → import 왕복 후 굽기 스냅샷 동치.
- 시트 쓰기는 사용자 승인 후 · 시트 확인은 curl 읽기 전용.

## 기록 (2026-09-29 · 5 1부)

- **탭 이름 = U19**: `tables.md` 의 `Effects` → 시트 탭 `Skills` · `tables.md` 의 `Skills`(소유 줄) → `SkillOwners` · `owner_kind` = `card` · `defender` · `enemy` · 한국어 표시 열 `kind_ko`(보기 전용 — 임포터가 안 읽는다). `subject` 값 = 코어 멤버 이름(`Self` · `Any`).
- **탭 계약 하나** = `Scripts/Data/StatImport/DcSheetTabs.cs`(7탭 — `DcCards` · `DcCardEffects` · `DcAttackMods` · `DcSkills`(U18 유지) · `DcConfig` · `Skills` · `SkillOwners`). 옛 `DcMechanics` 은퇴(DTO · 차단 분기 삭제). ⚠ unit 8 단계 B(`5825fed06`) — `DcCardEffects` · `DcAttackMods` 도 은퇴 → **5탭**(`DcCards` · `DcSkills` · `DcConfig` · `Skills` · `SkillOwners`). ⚠ unit 9 — `DcCards` → **`Cards`** 개명(현행 5탭 = `Cards` · `DcSkills` · `DcConfig` · `Skills` · `SkillOwners` · push 전체 = 스탯 2 + 이 5 + `CostConfig` = 8탭).
- **임포터 하나** = `SkillSheet`(`Scripts/Data/StatImport/SkillSheet.cs`): 계획 → 쓰기 전 diff 표(`[skills-diff]`) → apply. 없는 id(효과 · 소유자 · 탄 · 패턴 · 장판)는 보고만. `Skills` = id 별 부분 갱신(빈 칸 = 그대로) · `SkillOwners` = 탭에 나온 소유자의 `bindings` 재구성(시트-정본). 에디터 창에 「diff 미리보기(쓰지 않음)」 버튼.
- 경로: 로그인 자동 import · 로비 refresh(`DcSheetRuntimeRefresher` — 방어유닛 · 적 카탈로그 배선) · 에디터 창 import/export · push 바디 · 합본 export.
- U18: 액티브 문안의 비용 = 정의표 카드 값(`CardDef.Cost` = `AwakeningConfig.costActive`) · 모르면 비용 칸을 뺀다.
- 커밋: `658988c93`(U18) · `7dc32158f`(SkillSheet) · `980367c1d`(탭 계약) · `ba48b6742`(왕복 테스트).

## 남은 것 · 알려진 한계

- **모양 탭(`Projectiles` · `Patterns` · `PatternShots` · `Hazards` · `HazardEffects` · `Blockers`) 은 이 1부 밖** — 오늘 시트에 없는 값이라 회귀가 없고, 패턴 · 장판 SO 에 U10 이전 옛 피해 칸이 남아 있어 먼저 열면 죽은 열이 시트에 보인다. 소유자 표(`Cards` · `Units` · `Enemies`)는 기존 탭 그대로(`Defenders` · `Enemies` · 카드 탭은 unit 9 에서 `DcCards` → `Cards` 개명 · 전 탭 열 스네이크).
- 런타임 refresh 는 효과가 **지금 가리키는** 탄 · 패턴 · 장판만 안다(에셋 스캔 없음 — 보고됨). 에디터 import 는 전부 안다.
- 빈 칸 = 그대로라 참조를 **비우는** 방법이 없다 · bool 을 끄려면 `FALSE` 를 적는다.
- `Skills` export 는 그 종류가 쓰는 칸을 **기본값이어도** 적는다(`buff_stat` = `AttackDamage` 처럼 첫 enum 값이 빈 칸으로 숨지 않게) · 비율 칸(`basis_stat` · `ratio`)은 `magnitude_mode = Flat` 줄에서 비운다.
- push 는 업서트(고아 행 안 지움) — `SkillOwners` 에서 줄을 빼도 시트에 옛 줄이 남으면 다음 import 가 되살린다(`DcCardEffects` 와 같은 성질).
- ~~export 는 기본값이 아닌 칸을 전부 쓴다 · 「안 쓰는 칸 경고」 미구현~~ → **unit 9 해결**: export = 종류가 쓰는 칸만 · import = 안 쓰는 칸에 값이 오면 경고하고 무시.
- ~~`DcSkills` 의 수치 칸이 문안만 움직인다~~ → **unit 8 · 9 해결**: 문안 = 시전 줄 효과 + 카드 `cooldownSec`(unit 8) · `DcSkills` 수치 칸 삭제 · 쿨다운 = `Cards.cooldown_sec`(unit 9).

## 실제 시트 설정 (사용자 몫 · 에이전트는 시트에 쓰지 않는다 — unit 9 최종 8탭)

아래 헤더 줄이 **정본**이다 — 시트 탭 1행에 이 순서 그대로(export 가 쓰는 열 순서 = `SheetColumns.Of` · 테스트 `SheetHeaderDocTests` 가 DTO 의 JSON 이름과 대조한다).
열 이름 = 전 탭 스네이크. `_` 로 시작하는 열은 **정보 열**(export 가 채우고 임포터 · 매퍼가 건너뛴다 · `tables.md` §13). 폐기 호환 열 `attack_damage`(옛 `atk` 개명 경고용)는 만들지 않는다.

- `Skills` 헤더: `effect_id, kind, kind_ko, deprecated, magnitude_mode, basis_stat, ratio, damage, shield, percent, mul, count, radius_tiles, range_tiles, duration_sec, flight_sec, tick_sec, stack_cap, speed, cone_half_deg, density_radius_tiles, landing_ring_tiles, cc_kind, stack_kind, buff_stat, shield_filter, includes_self, telegraph, ally_filter, projectile_id, pattern_id, hazard_id`
- `SkillOwners` 헤더: `owner_kind, owner_id, slot, trigger, period, period_sec, fraction, subject, gate, gate_subject, gate_value, fire_cap, effect_id`
- `Cards` 헤더: `id, display_name, type, axis, description, visible, attach_type, attach_value, host_kinds, cooldown_sec, needs_two_tiles, _skill_id`
- `Defenders` 헤더: `id, display_name, desc, visible, role, rarity, health, attack_range, atk, heal, attack_cooldown, hit_delay_sec, attack_target_count, cost, placement_cooldown, death_cooldown, retire_cooldown_ratio, max_on_board, footprint_width, footprint_height, aggro_capacity, aggro_range, awakening_reward`
- `Enemies` 헤더: `id, display_name, enemy_class, attack_method, target_mode, engage_movement, target_priority_class, target_class_mask, health, move_speed, atk, attack_range, attack_cooldown, attack_target_count, hit_delay_sec, aggro_attack_damage, aggro_attack_cooldown, aggro_attack_range, awakening_reward`
- `DcSkills` 헤더: `id, display_name, description, _effect`
- `DcConfig` 헤더: `id, gauge_max, gauge_start, cost_squad, cost_unit, cost_active, hand_size, max_attach_per_unit, slomo_time_scale, deck_size, max_squad, max_unit`
- `CostConfig` 헤더: `id, starting_cost, max_cost, regen_per_sec, placement_phase_duration`

**서버(Apps Script push 업서트 키 · GET `/demo/google/sheet/{탭}`)**: `Skills` = `effect_id` · `SkillOwners` = (`owner_kind`, `owner_id`, `slot`) · `Cards` · `DcSkills` · `DcConfig` · `Defenders` · `Enemies` · `CostConfig` = `id`. `Defenders` · `Enemies` · `CostConfig` 탭 이름은 에디터 창에서 바꿀 수 있다(기본값 그대로).

**한 번에 바꾸는 절차**(탭을 하나씩 바꾸면 그 사이 로그인 import 가 옛 열을 「계약 밖 헤더」로 보고하고 **아무것도 바꾸지 않는다** — 빈 칸 = 그대로 · 없는 탭 = 섹션 없음):
1. Unity 에디터 → `Window/Wassup/Unit Stat Import` → 「Export Dreamcatcher → 시트 페이로드」(DC 5탭) · 「Export SO → JSON Files」(Defenders · Enemies) · 「Export CostConfig SO → JSON」 — 값의 정본은 **에셋**이다(손으로 친 시트 값은 로그인 import 가 에셋에 덮는다).
2. 시트에서 위 8탭을 새 헤더로 다시 만든다(탭 이름 `Cards` 는 새 이름 · 나머지는 같은 이름에 헤더만 스네이크) → export 값을 붙인다(또는 에디터 「Push to Sheet」 — 업서트 · 고아 행 안 지움).
3. 서버 업서트 키를 위 표대로 설정 → GET 으로 8탭 확인(curl 읽기 전용).
4. 옛 탭 `DcCards` · `DcCardEffects` · `DcAttackMods` · `DcMechanics` 는 아무도 안 읽는다 — 보관 · 삭제 자유.
5. ⚠ 에디터 창의 DC 탭 목록 prefs 키가 `.v4` 로 바뀌었다(옛 목록 = 옛 이름 `DcCards` 를 조용히 fetch) — 첫 실행에 새 기본값(5탭)으로 떨어진다.
