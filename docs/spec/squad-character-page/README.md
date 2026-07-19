# squad-character-page — 스쿼드 페이지를 캐릭터 열람+편성 화면으로 재설계

> 상태: **완료 2026-07-18 · rev 2026-07-19 units 9~10 완료** (units 0~10 구현·커밋·Play 확인. handoff `8_handoff_summary.md`. unit 9 = 슬롯 탭 제거→선택, unit 10 = 편성-먼저 정렬 + 헤더 선택 표시 — ebfa923a)
> 선행: `squad-loadout`(B, 완료 — SquadSave/편성 반입) · `dreamstone-loadout`(스톤 4 전역 슬롯) · `defender-portraits`(포트레이트) · `unit-stat-projection`(AttackOutputStats) · `spine-runtime-4-2-upgrade`(SkeletonGraphic 4.2)
> 성격: **아웃게임 UI/UX 재설계** (MonoBehaviour 프레젠테이션 전용). ECS/BattleBridge 무관 — 플레이 오브젝트 spec 아님 → 파이프라인 커버리지 섹션 N/A.

## 검증 질문

OutgameScene 스쿼드 화면에서, **선택 유닛의 라이브 Spine + 스탯 + 설명이 좌측 한 패널에 강조**되고, 같은 페이지 우측 목록에서 **셀을 탭해 손쉽게 다른 유닛으로 정보를 바꾸며**, 상세의 [출전]으로 **7슬롯 편성을 완성**한 뒤 게임을 시작하면 그 편성이 그대로 반입되는가? (전체화면 모달 피커 없이)

## 상위 목표

기존 "슬롯 한 줄 + 전체화면 모달 피커" 를 **명일방주/에픽세븐식 캐릭터 페이지**(상세 1/3 + 목록 2/3 split-view)로 대체한다. 열람과 편성을 한 화면에 통합하고, 모달을 폐기한다. SquadSave 데이터 모델·반입 규칙(squad-loadout)·스톤 전역 계약(dreamstone-loadout)은 **불변**.

## 레이아웃 (option A — 아트-백드롭)

```
┌─────────┬────────────────────────┐
│░░░░░░░░░│ ‹편성›▣▣▣▣▢▢▢  💎💎💎💎 │ ← 헤더 스트립
│░ SPINE ░├────────────────────────┤
│░ 풀바디 ░│  브라우저 2/3 (그리드)  │
│░░░░░░░░░│  ▢ ▢ ▢ ▢ ▢ ▢          │
├────────┤  ▢ ▢ ▢ ▢ ▢ ▢          │
│█이름 ★Epic│  ▢ ▢ ▢ ▢ ▢ ▢          │
│█ATK HP RNG│                        │
│█설명문 2줄│  (스톤 슬롯 탭 시       │
│█[출전 ⊕] │   스톤 모드로 전환)     │
└─────────┴────────────────────────┘
```

- **좌 1/3 상세**: 라이브 Spine 풀바디가 패널 배경 + 등급 글로우. 하단 반투명 통합 카드 = 이름·등급·클래스·코스트·스탯·설명문·[출전].
- **우 2/3**: 상단 헤더(편성 7 + 스톤 4) + 아래 브라우저 그리드(유닛 ~18 대부분 노출, 스크롤 최소).

## 작업 단위

| 파일 | 작업 | 문서 | 목적 |
|---|---|---|---|
| 0 | 로직 | `0_kit_summary.md` | 기믹 요약문 순수 생성기 `UnitKitSummary`(DefenderUnitData→문장) + EditMode 테스트 |
| 1 | UI | `1_unit_detail_view.md` | 상세 패널 뷰 — SkeletonGraphic 라이브 Spine + 통합 스탯/설명 카드 + [출전] |
| 2 | UI | `2_roster_browser.md` | 리스트 브라우저 그리드 — 셀(포트레이트+등급프레임+클래스) + 선택 하이라이트 + "편성중" 뱃지 |
| 3 | UI | `3_header_strip_stone_mode.md` | 헤더 편성 7 + 스톤 4 스트립 + 스톤 슬롯 탭 → 브라우저 스톤 모드 전환 |
| 4 | 통합 | `4_orchestrator.md` | `SquadCharacterPageController` — 출전 토글/편성해제/dedup/append, 스톤 모드, 저장 (모달 폐기) |
| 5 | 배선 | `5_wiring_play_e2e.md` | `SquadCharacterPage` 런타임 빌더 + 실제 squadPanel 배선 + Play e2e |
| 6 | 데이터 | `6_unit_desc_field.md` | `desc` SO 필드(시트-동기 plain) + `UnitKitSummary.Describe` 폴백 + 현재 요약문 시드 |
| 7 | 시트 | `7_desc_sheet_sync.md` | `DefenderStatDto.desc` + import/export 왕복(체력 등과 동형 리플렉션) + 테스트 |
| 8 | 인계 | `8_handoff_summary.md` | handoff |
| 9 | UX | `9_slot_tap_selects.md` | 헤더 찬 유닛 슬롯 탭 = 즉시 제거 → 선택(상세 보기)로 변경. 제거는 [편성 해제] 버튼으로 일원화 |
| 10 | UX | `10_roster_sort_and_slot_selected.md` | 컬렉션 그리드 편성-먼저 정렬(라이브) + 헤더 슬롯 선택 outline (드림캐쳐 unit 6과 쌍) |

