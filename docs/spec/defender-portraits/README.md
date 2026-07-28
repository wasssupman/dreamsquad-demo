# defender-portraits

> 상태: 완료 2026-07-08 (커밋 c423be4c, 95e1099b)
>
> 후속 대체: `docs/spec/defender-spine-portraits/` (2026-07-28).
> 현재 아트 source는 `Assets/_Project/Art/DefenderPortraits/spine/`이며,
> `skeletonDataAsset + partSkins + slotColors`에서 다시 베이크할 수 있다.

## 목표

클래스별로 준비된 방어 유닛 포트레이트를 `DefenderUnitData` 에 데이터로 연결하고,
**스쿼드 페이지**(OutgameScene / `SquadBuilderView`)와 **인게임 유닛 선택 UI**
(BattleScene / `DefenderSelector`)에서 텍스트/단색 대신 포트레이트로 유닛을 보여준다.

검증 질문: *"스쿼드 편성 화면과 전투 배치 스트립에서, 각 유닛이 이름표가 아니라
클래스에 맞는 포트레이트 이미지로 식별되는가?"*

## 배경

- 이 spec이 `DefenderUnitData.portrait`와 공통 UI 소비 경로를 처음 도입했다.
- 최초 AI 포트레이트 배정은 후속 `defender-spine-portraits`에서 현재 Spine 외형 기반
  512×512 투명 Sprite로 전수 교체됐다.
- `role`은 포트레이트 매핑 키가 아니다. 현재도 유닛 영속 키인 **id**를 사용한다.

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

## 현재 배정 계약

- 유효 `DefenderCatalog.units` 전원이
  `spine/defender_portrait_{id}.png`의 고유 Sprite를 참조한다.
- 파생 PNG의 source of truth와 베이크/프레이밍 계약은
  `docs/spec/defender-spine-portraits/README.md`가 소유한다.
- 최초 AI 배정표와 구현 이력은 이 폴더의 번호 문서와 git 이력에 보존한다.

## 파이프라인 커버리지

**N/A** — 본 spec 은 플레이 오브젝트(유닛/적/투사체/해저드/VFX)의 생성→렌더 경로를
신설·변경하지 않는다. UI 프레젠테이션 + SO 데이터 필드만 추가한다.
(`docs/reference/object-pipeline-map.md` 대조 불필요.)

## 후속 후보 (현 spec 범위 밖)

- `AttackUnitData`(적 유닛)에 동일한 포트레이트 도입.
- ResultScreen/리더보드 등 다른 화면으로의 포트레이트 확장.
