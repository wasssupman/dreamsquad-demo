# 4 — 프리셋 페이지 + 컨트롤러 + 확인 팝업

## 목적

스크롤 가능한 프리셋 목록 페이지를 런타임 빌드하고, 적용 버튼 → 확인 팝업 → 프로필 반영/저장까지 배선.

## 변경 대상

- 신규: `Assets/_Project/Scripts/UI/Outgame/PresetPage.cs` (`Wassup.UI`) — 런타임 빌더
- 신규: `Assets/_Project/Scripts/UI/Outgame/PresetPageController.cs` — 오케스트레이터/적용
- 신규: `Assets/_Project/Scripts/UI/Outgame/PresetConfirmPopup.cs` — 소형 확인 팝업

## 구현

### PresetPage (빌더)

`DreamcatcherDeckPage` 와 동형 — `PresetPanel` 아래 한 GameObject 에서 전체를 빌드하고 컨트롤러 주입.

- 세로 `ScrollRect` 구성은 `SquadRosterBrowser.EnsureGridBuilt` 재현하되 content 를
  `GridLayoutGroup` 대신 `VerticalLayoutGroup` + `ContentSizeFitter`(vertical PreferredSize) 로.
- `[SerializeField]`: `SquadPresetCollection collection`, `PlayerProfileSO profileSO`, `TMP_FontAsset font`.
- **ConfirmPopup 배선 정거장**: `PresetConfirmPopup` GameObject 도 이 빌더가 생성해(스크롤 위 오버레이,
  최상단 렌더) 컨트롤러에 `SetField` 로 주입한다 — detail/strip 주입과 동일 경로. 팝업은 Page/Controller 를
  역참조하지 않는 독립 소형 뷰.
- 빌드 후 컨트롤러 GameObject 를 비활성 생성 → 필드 주입(reflection `SetField`, 기존 페이지 방식) → 활성.

### PresetPageController (오케스트레이터)

- `OnEnable`: `collection` null 가드(미할당 시 조기 반환 + 경고 로그, NRE 방지) 후 `collection.presets`
  순회, 각 프리셋마다 `PresetListItemView.Build` 로 아이템 생성해 scroll content 에 부모링.
  각 `ApplyClicked` 를 해당 프리셋에 캡처해 구독.
- 적용 흐름:
  1. `PresetConfirmPopup.Show("현재 스쿼드·덱을 이 프리셋으로 교체합니다", onConfirm)`.
  2. onConfirm: 프리셋 SO 배열 → id 리스트 매핑
     (`units` 에서 `u?.id`, `cards` 에서 `c?.id`, null 항목은 스킵 또는 `""`).
  3. `PresetApply.WriteToProfile(profileSO.profile, unitIds, cardIds)`.
  4. 성공 시 `ProfileStore.Save(profileSO.profile)`.
- `profileSO`/`profile` null 가드(적용 단계). 목록은 `OnEnable` 마다 재빌드하거나 1회 빌드 후 재사용(구현
  재량 — 프리셋 집합은 authoring 정적이라 1회 빌드로 충분).
- **카탈로그 미등록 id**: 프리셋이 라이브 카탈로그에서 제거된 유닛/카드 SO 를 참조하면 적용은 그 id 를
  맹목 기록한다(카드는 START 게이트가 `unknown card` 로 차단, 유닛은 반입 시 조용히 누락). v1 은 이를
  방어하지 않음 — 런타임 검증은 README "후속 후보"(런타임 덱 규칙 검증) 참조.

### PresetConfirmPopup (확인 팝업)

- `MenuPopup` 의 빌드 스타일 참고 — dim 배경 + 메시지 TMP + [취소]/[적용] 두 버튼.
- `public void Show(string message, Action onConfirm)` / 내부 `Hide()`. [적용] → onConfirm+Hide, [취소] → Hide.
- 아웃게임 UI 전용 — TimeManager 등 전투 의존 없음(MenuPopup 의 pause 로직은 복제하지 않음).
- 단일 역할 소형 뷰. 새 Manager 싱글톤 아님(제약 5).

## 완료 기준

- [ ] Unity 컴파일 무오류.
- [ ] `collection` 의 프리셋 수만큼 아이템이 세로 스크롤 목록으로 렌더, 스크롤 동작.
- [ ] 적용 버튼 → 확인 팝업 → [적용] 시 프로필 변이 + `ProfileStore.Save` 호출, [취소] 시 무변화.
- [ ] 적용 후 스쿼드 페이지/드캐 페이지를 다시 열면(각 `OnEnable` 재빌드) 교체된 로드아웃이 반영됨.
- 확인 2026-07-20 (커밋 05c7c7b8): Play — 프리셋 2개 세로 스크롤 목록 렌더, 적용→확인팝업→PresetApply→ProfileStore.Save e2e(센티넬 덮어써짐 + profile.json 반영) 확인.
