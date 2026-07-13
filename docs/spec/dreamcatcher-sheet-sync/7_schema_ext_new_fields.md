# 7. DcMechanics 스키마 확장 — Spec A/B 신필드 4종

## 목적

`dreamcatcher-sheet-sync`(units 0~6, 완료 2026-07-11) 이후 Spec A(`dreamcatcher-new-abilities`) · Spec B(`dreamcatcher-kill-and-threshold`)가 `DcTriggerSpec`/`DcPayloadSpec` 에 신필드를 추가했다. 이 필드들은 SO 에는 있으나 `DcMechanics` 탭 스키마(DTO/exporter/applier)에 컬럼이 없어 **기획 시트에서 편집 불가**(import 는 partial-update 라 손실은 없고, 값을 담지/되올리지 못할 뿐). 4종 컬럼을 추가해 라운드트립을 완성한다.

## 신필드 4종 (평탄화 컨벤션 = unit 0 계약 승계)

| SO 필드 | 타입 | 시트 컬럼(헤더) | 소비 payload/trigger | 출처 |
|---|---|---|---|---|
| `trigger.fraction` | float | `triggerFraction` | HealthThreshold 트리거 경계비율 | Spec B (last_stand) |
| `payload.ccKind` | DcCcKind | `ccKind` | ApplyCcToTarget (Stun/Impulse) | Spec A (frost_arrow) |
| `payload.stackKind` | DcStackKind | `stackKind` | ApplyStackToTarget (Fire/Ice/Bleed/Poison) | Spec A (ember_bite) |
| `payload.buffStat` | CardBuffKind | `buffStat` | SelfStatBuff 대상 스탯 (AttackDamage/AttackSpeed…) | Spec B (last_stand/devouring) |

- **trigger.* → 접두**(`triggerFraction`, `triggerKind`/`triggerPeriod` 선례). **payload 스칼라 → 무접두**(`ccKind`/`stackKind`/`buffStat`, `magnitude`/`tileRange`/`duration` 선례).
- enum 은 C# 멤버명 문자열(case-insensitive) — 기존 컨벤션 그대로.
- `DcMechanics` 는 **Unity-SoT 탭**(projectile 에셋 참조 보유) → 신필드도 **overlay-only**(slot 존재 시 값 덮어쓰기). 구조/신규 슬롯은 Unity authored + Export 재시드.

## 변경 대상

- `Assets/_Project/Scripts/Data/StatImport/DcSheetImportDto.cs` — `DcMechanicDto` 에 `float? triggerFraction`, `DcCcKind? ccKind`, `DcStackKind? stackKind`, `CardBuffKind? buffStat` 추가.
- `Assets/_Project/Editor/UnitStatImport/DcSheetExporter.cs` (`:61-68` 부근) — export row 에 4필드 기록.
- `Assets/_Project/Scripts/Data/StatImport/DcSheetApplier.cs` (`OverlayMechanics`, `:205-210` 부근) — non-null DTO 필드를 `m.trigger.fraction`/`m.payload.{ccKind,stackKind,buffStat}` 에 대입 후 struct 재대입.
- `docs/spec/dreamcatcher-sheet-sync/0_json_schema_contract.md` — `DcMechanics` 헤더 표(:37-46) + `payloadKind → 사용 컬럼` 매트릭스(:91-99)에 4컬럼 반영.
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcSheetImportTests.cs` — overlay 신필드 갱신 1케이스(ccKind/stackKind/buffStat/triggerFraction non-null → SO 반영) + enum 오타 실패 가드.

## 스코프 밖 (후속 후보)

- **`periodSeconds`**(PeriodicTimer 트리거 주기초) — 동일 클래스의 5번째 시트 갭. 현재 사용자 요청(4필드) 밖. 필요 시 동일 패턴 1줄로 추가.
- `auraPrefab`/`auraScale` — 에셋 참조 + 스칼라. 시트 계약 밖 유지(Unity authored).
- 시트發 신규 카드 upsert — 기존 계약대로 update-only.

## 구현 노트

- `OverlayMechanics` 는 이미 trigger/payload 접두 평탄화를 **수동 매핑**(리플렉션 불가 지점). 4필드도 동일하게 `if (dto.X.HasValue) target.X = dto.X.Value;` 라인 추가 — 순수 값 복사, SO/에디터 API 무관 → 런타임 asmdef, EditMode 테스트 대상.
- `magnitude` 컨벤션 확인(문서만): SelfStatBuff 는 `magnitude` = **퍼센트**(last_stand 30, devouring 8) → bake 시 `MapDcBuff` 가 배율 변환. ApplyStackToTarget 는 `magnitude` = 스택 수(ember 1). 시트 매트릭스에 명시.

## 완료 기준

- [x] compile 0 error (4어셈블리).
- [x] EditMode: 신필드 overlay 갱신 케이스 통과 + 기존 `DcSheetImportTests`/`UnitStatImportTests` 전체 green. (718 pass / 0 fail / 2 pre-existing skipped)
- [x] Export 경로에 4컬럼 배선(`MechanicRow : DcMechanicDto` 상속 → 자동 emit). code-review 로 exporter/applier/DTO 3-way 대칭 검증. (실 구글시트 왕복은 사용자 시트 반영 후.)
- [x] 스키마 계약 문서(0_) 갱신 — DcMechanics 헤더 표 + payloadKind 매트릭스.
- [x] 사용자에게 넘길 **시트 업데이트 JSON** 제공 — `7_sheet_update.json` (신규 5카드 cards/cardEffects/mechanics 행).

확인 2026-07-13 — 커밋 `27611a30`. code-review APPROVE(CRITICAL/HIGH/MEDIUM 0). 실 구글시트 반영 + import 왕복 검증은 사용자 몫(운영 규칙 1~3).
