# Spec — Defender Deploy Cutscene

> 상태: 완료 (2026-07-14)

## 상위 목표

유닛을 드래그로 배치하는 스와이프 동안, 해당 유닛의 짧은 컷신(스프라이트 플립북)이
화면 **좌상단 모서리**에 등장한다. 컷신은 로비 캐릭터의 스프라이트 애니메이션 개념을
참고하되(프레임을 UI Image 에 순차 표시), 원샷 플립북이므로 Animator 없이 스크립트로
재생한다. 화면 왼쪽 **바깥에서 빠르게 슬라이드-인**하며 애니를 재생하고, 끝나고 **1초 후
왼쪽으로 슬라이드-아웃**하며 사라진다. 드래그가 먼저 끝나도(드롭/취소) 컷신은 독립적으로
재생을 완주한다.

## 검증 질문

Defender_Ranger 를 드래그로 집으면 좌상단에 Ranger 컷신 플립북이 뜨고, 33프레임 재생
후 1초 뒤 사라지는가? 드래그를 즉시 놓아도 컷신은 끝까지 재생되는가? 컷신 프레임이
없는 유닛은 아무 일도 일어나지 않는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_sprite_pipeline.md` | 프레임 전처리(역순 리넘버 + 누끼 + 50% 축소 + 임포트) | 33장 Ranger 스프라이트 확보 |
| 1 | `1_data_field.md` | DefenderUnitData 에 컷신 프레임/fps 필드 + Ranger 에셋 할당 | 유닛→프레임 매핑 |
| 2 | `2_cutscene_player.md` | `DeployCutscenePlayer` — 좌상단 오버레이 플립북 재생기 | 독립 재생 + 1초 후 소멸 |
| 3 | `3_wiring.md` | BeginDrag 트리거 + DefenderSelector 주입 + Play 검증 | 드래그 스와이프에 연결 |

## 공통 원칙 / Feature-wide 계약

- **트리거**: `DefenderDragPlacementController.BeginDrag` 진입 시 유닛에 컷신 프레임이
  있으면 1회 재생. 프레임 비어 있으면 no-op(다른 유닛은 조용히 skip).
- **수명 = 스와이프 연동** (rev 2026-07-15): 컷신은 등장 후 드래그하는 동안 계속 유지되고,
  스와이프(드래그)가 끝나면 소멸한다. `CleanupSession`(드롭/취소/비활성)이 `EndCutscene()` 로
  슬라이드-아웃을 트리거. (구현: `holdSecondsAfter`=사실상 무한 + hold 루프 `_endRequested` 탈출.)
  구 계약("독립 재생: 자체 코루틴 완주 → 1초 hold 후 자동 소멸")은 폐지.
- **렌더**: ScreenSpaceOverlay 캔버스의 UI `Image` 1장. 프레임을 `Image.sprite` 로 교체하는
  스크립트 플립북(fps = 데이터 값). Animator/.anim 미사용.
- **배치/연출**: 좌상단 앵커(anchor/pivot = top-left) + 인스펙터 마진. 표시 크기는 스프라이트
  네이티브(이미 50% 축소된 640×360) × displayScale. 등장은 화면 왼쪽 '바깥'에서 빠른
  슬라이드-인(애니 동시 재생), 퇴장은 왼쪽으로 슬라이드-아웃. 세로 위치는 좌상단 고정.
- **누끼 자산**: 원본 검정 불투명 배경 → 투명 매팅 + 50% 축소된 PNG 33장. 넘버링은
  원본 역순(원본 frame_033 → Ranger_001, frame_001 → Ranger_033).
- **하드코딩 금지**: fps·유닛별 표시배율(`deployCutsceneScale`)·유닛별 도착 오프셋
  (`deployCutsceneOffset`)은 `DefenderUnitData`, hold(1초)·displayScale·baseline 마진·
  슬라이드 속도는 `DeployCutscenePlayer` SerializeField. 값은 전부 데이터/인스펙터에서 나온다.
  - 최종 크기 = 네이티브 × displayScale(공유) × deployCutsceneScale(유닛별).
  - 도착 위치 = cornerMarginPx(공유 baseline, x=-100) + deployCutsceneOffset(유닛별).
    컷씬마다 캐릭터 위치/크기가 달라 유닛별 미세조정. (Ranger 0 → -100, Archer -150 → -250)
- **기능 온/오프**: `DragSwaySettings.enableDeployCutscene`(bool). 이 SO 는 이미 드래그 배치
  프리뷰 연출 튜닝 허브로 컨트롤러에 주입돼 있어 재사용. 끄면 프레임이 있어도 재생 안 함.
- **경계**: 순수 프레젠테이션(MonoBehaviour View). ECS/BattleBridge 시뮬레이션 경로를
  건드리지 않는다. `BattleBridge` 를 경유할 필요 없음(전투 상태 미참조).

## 파이프라인 커버리지

N/A — 전투 플레이 오브젝트(유닛/적/투사체/해저드)가 아닌 배치 UX 오버레이 연출.
스폰→렌더 파이프라인(`docs/reference/object-pipeline-map.md`) 대상이 아니다.
(cf. `outgame-lobby-characters` 도 같은 사유로 N/A.)

## 후속 후보 (이번 스코프 밖)

- Ranger·Archer 외 나머지 유닛 컷신 프레임 제작/할당. (Ranger 33장, Archer 49장 완료)
- 컷신 in/out 트랜지션(페이드·슬라이드), 프레임 아틀라스화로 메모리 최적화.
- 사운드(컷신 보이스/스팅어) 동기.
- 같은 유닛 연속 드래그 시 컷신 재생 정책(현재는 재생 중 재트리거 시 재시작).
