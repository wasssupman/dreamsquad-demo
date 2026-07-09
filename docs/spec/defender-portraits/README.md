# defender-portraits

> 상태: 완료 2026-07-08 (커밋 c423be4c, 95e1099b)

## 목표

클래스별로 준비된 방어 유닛 포트레이트를 `DefenderUnitData` 에 데이터로 연결하고,
**스쿼드 페이지**(OutgameScene / `SquadBuilderView`)와 **인게임 유닛 선택 UI**
(BattleScene / `DefenderSelector`)에서 텍스트/단색 대신 포트레이트로 유닛을 보여준다.

검증 질문: *"스쿼드 편성 화면과 전투 배치 스트립에서, 각 유닛이 이름표가 아니라
클래스에 맞는 포트레이트 이미지로 식별되는가?"*

## 배경

- 포트레이트 원본: `Assets/_Project/Art/DefenderPortraits/{bishoujo,modern}/`
  — 두 아트 스타일로 클래스별 1장씩(각 16개 + 컨택트 시트).
- 현재 import 설정은 일반 Texture (`textureType: 0`, `spriteMode: 0`) — UI Image 에
  쓰려면 Sprite 로 재import 필요.
- `DefenderUnitData` 에는 아직 포트레이트/아이콘 필드가 없다. `role`(DefenderClass)은
  버프 타겟팅용 6-값 enum 이라 16 포트레이트와 1:1 이 아니다 → 매핑 키는 유닛 **id**.
- 스타일은 **클래스별 혼합**(사용자 결정 2026-07-08). 아래 "포트레이트 배정표" 참조.

## 작업 단위

| # | 문서 | 목적 |
|---|---|---|
| 0 | `0_portrait-field-and-import.md` | `DefenderUnitData.portrait` 필드 추가 + 포트레이트 텍스처 Sprite 재import |
| 1 | `1_assign-portraits-to-defenders.md` | 16개 방어 SO 에 배정표대로 포트레이트 스프라이트 할당 |
| 2 | `2_squad-ui-portrait.md` | `SquadBuilderView` 유닛 슬롯 + 유닛 피커에 포트레이트 표시 |
| 3 | `3_ingame-selector-portrait.md` | `DefenderSelector` 배치 스트립 슬롯에 포트레이트 표시 |
| 4 | `4_portrait-sizing-and-ui-tweaks.md` | 포트레이트 크기 확대 + 드림캐쳐 타이틀 가림 수정(육안 피드백) |
| 6 | `6_remove-selection-and-clickplacement.md` | 인게임 선택 로직/프레임 제거 + 클릭 배치 비활성(드래그-드롭 전용) |

## feature-wide 계약

- 매핑 키는 유닛 **id** (asset 이름/displayName 아님). id 는 저장/로드 영속 키.
- `DefenderUnitData.portrait` 는 `Sprite` 이며 **nullable**. null 이면 기존 텍스트/단색
  폴백을 그대로 유지한다 (포트레이트 미할당 유닛도 UI 가 깨지지 않는다).
- UI 는 포트레이트를 `preserveAspect = true` 로 표시한다.
- 선택/필터 상태(선택 하이라이트, 이미-편성 dim)는 포트레이트 도입 후에도 유지된다.
- 포트레이트 필드는 순수 프레젠테이션 데이터 — ECS 런타임/전투 로직은 참조하지 않는다.

## 포트레이트 배정표 (클래스별 혼합, 초안 — 리뷰 시 개별 조정 가능)

| id | style | 원본 파일 |
|---|---|---|
| archer | bishoujo | `bishoujo/defender_portrait_archer_test_01.png` |
| ranger | bishoujo | `bishoujo/defender_portrait_ranger_test_01.png` |
| piercer | bishoujo | `bishoujo/defender_portrait_piercer_test_01.png` |
| bastion | bishoujo | `bishoujo/defender_portrait_bastion_test_01.png` |
| guardian | bishoujo | `bishoujo/defender_portrait_guardian_test_01.png` |
| healer | bishoujo | `bishoujo/defender_portrait_healer_test_01.png` |
| fire_caster | bishoujo | `bishoujo/defender_portrait_fire_caster_test_01.png` |
| ice_caster | bishoujo | `bishoujo/defender_portrait_ice_caster_test_01.png` |
| poison_caster | bishoujo | `bishoujo/defender_portrait_poison_caster_test_01.png` |
| blocking_caster | bishoujo | `bishoujo/defender_portrait_blocking_caster_test_01.png` |
| scout | modern | `modern/defender_portrait_scout_modern_test_01.png` |
| sniper | modern | `modern/defender_portrait_sniper_modern_test_01.png` |
| marksman | modern | `modern/defender_portrait_marksman_modern_test_01.png` |
| artillery | modern | `modern/defender_portrait_artillery_modern_test_01.png` |
| cannon | modern | `modern/defender_portrait_cannon_modern_test_01.png` |
| bruiser | modern | `modern/defender_portrait_fighter_modern_test_01.png` |

배정 원칙: 총기·정밀사격·정찰·중화기 계열(scout/sniper/marksman/artillery/cannon/bruiser)은
전술 느낌의 **modern**, 활·마법·방패·서포트 계열은 판타지 느낌의 **bishoujo**.
`bruiser` 의 포트레이트 파일명은 `fighter`(displayName=Fighter) 로 존재한다.

## 파이프라인 커버리지

**N/A** — 본 spec 은 플레이 오브젝트(유닛/적/투사체/해저드/VFX)의 생성→렌더 경로를
신설·변경하지 않는다. UI 프레젠테이션 + SO 데이터 필드만 추가한다.
(`docs/reference/object-pipeline-map.md` 대조 불필요.)

## 후속 후보 (현 spec 범위 밖)

- 포트레이트 원본의 `_test_01` 접미사 정리 / 최종 네이밍 확정.
- 루트 `Art/DefenderPortraits/defender_portrait_ranger_test_01.png` 중복본 정리.
- 미사용 스타일(현재 배정에서 빠진 16장)의 활용(스킨 토글 등).
- `AttackUnitData`(적 유닛)에 동일한 포트레이트 도입.
- ResultScreen/리더보드 등 다른 화면으로의 포트레이트 확장.
