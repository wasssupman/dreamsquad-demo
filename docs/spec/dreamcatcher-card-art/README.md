# dreamcatcher-card-art

> 상태: 완료 2026-07-08 (units 0~3, Play 검증 완료)
> 선행: `dreamcatcher-deck-builder`(완료, 페이지·덱규칙·카탈로그), `ingame-dreamcatcher`(완료, 카드 데이터).
> handoff → `4_handoff_summary.md`.

## 검증 질문

OutgameScene 드림캐쳐 페이지에서, 각 드림캐쳐 카드가 **배정된 타로풍 아트 이미지 + 효과 텍스트를 세로(Column)로** 표시하고, 보유 컬렉션과 덱 슬롯이 **라인당 5개 카드 그리드(보유는 세로 스크롤)** 로 보이는가? 카드는 10종이며 각 카드의 아트는 `DreamcatcherCard` SO 필드로 배정되는가? 새 드림캐쳐를 추가할 때 SO+아트+카탈로그 등록만으로 페이지에 자동 렌더되는가?

## 상위 목표

기존 "플랫 색상 버튼 + 텍스트" 페이지를 **아트 카드 그리드**로 승격한다. 스코프:

- `DreamcatcherCard` SO 에 `Sprite art` 필드 신설.
- 카드 풀 6 → 10 종 확장 (신규 4종은 기존 효과 채널 재사용, 신규 메커닉 없음).
- 루트의 테스트 이미지 10장(`dreamcatcher-card-test-01~10.png`)을 `Assets/_Project/Art/DreamcatcherCards/` 로 Sprite 임포트 승격.
- 10 카드에 아트 순서대로 자동 배정(인스펙터 재조정 전제).
- `DreamcatcherDeckBuilderView` 를 아트 카드(이미지+효과텍스트 Column) 그리드로 리디자인. 보유=5열 세로 스크롤, 덱 슬롯=동일 카드 세로.

스코프 밖(후속): 카드 콘텐츠 확장(신규 메커닉/무의식), 보유/언락/가챠, 다중 덱, 인게임 3중1 모달(SelectionView) 아트화.

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 데이터 | `0_card_art_field_and_pool.md` | `DreamcatcherCard.art`(Sprite) 추가 + 신규 카드 4종 asset + 카탈로그 10종 등록 |
| 1 | 에셋 | `1_promote_card_art_assets.md` | PNG 10장 → Art 폴더 Sprite 승격(meta) + 10 카드에 art 순서 배정 |
| 2 | UI | `2_deck_builder_card_ui.md` | 뷰 리디자인: 카드 아이템(아트+효과텍스트 Column) + 보유 5열 스크롤 + 덱 5열 카드 |
| 3 | 배선/검증 | `3_scene_wiring_and_verify.md` | 씬 참조 확인 + Play 검증(에디터) |
| 4 | 인계 | `4_handoff_summary.md` | 세션 인계 요약 |
| 5 | 레이어/스타일 | `5_devlayer_and_confirm_polish.md` | 개발버튼 로비 레이어 전용화 + 확정영역(MY DECK) 프레임 스타일 + 상·하단 동일폭 정렬 |
| 6 | 카드 팝업 | `6_card_detail_popup.md` | 카드=이미지 전용, 탭 시 효과 모달 팝업(액션+X+바깥클릭 닫힘) |

## Feature-wide 계약

- **아트 배정 = SO 필드**: `DreamcatcherCard.art` (`Sprite`, nullable). 뷰는 art 있으면 이미지, 없으면 색상 폴백.
- **카드 풀 10종**: 기존 6 + 신규 4(Normal, 기존 `CardBuffKind`/`CardTargetAxis` 채널만). `DreamcatcherCardCatalog` 가 단일 목록.
- **덱 규칙 불변**: `DeckRules`(정확히 10·고유≤2) 그대로. 카드 풀만 커짐.
- **효과 텍스트**: axis(RANGER/GUARDIAN/COST-1/ALL) + 버프 라인(ATK/AS/HP/MOVE/COST ±%). `DreamcatcherSelectionView.Summary` 표기 규약과 정합.
- **레이아웃은 코드 주도**: 뷰가 컨테이너 rect/스크롤/그리드를 런타임 구성(씬 YAML 대수술 회피). 직렬화 참조(`ownedContainer`/`deckContainer`)는 앵커로만 사용.
- **확장 규약**: 새 드림캐쳐 = 새 `DreamcatcherCard` SO 생성 → `art` 배정 → 카탈로그 배열에 추가. 코드 변경 불필요, 페이지 자동 렌더.
- **라벨 영문 유지**(한글 폰트 후속).

## 파이프라인 커버리지

`docs/reference/object-pipeline-map.md` 대상 아키타입(유닛/적/투사체/해저드/VFX/프랍/타일)에 **해당 없음**. 드림캐쳐 카드는 전투 씬 플레이 오브젝트가 아니라 **Outgame UI/데이터 에셋**이다. 생성→렌더 경로는 `ScriptableObject(DreamcatcherCard) → DreamcatcherCardCatalog → DreamcatcherDeckBuilderView(uGUI Image/TMP)` 로, 파이프라인 맵의 정거장(Authoring/ECS/Spine/Particle)과 무관. → 전 정거장 **N/A (Outgame UI 에셋, ECS·전투 렌더 경로 미사용)**.

## 후속 후보 (범위 밖)

- 카드 콘텐츠 확장: 신규 메커닉 채널(row-only/crit/pierce/splash 등) + 무의식 2장.
- 보유/언락(ownedCardIds) + 가챠/꿈런 파밍.
- 인게임 3중1 모달(`DreamcatcherSelectionView`) 도 아트 카드화(현재는 텍스트).
- 아트 이미지 압축/아틀라스(모바일 메모리) 최적화.
- 카드 등급/희귀도별 프레임·글로우.
