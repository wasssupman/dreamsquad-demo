# 2 — 스쿼드 페이지 장착 UI (슬롯 탭 → 피커 모달)

## 목적

스쿼드 페이지를 "슬롯만 보이는 메인 + 슬롯 탭 시 피커 모달" 구조로 재편해, 유닛 편성과 드림스톤 장착을 동일 인터랙션으로 제공한다 (2026-07-04 UI 리뷰 C안).

## 변경 대상

- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs` (구조 재편)
- OutgameScene — `DreamstoneCatalog` 참조 wiring (UnityMCP, `unity-feature-wiring` 스킬 절차)
- rev 2026-07-04 (에디터 락, wiring 최소화 결정): `stoneSlotsContainer` 는 runtime fallback 을 가지므로 **필수 wiring 아님** — 씬 wiring 필수 항목은 `stoneCatalog` 참조 하나뿐.

## 구현

- `SquadBuilderView` 에 `[SerializeField] DreamstoneCatalog stoneCatalog` 추가.
- **메인 화면**: 유닛 슬롯 7 + 스톤 슬롯 4 만 표시. 기존 "보유 유닛 그리드"는 메인에서 제거하고 피커 안으로 이동. 라벨: 유닛 = 표시명, 스톤 = 스탯 약칭 + 수치(예: `ATK +7.5%`) + 등급 색 배경.
- **피커 모달** (런타임 생성 오버레이 패널 1개를 유닛/스톤 겸용 재사용, 기존 `CreateButton` 패턴):
  - 유닛 슬롯 탭 → 보유 유닛 그리드. 선택 = **탭한 슬롯**에 배정. [해제] = 슬롯 비움. [닫기].
  - 스톤 슬롯 탭 → 카탈로그 전체(16종) 그리드. 선택 = `SquadSave.SetStoneSlot(index, id)` (unit 1 도우미). 중복 장착 허용. [해제]/[닫기] 동일.
  - 모달 열림 중 배경 입력 차단(반투명 스크림). 뎁스는 모달 1장 — 추가 네비게이션 없음.
- 등급 색 (MVP, 아이콘 아트 후속): Common 회색 / Rare 파랑 / Epic 보라 / Unique 주황.
- 저장: 기존 저장 흐름(`ProfileStore.Save` 경유)에 편승 — `Squad` 객체 직접 수정. `Refresh()` 가 유닛/스톤 슬롯 라벨을 함께 갱신.
- 유닛 중복 배정 정책은 현행 유지(변경 없음 — 반입 시 `SquadDraw` 가 dedup). 카탈로그에 없는 id 가 슬롯에 있으면(에셋 삭제) id 원문 표시 + 피커 [해제] 가능.
- **`stoneSlotsContainer` runtime fallback** (rev 2026-07-04): `EnsureStoneSlotsContainer()` — `stoneSlotsContainer == null` 이면 `slotsContainer` (유닛 슬롯 행)의 RectTransform anchor/pivot/size + `HorizontalLayoutGroup` 설정을 그대로 미러해 한 행 아래(`slotsContainer.sizeDelta.y + 20` 만큼 offset)에 런타임 GameObject 를 생성한다. `slotsContainer` 도 없으면 `this.transform` 아래 안전망 배치. 씬에서 `stoneSlotsContainer` 를 나중에 authoring 하면 그 값이 우선(필드가 non-null 이면 fallback 미실행).

## 완료 기준

- compile 클린 + OutgameScene wiring 완료 (`stoneCatalog` 참조 할당 — `stoneSlotsContainer` 는 runtime fallback 이라 필수 아님)
- 에디터 Play: 유닛/스톤 각각 슬롯 탭 → 피커 → 장착/해제/저장 → 씬 재진입 후 장착 상태 유지
- 스톤 중복 장착(같은 스톤을 2개 슬롯에) 동작 확인
- 게임뷰 스크린샷으로 메인/모달 레이아웃, 등급 색 육안 확인

> 완료 확인(부분) 2026-07-04 — 코드 구현 + 리뷰 M1 반영 + 리그 게이트 PASS(compile clean, EditMode 12/12 회귀 green). OutgameScene wiring(stoneCatalog/stoneSlotsContainer, 구 ownedContainer 정리) + Play 육안 검증 pending.
> 완료 확인(2차) 2026-07-04 — OutgameScene `stoneCatalog` 씬 배선(YAML 1줄) + `stoneSlotsContainer` 런타임 fallback 커밋. 리그 게이트 PASS(compile + EditMode 12/12 + PlayMode 4/4, Outgame 왕복 포함). 잔여: 스쿼드 페이지 Play 육안/스크린샷(레이아웃·피커 렌더·등급 색, 구 ownedContainer 정리 여부 판단).
