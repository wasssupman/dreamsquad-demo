# Spine Runtime 4.2 Upgrade Spec

**작성일**: 2026-07-07
**상태**: unit 0~4 완료 (2026-07-07, unit 4 rev A = Layer Lab AMCasual Character 채택) — 에디터 시각 확인(rig 방향/URP/Play) 잔여, 파츠 조합 외형 시스템은 별도 spec 예정. 상세는 `4_rewire_new_resources.md` rev A 기록
**목표**: spine-unity 런타임을 3.8 → 4.2 로 업그레이드하고, 재활용 불가능한 3.8 스켈레톤 리소스를 전부 퇴역시킨 뒤, 4.2 신규 리소스 수급 규약과 재연결 경로를 마련한다.

## 배경 (2026-07-07 조사 결과)

- 런타임: `Assets/Spine` = `spine-unity-3.8-2021-11-10.unitypackage` (UPM 아님). `Assets/Spine Examples` 동반 임포트.
- 공식 4.2 패키지: `spine-unity-4.2-2026-05-29.unitypackage`, 지원 범위 **Unity 2017.1–6000.3**. 본 프로젝트는 **6000.4.3f1** — 명시 범위 밖이므로 스모크로 판정 (리스크 참조).
- 사용 중인 3.8 데이터: `player-main`(Characters, Defender 16종 전부 참조), `BellKnight`(BattleScene 씬 오브젝트 + Enemy_Tanker), `몬스터1`(Enemy_Vanguard).
- 미사용 잔재: `Assets/_Project/Spine/` 의 player-main 중복 세트, `Characters/` 의 미임포트 원본 8종(BellMage·DoubleWolf Long/Short·FleshSwarmer·ForestWormBoss·HeartWolf·MutantShroom3·WolfLamb — `.skel`/`.atlas` 확장자라 임포트조차 안 된 상태).
- 원본 `.spine` 소스 부재 → 재-export 불가 → **리소스 전면 신규 교체** (사용자 결정, 이 spec 의 전제).
- 코드는 고수준 API 만 사용 (`SkeletonAnimation`/`AnimationState`/`Skeleton`/skin/`FindAnimation`/`ScaleX`/`A`/`GetColor·SetColor`) + `SkeletonDataModifierAsset` 상속 1건(`SkeletonFlipXModifier`). 4.2 에서 대부분 그대로 컴파일될 것으로 예상, unit 1 에서 확정.
- `Wassup.Runtime.asmdef` 이 `spine-unity`, `spine-csharp` asmdef 를 참조 — 4.2 도 동일 이름이라 유지.
- spine-unity unitypackage 는 meta GUID 고정 → 런타임 교체 후에도 씬/에셋의 스크립트 참조(`SkeletonAnimation` 등)는 살아남는다. 끊기는 것은 SkeletonData **데이터** 에셋 참조뿐.

## 구현 문서 목록

| 작업 구분 | 문서 | 목적 |
|---|---|---|
| Unit 0 | `0_teardown_3_8_assets.md` | 3.8 스켈레톤 리소스 전량 퇴역 (런타임은 유지, 컴파일 그린) |
| Unit 1 | `1_swap_runtime_4_2.md` | 런타임 3.8 제거 + 4.2 임포트를 한 커밋으로 스왑, 컴파일/스모크 검증, lessons 갱신 |
| Unit 2 | `2_pipeline_smoke.md` | 4.2 예제 스켈레톤 임시 wiring 으로 SpineUnitView 파이프라인 Play 검증 (권장, 스킵 가능) |
| Unit 3 | `3_new_asset_conventions.md` | 4.2 신규 리소스 export/임포트 규약 문서화 |
| Unit 4 | `4_rewire_new_resources.md` | 신규 리소스 임포트 + 데이터 에셋/씬 재연결 + 임시 wiring 제거 |
| Unit 5 | `5_handoff_summary.md` | 종료 인계 (구현 종료 시 작성) |

## 공통 원칙

- 런타임 기준은 **spine-unity 4.2** (`spine-unity-4.2-2026-05-29.unitypackage` 이상). 신규 스켈레톤은 Spine Editor 4.2.xx export 만 허용.
- 3.8 데이터는 4.2 런타임에서 로드를 시도하지 않는다 (메이저 버전 간 포맷 비호환). 퇴역은 삭제로 처리하고, 복구는 git 히스토리에 맡긴다.
- 각 커밋 시점에 **컴파일 에러 0, 씬 로드 실패 0** 을 유지한다. 리소스 교체 완료 전까지 유닛 비주얼 공백(스켈레톤 미표시)은 허용한다.
- 파일명 규약: **ASCII 만** (macOS NFC/NFD 함정, lessons 03). 바이너리는 `.skel.bytes`, 아틀라스는 `.atlas.txt` 확장자.
- Presentation 계층(`SpineUnitView` 등)의 공개 계약은 바꾸지 않는다. 4.2 API 차이로 인한 내부 수정만 허용.
- `SkeletonFlipXModifier`(rig 좌우 정규화) 계약은 유지한다 — 신규 rig 이 "-x 바라봄 @ ScaleX=+1" 관례를 지키면 미사용, 어기면 SkeletonData 에 부착.

## 리스크

| 리스크 | 대응 |
|---|---|
| Unity 6000.4.3f1 이 공식 지원 상한(6000.3)보다 최신 | unit 1 컴파일 + Play 스모크로 판정. 실패 시 esotericsoftware 최신 패키지/포럼 확인. 탈출구: `git revert` 로 3.8 복귀 (lessons 03 에 복구 절차 검증됨) |
| `SkeletonDataModifierAsset`, `GetColor/SetColor` 확장이 4.2 에서 제거/변경됐을 가능성 | unit 1 컴파일에서 확정. 제거됐으면 `SkeletonFlipXModifier` 를 4.2 API 로 포팅 (계약 유지) |
| URP 17.4 + Spine 기본 셰이더 | 현재 `Spine/Skeleton` unlit 셰이더가 URP 에서 렌더되는 구조(SRPDefaultUnlit) 그대로 4.2 에도 적용. unit 2 스모크에서 확인. URP 전용 셰이더 패키지는 후속 후보 |
| 신규 리소스 수급 전까지 비주얼 공백 | unit 2 의 예제 스켈레톤 임시 wiring 으로 개발/검증 지속 가능 |

## 후속 후보

- `com.esotericsoftware.spine.urp-shaders` UPM 패키지 도입 (URP 네이티브 셰이더, 2D 라이팅 필요 시)
- 4.2 신규 물리 constraint 활용 (머리카락/천 등 2차 모션)
- UI 용 `SkeletonGraphic` 검토 (덱 빌더/드래프트 화면 캐릭터 노출)
