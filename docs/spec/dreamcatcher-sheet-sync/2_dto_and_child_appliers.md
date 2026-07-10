# 2. DTO 6종 + 자식 테이블 applier + config id

## 목적

탭 6종의 DTO 와 `(cardId, slot)` 매칭 부분 갱신 로직을 런타임 어셈블리에 구현하고 EditMode 테스트로 고정한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/AwakeningConfig.cs` · `DeckRuleConfig.cs` — `public string id;` append (기존 에셋 값은 에디터에서 `awakening_default`/`deck_rule_default` 로 기입)
- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs` (신규) — `DcCardDto`, `DcCardEffectDto`, `DcMechanicDto`, `DcAttackModDto`, `DcSkillDto`, `DcConfigDto` (전 필드 nullable, 헤더=필드명)
- `Assets/_Project/Scripts/Data/StatImport/DcSheetApplier.cs` (신규) — 적용 코어
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs` (신규)

## 구현

- **DcCards/DcSkills**: `UnitStatApplier.BuildIndex` + `UnitStatFieldMapper.ApplyNonNullFields` 재사용 (평평한 필드는 리플렉션 이름복사). type↔binding 부정합은 경고 로그.
- **시트 SoT 자식 탭 (DcCardEffects/DcAttackMods)**: cardId 별로 행을 그룹핑(slot 오름차순) → 배열 전체 재구성. 탭 미등장 카드 유지, 길이 변화 리포트, `(cardId,slot)` 중복 시 해당 카드 스킵 (unit 0 시맨틱).
- **Unity SoT 자식 탭 (DcMechanics)**: slot 유효 범위 검사 후 해당 배열 항목 struct 를 읽어 non-null DTO 필드만 갱신 → 통째 재대입 (struct 배열, projectile 참조 보존). trigger/payload 접두 평탄화는 수동 매핑 (리플렉션 불가 지점 — 필드 6~8개라 수동이 명확).
- **DcConfig**: id 인덱스로 두 config SO 를 한 탭에서 갱신. 각 DTO 행을 두 SO 타입에 순차 시도하지 않고, id→SO 레지스트리로 직접 매칭.
- 중복 `(cardId, slot)` / 범위 밖 slot / 미지 id → 스킵+리포트 카운트 (기존 로그 포맷).
- 순수 함수 원칙: 매핑/검증 로직은 SO·에디터 API 무관 → 전부 런타임 asmdef, EditMode 테스트 대상.

## 완료 기준

- [ ] compile 0 error
- [ ] EditMode 테스트: 정상 갱신 / 빈 셀 유지 / enum 오타 실패 / union config 부분 갱신 — 각 1케이스 이상
- [ ] 시트 SoT 탭 테스트: 행 추가로 배열 성장 / 행 감소로 축소+리포트 / 탭 미등장 카드 유지 / 중복 (cardId,slot) 스킵
- [ ] Unity SoT 탭 테스트: 범위 밖 slot 스킵 / projectile 참조 보존
- [ ] 기존 UnitStatImportTests 전체 그린 유지
