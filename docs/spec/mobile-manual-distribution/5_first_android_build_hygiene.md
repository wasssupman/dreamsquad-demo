# 5 — First Android Build Hygiene

## 목적

첫 Android 실빌드에서 드러난 Unity 6.4 orientation 직렬화 호환과 TMP 동적 폰트의
프리베이크 보존 문제를 해결해 clean commit 빌드 계약을 회복한다.

## 변경 대상

- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs`
- `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`
- `Assets/_Project/Fonts/{Anton,Bangers,Jua,Kanit} SDF.asset`
- `docs/spec/mobile-manual-distribution/README.md`

## 구현

- PlayerSettings는 수정하지 않는다. `defaultScreenOrientation: 5`는 Unity 6.4
  `PlayerSettings.defaultInterfaceOrientation`에서 `ScreenOrientation.AutoRotation`의 raw
  값으로 보일 수 있으므로, `UIOrientation.AutoRotation`과 해당 직렬화 값을 모두 인정한다.
- 자동회전 값만으로 통과시키지 않는다. Portrait/PortraitUpsideDown은 모두 금지하고
  LandscapeLeft/LandscapeRight는 모두 허용해야 한다.
- 회귀 테스트는 plain orientation 값 검증과 실제 tracked PlayerSettings capture를 분리해
  legacy 값과 가로 전용 플래그를 고정한다.

> **2026-07-27 갱신** (`c82d34b7`): 위 두 번째·세 번째 항목이 자동회전 전용이라
> `19ff8e8f`(5 → 2 `LandscapeRight` 고정, 자동회전 폐기) 이후 preflight 가 빌드를
> 막았다. `IsLandscapeAutoRotation` → `IsLandscapeOnly` 로 넓혀 **가로 고정도 통과**
> 시킨다(세로가 구조적으로 불가능해 자동회전보다 엄격). capture 테스트는 특정 값
> 대신 "가로 전용 통과" 만 고정한다 — 값을 못박으면 제품 결정이 바뀔 때마다 빌드가
> 멈춘다. 세로 허용 플래그 거부는 그대로다. 상세는 README 계약 참조.
- 5개 TMP 동적 폰트는 과거 커밋의 한글/UI glyph 프리베이크를 그대로 보존한다. 각 폰트의
  `Clear Dynamic Data On Build`만 끄고, 신규 폰트 기본값인 전역 TMP Settings는 유지한다.
- EditMode 회귀 테스트는 각 프리베이크 폰트의 clear 비활성, non-empty glyph/character
  table과 유효한 source font 참조를 확인한다.

## 완료 기준

- [x] MobileBuild EditMode 테스트가 직렬화 값 `5`, 실제 프로젝트 orientation과 기존 drift를
  검증한다.
- [x] 프리베이크 폰트 회귀 테스트가 통과한다.
- [ ] unit 6 적용 실빌드의 Unity 종료 후 worktree clean 검사가 자동으로 통과한다.
- [x] PlayerSettings, Firebase 설정과 서명 파일에 영속 변경이 없다.
- [x] clean source `1861a96a8819841df68edeb53b51bf622fce174a`의 새 stem에서 Android
  빌드·자동 검증이 통과한다.
- [x] 이어지는 iOS archive/Ad Hoc export·자동 검증이 통과한다.
