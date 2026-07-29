# 11 — Landscape Autorotation Regression

## 목적

Android와 iOS 모두 세로 방향은 허용하지 않되, 기기의 상하가 바뀌면 두 가로 방향 사이에서
자동회전하도록 빌드 계약을 고정한다.

## 변경 대상

- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs`
- `docs/spec/mobile-manual-distribution/README.md`

## 구현

- mobile build preflight는 `AutoRotation`, 세로 2방향 금지, 가로 2방향 허용을 모두 요구한다.
- `LandscapeLeft`와 `LandscapeRight` 고정 설정은 가로 화면이어도 회전 요구를 만족하지
  않으므로 거부한다.
- tracked `ProjectSettings.asset`의 Unity 직렬화 값 `5`를 자동회전으로 검증한다.
- 2026-07-27 iOS build 2는 고정 `LandscapeRight` 설정으로 생성된 과거 산출물이며,
  수정 검증에는 자동회전 설정으로 새 iOS IPA를 생성해야 한다.

## 완료 기준

- [ ] 고정 가로 방향을 거부하는 EditMode 회귀 테스트가 통과한다.
- [x] tracked PlayerSettings가 가로 양방향 자동회전으로 검증된다.
- [ ] MobileBuild EditMode 테스트가 통과한다.
- [ ] 새 iOS IPA에서 기기를 180도 돌렸을 때 `LandscapeLeft/Right` 사이로 회전한다.
