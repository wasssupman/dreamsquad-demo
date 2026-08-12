# unit 12 — 고유 리그의 아웃게임 호환

## 목적

unit 8 이 «모든 유닛이 `Casual Character` 리그 하나를 공유한다»는 전제를 깼는데, 그 전제에 기대던 코드가 **전투 밖에도** 있었다. unit 8 의 완료 기준은 전투 밖 경로를 셋(드래그 프리뷰·스쿼드 상세·항아리 피규어)만 셌고 **로딩 화면을 빠뜨렸다** — 그래서 실제로 크래시가 났다.

## 증상과 원인

**① 게임 시작이 간헐적으로 실패** (사용자 제보 2026-08-12)

```
ArgumentException: Skin not found: full_skins
  Spine.Skeleton.SetSkin → SkeletonGraphic.AssignInitialSkin → Initialize
  → SceneTransition.ApplyRunnerSkin → ConfigureRunners → Run
```

로딩 화면 러너는 스쿼드에서 3명을 뽑아 스켈레톤을 갈아끼우는데, `Initialize(true)` 안의 `AssignInitialSkin` 이 **씬에 저작된 `initialSkinName`(= `full_skins`)** 을 새 스켈레톤에 적용한다. CH1 은 스킨이 `default` 하나뿐이라 예외가 나고, 그 예외가 `Run()` 코루틴을 타고 올라가 씬 전환이 통째로 죽는다.

**간헐적이었던 이유**: `BuildRunnerUnits` 가 스쿼드를 Fisher–Yates 로 섞는다 — **소환사가 러너 슬롯에 뽑힐 때만** 터진다.

**② 같은 전제가 애니에도 있었다.** `loadingAnimation = "Run"` 인데 CH1 엔 없고, `AnimationState.SetAnimation(string)` 은 없는 애니에 **예외를 던진다**(`AnimationState.cs:663`). ①만 고치면 크래시가 한 줄 아래로 옮겨간다.

**③ 아웃게임에서 소환사만 2.69배로 크다.** 아웃게임 UI 세 경로가 `SpineVisualScale` 을 **읽지 않는다**(grep 0건). 크기는 씬에 저작된 RectTransform 스케일 하나로 정해지고, 모든 유닛이 같은 리그였을 땐 그게 곧 "다 같은 크기"였다. CH1 원본 높이 505.67 vs Casual Character 187.92.

## 계약

1. **초기 스킨은 그 유닛 데이터에서 나온다.** `string.IsNullOrEmpty(SpineSkinName) ? "default" : SpineSkinName` — `SpineUnitView.Spawn` 과 **같은 관용구**다. `"default"` 는 spine 의 `AssignInitialSkin` 이 건너뛰므로 스킨이 하나뿐인 리그에서도 안전하다. **스켈레톤을 바꾸기 전에** 세팅해야 한다.

2. **재생할 애니는 실존을 확인하고 고른다.** `ResolveRunnerAnimation` = 저작된 이름 → 유닛 walk → 유닛 idle → `idle`/`Idle` 순. 하나도 없으면 `null` 이고 호출측은 애니를 걸지 않는다(setup pose 유지). 달릴 줄 모르는 리그는 **서 있는 채로** 나온다 — 로딩 화면이 죽는 것보단 낫다.

3. **크기 보정은 임시다.** `DefenderUnitData.outgameScaleMul`(기본 1). 근본 해법은 «리그 원본 높이로 정규화» 이거나 에셋을 같은 기준으로 뽑는 것이고, **리그가 비정규본이라 유닛당 값 한 칸으로 땜빵**했다(사용자 결정 2026-08-12). 에셋이 정규화되면 이 필드를 지운다. 소환사 = `0.372`(= 187.92 / 505.67).
   - 저작 스케일은 **1회 캡처**해서 곱한다. 매번 곱하면 유닛을 넘길 때마다 누적된다.

4. **스쿼드 상세도 같은 지뢰를 갖고 있었다.** 지금은 그 씬의 `initialSkinName` 이 비어 있어 안 터질 뿐이라, 손대는 김에 계약 1 을 같이 적용했다(`SkeletonRenderer.Common.cs:412-415` 가 같은 코드 경로다).
   - ⚠ 4.3 의 분리된 컴포넌트 구조에서 **초기 스킨은 `SkeletonGraphic` 이 소유**한다. `SkeletonAnimation` 엔 그 필드가 없다.

## 변경 대상

- `Assets/_Project/Scripts/Core/SceneTransition.cs` — 계약 1·2·3
- `Assets/_Project/Scripts/UI/Outgame/SquadUnitDetailView.cs` — 계약 1·3
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `outgameScaleMul`(임시)
- `Assets/_Project/Data/Defenders/Defender_Summoner.asset` — `outgameScaleMul: 0.372`
- 신규 `Assets/_Project/Tests/EditMode/LoadingRunnerRigTests.cs`

## 완료 기준

- [x] compile 에러 0 · EditMode 2190 중 2187 통과 · 실패 0
- [x] 회귀 방지: `LoadingRunnerRigTests` 가 **카탈로그 전 유닛**을 실제 SkeletonDataAsset 으로 검사한다 — ① 러너 애니가 리졸브되는지 ② 저작 스킨이 자기 스켈레톤에 실재하는지. **고유 리그가 하나 더 들어와도 여기서 걸린다**
- [x] Play 육안 확인 (사용자, 2026-08-12)
- [ ] 아웃게임 크기 최종 조정 — `0.372` 는 높이 정합 계산값이라 시작점이다. 리그마다 발밑 여백이 달라 눈으로 다듬을 여지가 있다

---

**완료 기준 확인**: 2026-08-12 · 커밋 `5c73c128` · 크래시 2건 수정 + `LoadingRunnerRigTests` 회귀 고정 + 사용자 Play 확인.
**잔여 1건**: 아웃게임 크기 최종 조정(`outgameScaleMul` = 0.372, 임시) — `docs/spec/README.md` Follow-up Backlog 로 이관.

## 후속

- **아웃게임 크기 정규화** — 에셋이 정규본이 되면 `outgameScaleMul` 을 1 로 되돌리고 필드째 제거한다. 그때까지 이 필드는 «리그가 비정규본이다»의 표식이다.
- **항아리 피규어(`SpineFigureBuilder`)** 는 이번에 손대지 않았다. `sizeDelta` 400×600 고정이라 같은 크기 문제가 있으나, 소환사가 그 경로에 실제로 나타나는지 확인되지 않았다(제약 9 — 확인 전엔 건드리지 않는다).
