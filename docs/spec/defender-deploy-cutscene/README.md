# Spec — Defender Deploy Cutscene

> 상태: **완료** (2026-07-14) — 재생기·트리거·배선 전부 종료.
> 확장 이력: Guardian 프레임 (2026-07-15, `5cbee1b4`) · 뎁스 패럴랙스 통합 (2026-07-15, `de2275ee`).
> **진행 중**: unit 5 Cannon 프레임+뎁스 (2026-07-16) — 자산·할당 완료, **Play 검증 대기**.
>
> 인계 지도: `4_handoff_summary.md`(Ranger/Archer 시점). Cannon 시점 handoff 는 Play 검증·커밋
> 후 `6_handoff_summary.md` 로 작성 예정(아직 없음).

## 상위 목표

유닛을 드래그로 배치하는 스와이프 동안, 해당 유닛의 짧은 컷신(스프라이트 플립북)이
화면 **좌하단 모서리**에 등장한다. 컷신은 로비 캐릭터의 스프라이트 애니메이션 개념을
참고하되(프레임을 UI Image 에 순차 표시), 원샷 플립북이므로 Animator 없이 스크립트로
재생한다. 화면 왼쪽 **바깥에서 빠르게 슬라이드-인**하며 애니를 재생하고, **스와이프가
끝나면** 왼쪽으로 슬라이드-아웃하며 사라진다.

> 이 문단은 rev 2026-07-15(수명=스와이프 연동)·rev 2026-07-16(앵커=좌하단) 반영본이다.
> 초기 계약은 "좌상단 + 애니 완주 후 1초 hold 후 자동 소멸 + 드래그와 독립"이었고 **폐지**됐다.
> 상세는 아래 공통 원칙의 `수명`·`배치/연출` bullet 참조.

## 검증 질문

Defender_Ranger 를 드래그로 집으면 좌하단에 Ranger 컷신 플립북이 뜨고, 드래그하는 동안
유지되다가 드롭/취소하면 왼쪽으로 슬라이드-아웃하며 사라지는가? 스와이프 방향으로
컷신 아트가 3D 회전하듯 기울고 손을 떼면 0 으로 복귀하는가(뎁스 패럴랙스)? 컷신 프레임이
없는 유닛은 아무 일도 일어나지 않는가?

## 작업 단위

| # | 파일 | 작업 | 목적 |
|---|---|---|---|
| 0 | `0_sprite_pipeline.md` | Ranger 프레임 전처리(역순 리넘버 + 검정 누끼 + 50% 축소 + 임포트) | 33장 Ranger 스프라이트 확보 |
| 1 | `1_data_field.md` | DefenderUnitData 에 컷신 프레임/fps 필드 + Ranger/Archer 에셋 할당 | 유닛→프레임 매핑 |
| 2 | `2_cutscene_player.md` | `DeployCutscenePlayer` — 좌하단 오버레이 플립북 재생기 | 슬라이드 인/아웃 + 스와이프 연동 소멸 |
| 3 | `3_wiring.md` | BeginDrag 트리거 + DefenderSelector 주입 + Play 검증 | 드래그 스와이프에 연결 |
| 5 | `5_cannon_frames.md` | Cannon 49프레임(체커보드 누끼 · **정방향**) + 정적 뎁스 | 4번째 컷신 유닛 |

> 4·6 번은 handoff summary(작업 단위 아님). Guardian(49장)은 `5cbee1b4` 로 스펙 파일 없이
> 프레임만 추가됐다 — 소스 성질이 이 스펙에 미기록이다(후속 후보 참조).
>
> **unit 0 은 Ranger 전용 절차다.** 소스마다 배경색·재생 방향이 다르므로 새 유닛을 추가할 때
> 그대로 복사하지 말 것. 유닛별 차이는 각 작업 단위 파일이 소유한다(Cannon → unit 5).

## 공통 원칙 / Feature-wide 계약

- **트리거**: `DefenderDragPlacementController.BeginDrag` 진입 시 유닛에 컷신 프레임이
  있으면 1회 재생. 프레임 비어 있으면 no-op(다른 유닛은 조용히 skip).
- **수명 = 스와이프 연동** (rev 2026-07-15): 컷신은 등장 후 드래그하는 동안 계속 유지되고,
  스와이프(드래그)가 끝나면 소멸한다. `CleanupSession`(드롭/취소/비활성)이 `EndCutscene()` 로
  슬라이드-아웃을 트리거. (구현: `holdSecondsAfter`=사실상 무한 + hold 루프 `_endRequested` 탈출.)
  구 계약("독립 재생: 자체 코루틴 완주 → 1초 hold 후 자동 소멸")은 폐지.
- **렌더**: ScreenSpaceOverlay 캔버스의 UI `Image` 1장. 프레임을 `Image.sprite` 로 교체하는
  스크립트 플립북(fps = 데이터 값). Animator/.anim 미사용.
- **배치/연출** (rev 2026-07-16: 앵커 정정): 앵커/피벗 = **좌하단** `(0,0)` + 인스펙터 마진.
  `cornerMarginPx` = (x: 이미지 왼쪽끝 위치, y: 하단에서 위로), 현 baseline `(-100, 24)`.
  초기 계약은 좌상단이었으나 튜닝으로 좌하단이 됐다 — **이 bullet 이 계약이고**, unit 2 의
  top-left 기술은 폐지본이다. 표시 크기는 스프라이트
  네이티브(**유닛마다 다름**: Ranger/Archer 640×360, Guardian 180×180, Cannon 276×204)
  × displayScale(공유, 현 1.2). 등장은 화면 왼쪽 '바깥'에서 빠른
  슬라이드-인(애니 동시 재생), 퇴장은 왼쪽으로 슬라이드-아웃. 세로 위치는 좌하단 고정.
