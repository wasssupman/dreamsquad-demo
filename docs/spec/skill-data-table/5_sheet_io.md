# 5 — 새 시트 연결 (저작)

## 목적
unit 0 의 표 구조로 시트 export/import 를 새로 만든다. 기존 `DcSheetApplier`(카드 전용) 는 은퇴.

## 변경 대상
- `Editor/UnitStatImport/` · `Scripts/Data/StatImport/` — 표별 export/import(효과 · 소유 줄 · 탄 · 패턴 · 장판 · 소유자). 임포터 하나 · 표마다 id 조회.
- 로그인·런타임 임포트 경로 — `Scripts/UI/Outgame/LoginAutoImport.cs` · `Core/Dreamcatcher/DcSheetRuntimeRefresher` · `AllRuntimeRefresher` 를 새 임포터로.
- 임포트 전 diff 표 로그(dry-run 없음 → 에셋 쓰기 전에 무엇이 바뀌는지 먼저).

## 완료 기준
- export → import 왕복 후 굽기 스냅샷 동치.
- 시트 쓰기는 사용자 승인 후 · 시트 확인은 curl 읽기 전용.

## 기록 (2026-09-29 · 5 1부)

- **탭 이름 = U19**: `tables.md` 의 `Effects` → 시트 탭 `Skills` · `tables.md` 의 `Skills`(소유 줄) → `SkillOwners` · `owner_kind` = `card` · `defender` · `enemy` · 한국어 표시 열 `kind_ko`(보기 전용 — 임포터가 안 읽는다). `subject` 값 = 코어 멤버 이름(`Self` · `Any`).
- **탭 계약 하나** = `Scripts/Data/StatImport/DcSheetTabs.cs`(7탭 — `DcCards` · `DcCardEffects` · `DcAttackMods` · `DcSkills`(U18 유지) · `DcConfig` · `Skills` · `SkillOwners`). 옛 `DcMechanics` 은퇴(DTO · 차단 분기 삭제).
- **임포터 하나** = `SkillSheet`(`Scripts/Data/StatImport/SkillSheet.cs`): 계획 → 쓰기 전 diff 표(`[skills-diff]`) → apply. 없는 id(효과 · 소유자 · 탄 · 패턴 · 장판)는 보고만. `Skills` = id 별 부분 갱신(빈 칸 = 그대로) · `SkillOwners` = 탭에 나온 소유자의 `bindings` 재구성(시트-정본). 에디터 창에 「diff 미리보기(쓰지 않음)」 버튼.
- 경로: 로그인 자동 import · 로비 refresh(`DcSheetRuntimeRefresher` — 방어유닛 · 적 카탈로그 배선) · 에디터 창 import/export · push 바디 · 합본 export.
- U18: 액티브 문안의 비용 = 정의표 카드 값(`CardDef.Cost` = `AwakeningConfig.costActive`) · 모르면 비용 칸을 뺀다.
- 커밋: `658988c93`(U18) · `7dc32158f`(SkillSheet) · `980367c1d`(탭 계약) · `ba48b6742`(왕복 테스트).

## 남은 것 · 알려진 한계

- **모양 탭(`Projectiles` · `Patterns` · `PatternShots` · `Hazards` · `HazardEffects` · `Blockers`) 은 이 1부 밖** — 오늘 시트에 없는 값이라 회귀가 없고, 패턴 · 장판 SO 에 U10 이전 옛 피해 칸이 남아 있어 먼저 열면 죽은 열이 시트에 보인다. 소유자 표(`Cards` · `Units` · `Enemies`)는 기존 `DcCards` · `Defenders` · `Enemies` 탭 그대로.
- 런타임 refresh 는 효과가 **지금 가리키는** 탄 · 패턴 · 장판만 안다(에셋 스캔 없음 — 보고됨). 에디터 import 는 전부 안다.
- 빈 칸 = 그대로라 참조를 **비우는** 방법이 없다 · bool 을 끄려면 `FALSE` 를 적는다.
- push 는 업서트(고아 행 안 지움) — `SkillOwners` 에서 줄을 빼도 시트에 옛 줄이 남으면 다음 import 가 되살린다(`DcCardEffects` 와 같은 성질).
- export 는 기본값이 아닌 칸을 전부 쓴다 — 종류가 안 쓰는 칸(예: 광역 피해 효과의 `cc_kind`)도 값이 있으면 보인다. 「안 쓰는 칸 경고」(`tables.md` §2 검증)는 미구현.
- `DcSkills` 의 수치 칸(range · magnitude · durationSec · cooldownSec · warningSec)은 이제 **문안만** 움직인다(굽기 = `Skills` 의 액티브 효과 줄 + 카드 `cooldownSec`) — 두 곳을 고쳐야 같은 값이 된다(U18 분리 spec 후보).

## 실제 시트 설정 (사용자 몫 · 에이전트는 시트에 쓰지 않았다)
1. 탭 `Skills` 1행 헤더: `effect_id, kind, kind_ko, deprecated, magnitude_mode, basis_stat, ratio, damage, shield, percent, mul, count, radius_tiles, range_tiles, duration_sec, flight_sec, tick_sec, stack_cap, speed, cone_half_deg, density_radius_tiles, landing_ring_tiles, cc_kind, stack_kind, buff_stat, shield_filter, includes_self, telegraph, projectile_id, pattern_id, hazard_id`
2. 탭 `SkillOwners` 1행 헤더: `owner_kind, owner_id, slot, trigger, period, period_sec, fraction, subject, gate, gate_subject, gate_value, fire_cap, effect_id`
3. 서버: GET `/demo/google/sheet/{Skills|SkillOwners}` 확인 · Apps Script push 업서트 키 = Skills `effect_id` · SkillOwners (`owner_kind`, `owner_id`, `slot`).
4. 초기 데이터는 **에디터 export**(「Export Dreamcatcher → 시트 페이로드」)로 — 탭이 생기면 로그인 자동 import 가 적용하므로 손으로 친 값은 에셋을 덮는다.
5. `DcMechanics` 탭은 더 읽지 않는다(보관·삭제 자유). 탭이 생기기 전엔 두 탭 fetch 가 실패로 보고되고 아무것도 바뀌지 않는다.
