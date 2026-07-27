# 8 — Fixed Landscape Build Preflight

## 목적

프로젝트의 화면 방향 계약이 가로 자동회전에서 `LandscapeRight` 고정으로 바뀐 뒤에도
mobile build preflight가 현재 PlayerSettings를 정확히 검증하고 서명 빌드를 허용하게 한다.

## 변경 대상

- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs`
- `docs/spec/mobile-manual-distribution/README.md`

## 구현

- mobile build의 화면 방향 정본은 `PlayerSettings.defaultInterfaceOrientation ==
  UIOrientation.LandscapeRight`로 둔다.
- 고정 방향에서는 사용되지 않는 autorotation 허용 플래그를 preflight 상태와 판정에서 제거한다.
- `Portrait`, `PortraitUpsideDown`, `LandscapeLeft`, `AutoRotation`은 모두 configuration drift로
  거부한다.
- 실제 tracked PlayerSettings capture가 `LandscapeRight`인지 검증하는 EditMode 회귀 테스트를
  유지한다.
- 과거 unit 5의 serialized autorotation 값 `5` 호환은 당시 빌드 이력으로 보존하되, 현재 계약은
  이 작업 단위와 README가 우선한다.

## 완료 기준

- [x] MobileBuild EditMode 테스트가 고정 `LandscapeRight` 허용과 다른 방향 거부를 검증한다.
- [x] 실제 tracked PlayerSettings capture 테스트가 통과한다.
- [ ] build number `2`의 iOS Ad Hoc IPA 생성과 자동 검증이 통과한다.
- [ ] 빌드 종료 후 worktree가 clean하다.
