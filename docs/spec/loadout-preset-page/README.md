# loadout-preset-page

> 상태: 완료 2026-07-20 (커밋 05c7c7b8) — 컴파일 그린 · EditMode 9/9 · Play e2e(패널 렌더 + 적용→팝업→프로필→디스크 저장) 검증

## 목표

로비에 **프리셋** 버튼을 추가하고, 기획자가 SO 로 authoring 한 "스쿼드 7 + 드림캐쳐 10" 완성 덱을
스크롤 목록으로 보여주는 프리셋 페이지를 구현한다. 각 프리셋에는 **적용** 버튼이 있고, 적용하면
현재 선택된 스쿼드·드림캐쳐 덱의 내용을 그 프리셋으로 덮어쓴다.

### 검증 질문

플레이어가 로비에서 프리셋 페이지를 열어, 기획자가 만든 스쿼드+드림캐쳐 덱 목록을 스크롤로 보고,
하나를 **적용**해 현재 스쿼드·덱을 교체할 수 있는가?

## 연결 문서

- 데이터 저장: `Assets/_Project/Scripts/Core/Profile/PlayerProfile.cs` (`SquadSave`, `DeckSave`), `ProfileStore.cs`
- 재사용 대상 UI: `SquadRosterBrowser`(스크롤 그리드 구성), `DreamcatcherDeckStrip`(카드 아트 슬롯/폴백),
  `SquadHeaderStrip`(유닛 슬롯 비주얼 기준), `MenuPopup`(팝업 빌드 패턴), `OutgameMenuController`(로비 패널 라우팅)
- 스타일 헬퍼: `UnitRarityStyle.Frame`, `CardCategoryStyle.Frame/ArtFallback`

## 구현 문서 목록

| # | 작업 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_data_model.md` | `SquadPresetCollection` + `SquadPreset` SO 정의 |
| 1 | Core 로직 | `1_apply_helper.md` | `PresetApply.WriteToProfile` 정적 헬퍼 + EditMode 테스트 |
| 2 | UI 뷰 | `2_preset_unit_cell.md` | 읽기전용 `PresetUnitCell` (스쿼드 셀 스타일) |
| 3 | UI 뷰 | `3_preset_list_item.md` | `PresetListItemView` (이름 + 셀 7 + 아트 10 + 적용 버튼) |
| 4 | UI 통합 | `4_preset_page_and_controller.md` | `PresetPage` 런타임 빌더 + `PresetPageController` + 확인 팝업 + 적용 배선 |
| 5 | 씬 배선 | `5_lobby_wiring.md` | `OutgameMenuController` 버튼/패널 + UnityMCP 씬 와이어링 + Play 검증 |

## feature-wide 계약 / 공통 원칙

1. **프리셋 = authored SO 데이터.** `SquadPresetCollection` 하나가 `List<SquadPreset>` 를 보유하고,
   개별 `SquadPreset` 는 `DefenderUnitData[]`(≤7)·`DreamcatcherCard[]`(≤10) 를 **SO 직접 참조**로 담는다.
   id 는 저장하지 않으며 적용 시점에만 `.id` 를 읽는다.
2. **적용 = 제자리 덮어쓰기 (기본값 미생성).** 적용은 프로필의 **선택된** `SquadSave.unitIds`(7슬롯)와
   **선택된** `DeckSave.cardIds` 내용을 교체할 뿐, 새 스쿼드/덱 엔트리를 만들거나 선택 대상
   (`selectedSquadId`/`selectedDeckId`)을 바꾸지 않는다. 선택 스쿼드·덱은 **이미 존재한다는 전제**다 —
   신규유저 기본 로드아웃(squad_1/deck_1) 주입은 `ProfileStore.EnsureDefaultSquad/EnsureDefaultDeck`
   (load 시점, `DreamcatcherDeck_Default` 시드) **단독 소유**. 프리셋은 기본값을 생성하지 않고, 둘 중
   하나라도 없으면 부분 적용 없이 no-op(false).
3. **드림스톤 제외.** 프리셋은 유닛·드림캐쳐만 관리한다. 적용 시 `SquadSave.stoneIds`(4슬롯)는 건드리지 않는다.
4. **적용 시 덱 규칙 검증 없음(v1).** 프리셋은 유효하게 authoring 된다는 전제. 잘못된 덱은 기존 START
   로드아웃 게이트(`LoadoutGate.Check`)가 여전히 잡는다. 런타임 검증은 후속 후보.
5. **저장 분리.** `PresetApply.WriteToProfile` 는 프로필을 **변이만** 하고 디스크에 쓰지 않는다.
   `ProfileStore.Save` 호출은 호출처(컨트롤러) 책임 — 헬퍼는 EditMode 순수 테스트 대상으로 유지.
6. **읽기전용 페이지.** 프리셋 페이지에서는 편집이 없다. 셀·아트는 탭/드래그/편성 기능 없이 표시만 한다.
7. **런타임 빌더 패턴.** `SquadCharacterPage`/`DreamcatcherDeckPage` 와 동일하게 코드로 UI 를 빌드하고
   컨트롤러를 주입한다. 아이템 프리팹은 authoring 하지 않는다.
8. **로비 패널 라우팅 재사용.** 프리셋 패널은 `OutgameMenuController.RaiseExclusive` 를 통해 배타적으로 열고,
   `ClosePanels` 에 포함시킨다(다른 패널과 동일 규약).

## 파이프라인 커버리지

**N/A** — 신규 플레이 오브젝트(유닛/적/투사체/해저드/VFX)나 생성→렌더 경로 변경이 없다.
본 spec 은 아웃게임 UI + 프로필 저장 데이터만 다루므로 `object-pipeline-map.md` 대조 대상이 아니다.

## 후속 후보 (현 스코프 밖)

- 적용된 프리셋을 목록에서 "적용됨/선택됨" 으로 시각 하이라이트.
- 적용 시 덱 규칙(`DeckRules.Validate`) 런타임 검증 및 사유 안내.
- 프리셋에 드림스톤 4슬롯 포함(완전 스쿼드 스냅샷).
- 플레이어가 현재 로드아웃을 새 프리셋으로 저장(런타임 authoring). 현재는 기획자 authoring 전용.
- 프리셋 아이템에서 상세 보기(유닛/카드 탭 → 상세 패널).
