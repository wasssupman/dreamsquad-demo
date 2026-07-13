# 3. Dreamcatcher 런타임 리프레셔

## 목적

로비에서 드림캐쳐 6탭(DcCards/DcCardEffects/DcMechanics/DcAttackMods/DcSkills/DcConfig) 최신 시트값을 내려받아 **메모리 내 SO 인스턴스에 즉시 반영**한다. 유닛 리프레셔(`UnitStatRuntimeRefresher`)와 동일 패턴 — in-memory only, 세션 한정, dev/QA 전용.

## 런타임 SO 열거 소스 (전량, 결정 2026-07-13)

AssetDatabase 스캔 불가 → 명시 참조로 29카드+6스킬+2config 전량 커버:

| 대상 | 소스 |
|---|---|
| deck 카드 23 | `DreamcatcherCardCatalog.cards` |
| Active 카드 6 | `[SerializeField] DreamcatcherCard[] activeCards` (카탈로그 미등록) |
| 스킬 6 | `activeCards[].skill` (Active 카드가 감싼 SkillData) |
| AwakeningConfig | `[SerializeField] AwakeningConfig awakeningConfig` |
| DeckRuleConfig | `cardCatalog.ruleConfig` |

cardsById = (catalog.cards ∪ activeCards) 인덱스 → DcCards flat 필드가 Active 카드에도 적용.

## 변경 대상

- `Assets/_Project/Scripts/Core/Dreamcatcher/DcSheetRuntimeRefresher.cs` (신설, 런타임 asmdef).
  - SerializeField: `cardCatalog`, `activeCards[]`, `awakeningConfig`, `baseUrl`(기본 dev API). 6탭명은 계약 고정이라 const 배열.
  - `Refresh(Action<string> onDone)` — `SheetFetcher.FetchAll(6 urls)` → `ApplyBodies`. `RequestInFlight` 가드(유닛 리프레셔 동일).
  - `internal static string ApplyBodies(SheetFetcher.Result[] r, string[] tabs, catalog, activeCards, awakeningConfig)` — 순수 코어(네트워크 없이 EditMode 테스트 가능). 6탭 파싱 → `DcSheetPayload` → BuildIndex(cards/skills/configs) → `DcSheetApplier.Apply(..., onApplied: null, log)`.
- `Assets/_Project/Tests/EditMode/DcSheetRuntimeRefresherTests.cs` (신설) — 합성 6탭 body → ApplyBodies → 카탈로그 SO 값 반영/미등장 유지 1케이스.

## 구현 노트

- 코어 재사용: `SheetFetcher`/`SheetEnvelopeParser.ParseSheetLogged`/`UnitStatApplier.BuildIndex`/`DcSheetApplier.Apply` 전부 런타임 asmdef. 에디터 `ApplyDcFetched`(UnitAssetScan 스캔)와 규칙 동일, 열거 소스만 카탈로그/참조로 교체.
- `onApplied: null` — 디스크 미저장(에디터 전용 API 금지, feature 계약).
- 실패 처리: 탭별 독립(ParseSheetLogged 가 tab별 에러 로깅), 전 탭 실패 시 로그만. 네트워크 불가 시 기존 값 유지.

## 완료 기준

- [x] compile 0 error (런타임+테스트 asmdef).
- [x] EditMode: 합성 body → ApplyBodies 로 카탈로그/Active/스킬/config SO 값 반영, 미등장 카드 유지 (`DcSheetRuntimeRefreshTests` 2케이스, EditMode 722 pass).
- [x] `onApplied` 경로에 AssetDatabase/SetDirty 없음(런타임 asmdef).

확인 2026-07-13 — 씬 배선 컴포넌트 실구동(one-shot) Matched 60/unmatched 0/skipped 0.