- **누끼 자산 (유닛-불변 계약만)**: 각 유닛 프레임 = **배경 투명 PNG** · **재생 순서 = 줌-인**
  (`{Unit}_001` = 가장 줌-아웃) · **네이티브 해상도 유지**(크롭/축소 없음).
  **배경 제거 기법과 소스 넘버링 방향은 계약이 아니다** — 소스마다 다르며 각 작업 단위
  파일이 소유한다. 여기 있는 값을 새 유닛에 복사하지 말고 소스를 먼저 실측할 것.
  - Ranger/Archer: 검정 배경 → luma 매트 + **역순** 리넘버(소스가 줌-아웃) → `0_sprite_pipeline.md`
  - Cannon: 체커보드가 RGB 에 베이크된 **가짜 투명** → flood-fill + **정방향** → `5_cannon_frames.md`
  - Guardian: 정방향(`depth-parallax` unit 8 교차기록). 스펙 파일 없음 → 후속 후보 참조.
- **하드코딩 금지**: 유닛별 값 **6종**은 `DefenderUnitData` — `deployCutsceneFrames` ·
  `deployCutsceneFps` · `deployCutsceneScale`(표시배율) · `deployCutsceneOffset`(도착 오프셋) ·
  `deployCutsceneDepth`(뎁스) · `deployCutsceneTiltGain`(틸트 배율).
  공유 값(hold·displayScale·baseline 마진·슬라이드 속도)은 `DeployCutscenePlayer` SerializeField.
  값은 전부 데이터/인스펙터에서 나온다.
  - 최종 크기 = 네이티브 × displayScale(공유, 현 1.2) × deployCutsceneScale(유닛별).
  - 도착 위치 = cornerMarginPx(공유 baseline, x=-100) + deployCutsceneOffset(유닛별).
    컷씬마다 캐릭터 위치/크기가 달라 유닛별 미세조정. **scale/offset 은 Play 튜닝으로 수렴시킨다.**
    (Ranger scale 1 / offset 0 → -100 · Archer 1.5 / -150 → -250 · Guardian 3 / +100 → 0 ·
    Cannon 2.6 / 0 → -100, **Play 미검증 시작값**)
- **기능 온/오프**: `DragSwaySettings.enableDeployCutscene`(bool). 이 SO 는 이미 드래그 배치
  프리뷰 연출 튜닝 허브로 컨트롤러에 주입돼 있어 재사용. 끄면 프레임이 있어도 재생 안 함.
- **뎁스 패럴랙스**: 별도 스펙 소유(`docs/spec/depth-parallax/`, 완료 2026-07-15, `de2275ee`).
  이 스펙은 **유닛별 자산 할당만** 담당(`deployCutsceneDepth` / `deployCutsceneTiltGain`).
  극성은 유닛별이 아니라 `DepthParallaxSettings.depthSign` 전역 노브다.
- **경계**: 순수 프레젠테이션(MonoBehaviour View). ECS/BattleBridge 시뮬레이션 경로를
  건드리지 않는다. `BattleBridge` 를 경유할 필요 없음(전투 상태 미참조).

## 파이프라인 커버리지

N/A — 전투 플레이 오브젝트(유닛/적/투사체/해저드)가 아닌 배치 UX 오버레이 연출.
스폰→렌더 파이프라인(`docs/reference/object-pipeline-map.md`) 대상이 아니다.
(cf. `outgame-lobby-characters` 도 같은 사유로 N/A.)

## 후속 후보 (이번 스코프 밖)

- 나머지 유닛 컷신 프레임 제작/할당. **완료: Ranger 33장 · Archer 49장 · Guardian 49장
  (`5cbee1b4`) · Cannon 49장 (`5_cannon_frames.md`)** — 남은 12 디펜더는 미착수.
- **Guardian 스펙 파일 부재** (`5cbee1b4` — 프레임만 커밋): 소스 성질이 이 스펙에 한 줄도
  없다(180×180 · **정방향** — `depth-parallax` unit 8 교차기록 · 배경 종류/누끼 기법 미기록).
  Cannon 으로 겪은 함정의 선례일 수 있는데 기록이 없다. 5번째 유닛 추가 시 선례는 unit 0/5
  만 삼을 것. 사후 복원은 추측이 섞이므로, 필요해지면 실제 자산을 실측해 unit 번호로 신설.
- Cannon 고해상도 재수급: 현 소스는 276×204(화면 3.1배 확대). 파일명이 광고하는 원본
  1932×1428 을 확보하면 재-누끼(`5_cannon_frames.md` 절차 그대로)로 Archer 급 선명도 가능.
- **뎁스 계단 리맵**: 근경 과장 + 몸통 4단 계단으로 패럴랙스를 이산 평면화 → 별도 spec
  `docs/spec/cutscene-depth-layering/`(초안, 2026-07-16). 4종 전부 대상이라 이 spec 범위 밖.
- 컷신 in/out 트랜지션(페이드·슬라이드), 프레임 아틀라스화로 메모리 최적화.
- 사운드(컷신 보이스/스팅어) 동기.
- 같은 유닛 연속 드래그 시 컷신 재생 정책(현재는 재생 중 재트리거 시 재시작).
