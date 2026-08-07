# 5. 신규 맵의 dev 슬롯 노출 (Map Stepper 편입)

rev 2026-08-07 — 페인터로 Bake 한 신규 맵을 로비 맵 스테퍼(`DevMapOverridePanel`)에서 바로 진입할 수 있게 한다 (사용자 요청).

## 목적

신규 MapDocument 는 풀 미등록이라 스테퍼로 진입할 수 없었다. **풀 본편(`entries`)에 넣는 방법은 금지** — `Count` 가 바뀌면 `seed % Count` 매핑이 전면 재배정되어 라이브 토너먼트 맵 결정론이 오염된다(tournament-seed-map-select 계약). 대신 시드 선택에 불가시인 dev 전용 슬롯을 풀에 추가한다.

## 변경 대상

- `Assets/_Project/Scripts/Data/MapGrid/MapDocumentPool.cs` — `devEntries` + `DevCount`/`GetDev` + 에디터 전용 `EditorRegisterDevDocument`(본편/dev 중복 거부)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — DevMapOverride 인덱스 해석을 `[0..Count+DevCount-1]` 로 확장(시드 3분기는 `Count` 만 봄 — 결정론 불가시)
- `Assets/_Project/Scripts/UI/DevMapOverridePanel.cs` — 순환 슬롯: 풀 → dev(`D{n}:이름` 라벨) → ENDLESS
- `Assets/_Project/Editor/MapPainterWindow.cs` — Bake 후 대상 문서가 풀 어디에도 없으면 dev 슬롯 자동 등록(`t:MapDocumentPool` 첫 asset)
- `Assets/_Project/Tests/EditMode/MapDocumentPoolDevEntriesTests.cs`

## 계약

1. **`entries` 불변**: dev 슬롯은 `Count` 에 절대 포함되지 않는다. 랜덤/토너먼트/폴백 맵 선택은 byte-identical.
2. **진입 경로는 DevMapOverride 뿐**: dev 인덱스 = 풀 뒤 이어붙은 슬롯(`Count + j`). deck 은 null = 레거시 serialized deck 폴백 계약 재사용.
3. **자동 등록은 Bake 시점, 에디터 전용, 중복 거부**: 이미 풀 본편에 있는 맵(기존 6종 재베이크)은 dev 로 이동하지 않는다.
4. dev 슬롯의 라이브 풀 승격은 수동 — 풀 asset 에서 entries 로 옮기는 순간 토너먼트 경계 규칙(PRD 계약)을 따른다.

## 완료 기준

- EditMode: 등록 dedup(본편/dev/null 거부·Count 불변) + BattleBridge dev 인덱스 해석(devEntries 문서로 빌드) 테스트 그린.
- 에디터: 신규 문서 Bake → 콘솔 "dev 슬롯에 등록" 로그 → 로비 스테퍼에 `D0:{이름}` 노출 → 배틀 진입 확인.
- 기존 스테퍼 동작(풀 인덱스·ENDLESS·OFF) 불변.
