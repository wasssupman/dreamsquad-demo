# 6 — Handoff Summary

## Commit
- `05c7c7b8` feat(loadout-preset-page): 로비 프리셋 페이지 + 적용 (units 0-5)
- `92a1b8aa` docs: 드림캐쳐 덱 규칙=10 확정 — stale '8장' 참조 정정 (곁가지)

## Implemented
- 로비에 "프리셋" 버튼 추가 → `PresetPanel` 배타적 오픈(`OutgameMenuController.OnOpenPreset`).
- `SquadPresetCollection` SO: `List<SquadPreset>`(각 스쿼드7유닛 + 드캐10카드, **SO 직접참조**).
- 프리셋 페이지: 세로 스크롤 목록, 각 아이템 = 이름 + 유닛 셀 7 + 드림캐쳐 아트 10 + [적용].
- [적용] → 확인 팝업 → 선택 스쿼드 `unitIds`(7) + 선택 덱 `cardIds` 교체 → `ProfileStore.Save`.
- 드림스톤 4슬롯은 **미변경**(계약 3). 선택 덱 없으면 `deck_1` 생성/선택.
- 샘플 프리셋 2개("추천 스쿼드 A/B") 카탈로그 기반 authoring, `SquadPresetCollection.asset`.

## Key Files
- `Assets/_Project/Scripts/Data/Preset/SquadPresetCollection.cs` — SO 정의
- `Assets/_Project/Scripts/Core/Profile/PresetApply.cs` — 순수 적용 헬퍼(+ `Tests/EditMode/Profile/PresetApplyTests.cs`)
- `Assets/_Project/Scripts/UI/Outgame/PresetUnitCell.cs` / `PresetListItemView.cs` / `PresetPage.cs` / `PresetPageController.cs` / `PresetConfirmPopup.cs`
- `Assets/_Project/Scripts/UI/Outgame/OutgameMenuController.cs` — presetPanel 라우팅
- `Assets/_Project/Data/Preset/SquadPresetCollection.asset` — 샘플 데이터
- 씬: `Assets/_Project/Scenes/OutgameScene.unity` (PresetPanel/PresetButton)

## Verified
- 컴파일 그린(콘솔 CS 에러 0; Burst JIT 캐시 DLL 경고는 환경 이슈, 본 feature 무관).
- EditMode 9/9 통과(`PresetApplyTests`).
- 씬 YAML: presetPanel/collection/profileSO/font refs 전부 non-zero, PresetButton.onClick→OnOpenPreset.
- Play e2e: 패널 렌더(스크린샷 확인) + 적용 체인(아이템 적용→확인팝업→프로필 교체→profile.json 저장, 센티넬 덮어써짐).
- 검증용 부수효과(프로필/스크린샷/일회용 배선 스크립트) 정리 완료.

## Notes
- 씬 배선은 execute_code 불가로 **일회용 에디터 MenuItem 스크립트**(Assets/Editor/PresetWiring.cs)로 수행 후 삭제. 재배선 필요 시 동일 패턴.
- `deckSize`/카드 수 "10"은 데이터 주도값(`DeckRuleConfig.deckSize`) — 하드 상수 아님. 프리셋 카드 수는 라이브 `EffectiveDeckSize` 에 맞춰 authoring.
- 적용은 덱 규칙 검증 없음(계약 4) — 무효 덱은 START 게이트가 잡음.
- 프리셋 버튼 아이콘은 플레이스홀더(파란 카드 + "프리셋" 라벨, 드캐 아이콘 숨김) — 전용 아이콘 교체는 후속.

## Follow-up
- README "후속 후보" 참조: 적용됨 하이라이트 · 런타임 덱 규칙 검증 · 드림스톤 포함 · 런타임 authoring(현재 로드아웃→새 프리셋 저장) · 아이템 상세보기 · 프리셋 버튼 전용 아이콘.
