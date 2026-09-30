# 10 — 인계 요약 (2부 · 시트 구조 리팩토링 + 병합 전 감사)

## Commit
브랜치 `unified-effect-layer`(워크트리 `wassup-core`) · 1부 인계 = `6_handoff_summary.md`. 2부 경계:
- 스펙 · 결정 `70fb9eff0` `b6c3db8ba` `6b2079b8e` `953fe0c3b`(unit 7 보류 · 범위 축소)
- unit 8 단계 A `bc68220da` `8f3a8b2d2` `e32db6849` `dddeb1eec` `33a8f3a58` `1779d9436` · 이전 적용 `43e6d841f` · 단계 B `258a3a0cd` `5825fed06` `2f2fefee5` `87476471c` · 재직렬화 `c61bc5460` · 기록 `169be54a7`
- unit 9 `609a52ef3` `18de2e81f` `e2a29fe35` `3c8c279aa` `237a97596` `428a645aa` `435650b45` · 기록 `ac09ad8ed` `38d3ead06`
- 병합 전 감사(4갈래 비평 → 리드 대조) — 코드 `3c31dfe9f` `793a4b688` `105d17802` `02a7dbddc` `0832cf84e` `c66b11899` `645c89ef2` `2558c02f0` · 문서 `bfc0c544c` `7ec57491e` `88f02b370` `2f95c1f71` `e68bea4fa` `ff76e0b28`

## Implemented
- 상시 효과도 스킬 줄(계약 11): 스쿼드 스탯 15 · 공격 수식자 3 → 효과 에셋 18 + 카드 소유 줄(트리거 `None`) · 효과 종류 append 39~42 · 빌더가 기존 코어 모양으로 편다(새 트리거 0).
- 수혜 대상 = 효과 칸 `ally_filter`(계약 12) · Squad 머리 칩 = 첫 진영 버프 줄에서.
- 카드 전용 저장처 · 탭 은퇴: `DreamcatcherCard.effects` · `attackMods` · `DcCardEffects` · `DcAttackMods`.
- 액티브 문안 한 원천: 시전 줄 효과 값 + 카드 `cooldownSec`(`SkillData` 수치는 아무도 안 읽는다).
- 시트 8탭(`Skills` · `SkillOwners` · `Cards` · `Defenders` · `Enemies` · `DcSkills` · `DcConfig` · `CostConfig`) · 전 탭 스네이크 · `DcCards` → `Cards`(+ `host_kinds` · `cooldown_sec` · `needs_two_tiles` — 액티브 줄만) · `Skills` = 종류가 쓰는 칸만, 기본값이어도.
- U20: 공격 변형 효과를 적 · 적 숙주 카드가 들면 그 소유자의 시트 줄 전체를 건너뛴다(시트 층만 · 계약 13).
- 방어유닛 · 적 × 상시 4종 · 부착 즉시 전용 4종 = `NotWired`(배선 전 — unit 7 후속).
- 감사 반영: 효과 id 계약 10 강제(테스트 + 굽기 Error) · 발동 문맥 없는 적용의 시전 진영 = None(말하고 버린다) · 브레스 반각 ≤ 0° 거절 · `SkillCone` 은퇴.

## Key Files
`Scripts/BattleCore/Trigger/{TriggerKinds,EffectComboRule,SkillRouting,IntentApplier}.cs` · `Scripts/Data/Effects/{EffectValues,EffectSlots}.cs` · `Scripts/BattleCoreUnity/{BindingSpecBuilder,CardDefinitionBuilder,BindingDefinitionBuilder}.cs` · `Scripts/Data/StatImport/{SkillSheet,SheetColumns,DcSheetTabs,DcSheetImportDto}.cs` · 헤더 정본 `5_sheet_io.md` 「실제 시트 설정」(문서 대조 테스트 `SheetHeaderDocTests`) · 이전 기록 `dry-run/dry_run_part2.md`.

## Verified (2026-09-30 · HEAD `2558c02f0` + 이 문서)
헤드리스 1038/0/4 · Unity EditMode 3어셈블리 2650 — 실패 3 = 선행(카드 아트 중복 · 설명 어긋남 7장 · bomb_man) · PlayMode Core 97/97 · 굽기 스냅샷 2종 무변(값) · 전 탭 왕복 · 실제 시트(8탭) 읽기 전용 대조 = 빠진 줄 · 열 · 값 0 · 에디터 미리보기 `Skills`/`SkillOwners` 바뀌는 칸 0 · core-reviewer APPROVE(unit 8 · 통합 효과 층 unit 7).

## Notes
- **시트는 이미 새 형식이다.** `main` 에 병합되기 전 빌드는 옛 탭 · 옛 열을 읽어 로그인 import 가 아무것도 바꾸지 못한다(에셋 값으로 돈다) — 병합 · 푸시를 미루지 말 것.
- 빈 칸 = 그대로 · `SkillOwners` = 탭에 나온 소유자의 줄 재구성(시트 정본) · 푸시는 업서트(고아 행 안 지움).
- `SkillOwners.subject` 값은 `Self` · `Any` — 다른 글자를 치면 그 탭 전체가 파싱 실패로 적용되지 않는다.
- 효과 에셋 62개의 안 쓰는 칸(`cc_kind` Stun 등 unit 4 이전 기본값)은 에셋 · 해시에 남는다 — 지우면 재베이크.

## Follow-up
- 사용자: Apps Script 업서트 키(새 탭 `Skills` = `effect_id` · `SkillOwners` = (`owner_kind`,`owner_id`,`slot`) · `Cards` = `id`) 설정 후 첫 Push · 개사기 · 별똥 타격 전용 아트(보류 중) · 설명 어긋남 7장 · bomb_man 문안 · `kind_ko` 43 검토 · 병합 · 푸시 승인.
- 후속 spec 후보(README 「후속 후보 (2부)」): unit 7 · U18 액티브 분리 · 옛 메커닉 번역 층 제거 · 명칭 B7 · B10 · 설정 탭 통합 · 분열 적 칸 · 드림스톤 시트.
