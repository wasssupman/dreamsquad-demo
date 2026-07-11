# 5. Handoff Summary — dreamcatcher-sheet-sync

## Commit

- `1eed2ee0` docs(spec): 스펙 초안 — 탭 6종 스키마 + 시드 JSON + 확장성 검증 반영
- `e338c7da` feat(import): DTO 6종 + 이원 배열 applier (unit 2)
- `2afd6a83` docs: unit 2 완료 기입 + 카드 스키마 레퍼런스(`docs/reference/dreamcatcher-card-schema.md`)
- `2608c179` feat(import): 6탭 Import/Export 에디터 배선 (unit 3)
- `c696dd9a` chore(data): no-op import 재직렬화 정규화
- `4e491126` balance(dreamcatcher): 작별선물 폭발 100→500 — 시트 경유 첫 밸런스 (왕복 ②)
- `a3f4c9a9` feat(import): 유닛 시트 awakeningReward 컬럼 (unit 4)

## Implemented

- 구글 시트 탭 6종(`DcCards`/`DcCardEffects`/`DcMechanics`/`DcAttackMods`/`DcSkills`/`DcConfig`) → 드림캐쳐 카드/스킬/config SO 부분 갱신. 기존 유닛 파이프라인 코어(SheetFetcher/SheetEnvelopeParser/BuildIndex) 재사용
- 배열 SoT 이원화: effects/attackMods = 시트 SoT(행 추가/삭제 = 효과 추가/삭제, 미등장 카드 유지, 길이 변화 리포트) · mechanics = Unity SoT(값 오버레이, projectile 참조 보존)
- `_` 접두 컬럼 계약 밖 규칙을 매퍼에 코드화 (import 무시 / export 정보 기록)
- `Window/Wassup/Unit Stat Import` 창에 Dreamcatcher 섹션 (Import + SO→JSON Export)
- AwakeningConfig/DeckRuleConfig 에 `id` 신설, `DcConfig` union 탭으로 갱신
- 유닛 시트 `awakeningReward` 컬럼 (Defender/Enemy DTO)

## Key Files

- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs` · `DcSheetApplier.cs` — DTO/적용 코어 (런타임 asmdef)
- `Assets/_Project/Editor/UnitStatImport/UnitStatImportWindow.cs` — DC 섹션 + `RunDcImport`/`ApplyDcFetched`
- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` — 탭별 JSON export
- `docs/spec/dreamcatcher-sheet-sync/1_seed_dreamcatcher.json` — 최초 시드 (이후 SoT 는 시트)
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs` — 16 테스트

## Verified

- EditMode 65/65, compile 0 error
- Export ↔ 시드 6탭 MATCH (시드 생성기와 exporter 상호 검증)
- 실 시트 왕복 ①: 47행 Matched 46/unmatched 0/skipped 0, 재추출 값 시드와 IDENTICAL
- 실 시트 왕복 ②: farewell magnitude 100→500 이 Card_Farewell 1개에만 반영

## Notes

- **시트가 값의 SoT** — 카드/스킬/config SO 를 Inspector 로 고치면 다음 import 때 시트 값으로 덮인다. SO 변경은 Export 로 시트에 되올린다.
- 구조(배열 항목 신설·에셋 참조·카드 신설)는 Unity 가 SoT. `DcMechanics` 행 추가는 시트에서 불가 — Unity 에서 추가 후 Export 재시드.
- 하드코딩 감사 결과 현행 awakening hand 경로는 클린. dormant 3중1 경로(`DreamcatcherController`) 의 `%5`/`Draw3` 는 레거시라 미시트화.
- 검증용 일회성 MenuItem(`DcSheetSyncOneShot`) 은 unityMCP execute_code 고장 우회 패턴 — 재검증 필요 시 동일 패턴으로 재작성.

## Incident (2026-07-11, 종결)

- 유닛 시트가 7/6 동기화 이후의 Unity 측 변경(한글 displayName, 밸런스, Enemy_Boss_Nightmare 추가)을 모르는 stale 상태에서 import → 25에셋이 구값으로 덮임. **git restore 로 전량 원복** 후 현재 SO export 로 시트 재시드, 재임포트 26유닛 semantic diff 0 확증 (`52429f54`).
- 교훈: SO 를 Unity 에서 고치면 즉시 Export 재시드. 방어책으로 dry-run diff 프리뷰(백로그) 우선순위 상향 권고.

## Follow-up

- **Import dry-run diff 프리뷰** [M] · 적용 전 "변경될 에셋/필드 목록" 표시 후 확인. staleness 사고의 구조적 방어 — 우선 추진 권고.
- ~~시트 Defenders/Enemies 탭에 `awakeningReward` 헤더 추가~~ 완료 2026-07-11 (값 왕복 확인)
- README 후속 후보: Dreamstones 탭 [S] · 런타임 리프레시 DC 확장 [M] · 드캐 외 하드코딩 밸런스(SynergyPerNeighbor 등) [S] · 기본 덱 id 리스트 노출 [S] · 시트發 카드 upsert [M]
