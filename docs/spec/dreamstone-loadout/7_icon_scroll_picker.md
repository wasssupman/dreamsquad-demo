# 7. Dreamstone Icon Scroll Picker

## 목적

텍스트 전용 드림스톤 피커를 정식 아이콘 기반 UI로 바꾼다. 테스트 생성된 4종 드림스톤 보석 이미지를 스탯 타입별 대표 아이콘으로 승격하고, 64개 개별 스톤 SO가 자신의 효과 타입에 맞는 아이콘을 참조한다.

## 변경 대상

- `Assets/_Project/Art/Dreamstones/Icons/*.png`
- `Assets/_Project/Scripts/Data/Dreamstone/DreamstoneData.cs`
- `Assets/_Project/Data/Dreamstones/Stone_*.asset`
- `Assets/_Project/Scripts/UI/Outgame/SquadBuilderView.cs`
- `Assets/_Project/Tests/EditMode/DreamstoneCatalogTests.cs`

## 구현

- `DreamstoneData` 에 `Sprite icon` 필드를 추가한다.
- 공격력/체력/공격속도/코스트 생산속도 4종 아이콘 PNG를 Sprite import 설정으로 고정한다.
- 카탈로그의 64개 스톤은 `effect.kind` 기준으로 대표 아이콘을 공유한다.
- 스쿼드 페이지의 드림스톤 슬롯은 장착된 스톤의 아이콘을 우선 렌더하고, 텍스트는 요약 수치 보조 라벨로만 둔다.
- 드림스톤 피커는 64개 항목을 한 화면에 압축하지 않고 `ScrollRect` 기반 스크롤 리스트/그리드로 렌더한다.
- 장착된 개별 스톤은 기존처럼 딤드 + 선택 불가 상태를 유지한다.

## 완료 기준

- Unity compile clean.
- `DreamstoneCatalogTests` 가 모든 스톤의 `icon != null` 을 검증한다.
- 스쿼드 페이지에서 드림스톤 피커가 스크롤 가능하고 각 항목에 아이콘, 등급 배경, 수치 요약이 보인다.
- 드림스톤 슬롯에 장착된 스톤 아이콘이 표시되고 빈 슬롯은 기존 `+` 상태를 유지한다.

> 완료 확인 2026-07-06 — 커밋 `c51338c` 리뷰 APPROVE: ScrollRect 구성 표준(Viewport+Mask+ContentSizeFitter), RC2 밴드·last-sibling·딤드(interactable=false + CanvasGroup) 계약 전부 보존, validator icon!=null 포함. 리그 게이트 EditMode 27/27 + PlayMode green (유일 실패는 리뷰어 진단의 프로필 격리 결함 — 진단 수정으로 해소, 사용자 디스크 프로필 장착 상태에 진단이 오염되던 문제). 리뷰 M1 반영: 아이콘 4종 maxTextureSize 2048→256 (소스 1254², 표시 66~78px — 모바일 메모리/빌드).