순서: 0 → 1 → 2 → 3 → 4 → 5 → (6 → 7) → 8. 핵심 로직 유닛(0, 4) 종료 시 code-review, 나머지는 feature 종료 시 일괄. 6·7 은 "lore 문장 저작" 후속의 시트-동기 실현.

## Feature-wide 계약

- **모달 폐기**: 유닛·스톤 편성 모두 이 split-view 단일 면에서 처리. 기존 전체화면 `StonePickerPanel`(유닛/스톤 겸용) 제거.
- **상세 = 선택 대상**: 리스트 셀 탭·헤더 찬 유닛 슬롯 탭이 상세를 결정한다. 편성 상태와 **독립**(순수 열람). 편성 변경은 오직 [출전]/[편성해제] 버튼 (unit 9 개정 — 헤더 슬롯 탭은 더 이상 편성을 바꾸지 않음).
- **라이브 Spine**: `SkeletonGraphic`(UGUI)로 렌더, 전투와 동일 파츠/색(`SpineCombinedSkinCache` 재사용). `skeletonDataAsset` 없으면 `portrait` 폴백. idle 루프. 유닛 전환 시 리바인드.
- **설명문 자동 생성**: 신규 SO 필드/콘텐츠 저작 **0**. 클래스 + on-place 효과 + 방향공격/어그로/해저드 플래그 + 데미지 타입을 템플릿으로 조립하는 순수 함수. EditMode 테스트 대상. 진짜 lore 문장은 후속.
- **편성 규칙**: [출전]=첫 빈 슬롯 append, dedup(한 유닛 1슬롯, 기존 가드 유지), 제거=[편성해제] 버튼만(헤더 찬 슬롯 탭=선택, unit 9 개정). 슬롯 순서 = 추가 순서(드래그 재정렬 후속). `SquadSave` 계약·반입 규칙 불변.
- **스톤 전역**: 스톤 4는 스쿼드 전역 장비 — 유닛 리스트와 별개. 스톤 모드에서 `SquadSave.SetStoneSlot` 경유(dreamstone-loadout 계약 유지). 유닛별 장비 아님.
- **스탯 SoT**: 데미지는 `AttackOutputStats`(outputs 파생), 나머지는 SO 필드. 하드코딩 수치 0. 등급색은 기존 상수 재사용.
- **아키텍처**: 전부 MonoBehaviour 프레젠테이션(Outgame). ECS 맥락·BattleBridge 변경 없음. 아키텍처 중립 계산(요약문)만 static 순수 함수로 분리(제약 10).

## 후속 후보 (본 spec 범위 밖)

- **lore 스토리 문장 저작** — 유닛별 설명 필드 신설 + 콘텐츠 작성 (자동 요약문을 대체/보강)
- **스킬·드림캐쳐 상세 탭** — 부착 카드/스킬 성능 열람 (unit-dreamcatcher-inspect 문법 재사용 검토)
- **유닛별 장비** — 스톤을 유닛에 귀속시키는 E7식 기어 모델 (현재는 스쿼드 전역)
- **편성 드래그 재정렬 + 좌우 스와이프 prev/next 유닛 전환**
- **소유/잠금(가챠) 상태** — 현재 전 유닛 보유 전제. 미보유 셀 dim/잠금 UI
- **정렬·필터** — 클래스/등급/코스트별 리스트 정렬·필터 툴바
