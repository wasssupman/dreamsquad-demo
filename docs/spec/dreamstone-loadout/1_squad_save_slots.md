# 1 — SquadSave 스톤 슬롯

## 목적

스쿼드별 드림스톤 장착 상태를 프로필에 영속한다. 스톤은 스쿼드 소속(계정 소속 아님).

## 변경 대상

- `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` (`SquadSave`)
- `Assets/_Project/Scripts/Core/Profile/ProfileStore.cs` (정규화 경유 확인 — 코드 변경 없을 수 있음)
- `Assets/_Project/Tests/EditMode/ProfileStoreTests.cs` (케이스 추가)

## 구현

- `SquadSave`: `public const int StoneSlotCount = 4;` + `public List<string> stoneIds` (`DreamstoneData.id`, 빈칸 `""`).
- `NormalizeSlots()` 확장: `stoneIds` 도 길이 4 로 pad/trim, null 항목 → `""`. 기존 `unitIds` 정규화와 동일 방식. `ProfileStore.EnsureDefaultSquad` 가 로드/생성 시 스쿼드마다 `NormalizeSlots()` 를 이미 호출하므로 **추가 훅 불필요** — 확장이 그 경로에 자동 편승하는지 확인만 한다.
- **중복 장착 허용** — 정규화에서 dedup 하지 않는다.
- 장착 로직 순수 도우미: `SquadSave.SetStoneSlot(int index, string id) : bool` — 지정 슬롯에 배정(`""` = 해제), 범위 밖 index 는 false. 피커 모달(C안)이 슬롯을 지정해서 장착하므로 "첫 빈 슬롯" 탐색은 없다. 중복 id 허용이 **의도적**임을 테스트로 고정한다 (누군가 유닛 슬롯 로직과 통합하려 할 때 회귀 방지). UI(unit 2)는 이 도우미만 호출.
- 구버전 JSON(stoneIds 필드 없음) → 역직렬화 시 null → 정규화가 빈 4슬롯으로 복원. `schemaVersion` 증가 없음(additive 필드).
- 카탈로그에 없는 id(에셋 삭제 등)는 저장 계층에서 걸러내지 않는다 — 해석 시점(UI 표시/반입)에서 null 해석 → skip. (`squad-loadout` 의 unitIds 와 동일 정책.)

## 완료 기준

- EditMode 통과:
  - 라운드트립 — 장착 저장 → 로드 후 stoneIds 유지 (중복 항목 포함)
  - 정규화 — null / 길이 부족 / 길이 초과 각각 4슬롯으로
  - 구버전 JSON 호환 — stoneIds 없는 JSON 로드 시 빈 4슬롯
  - SetStoneSlot — 지정 슬롯 배정 / `""` 해제 / 중복 허용 / 범위 밖 index false

> 완료 확인 2026-07-04 — EditMode 12/12 PASS (리뷰어 테스트 리그 배치 실행, ProfileStoreTests+DreamstoneCatalogTests), compile clean.
