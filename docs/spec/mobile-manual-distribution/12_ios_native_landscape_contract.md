# 12 — iOS Native Landscape Contract

## 목적

PlayerSettings preflight만 통과하고 실제 iOS 앱이 세로로 실행되는 검증 공백을 제거한다.
iOS 네이티브 선언, Unity 런타임 상태, 최종 IPA를 모두 가로 양방향 계약으로 맞춘다.

## 변경 대상

- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Scripts/Core/MobileScreenOrientation.cs`
- `scripts/mobile/build.sh`
- `scripts/mobile/tests/build_sh_test.sh`
- `docs/spec/mobile-manual-distribution/README.md`

## 구현

- Xcode export의 `Info.plist`에서 iPhone 및 iPad 지원 방향을
  `LandscapeLeft/Right` 두 개로 명시하고 fullscreen을 요구한다.
- 앱 시작 전 Unity `Screen` 자동회전 상태도 세로 금지·가로 양방향 허용으로 설정한다.
- IPA 검증은 iPhone/iPad 방향 배열이 정확히 두 가로 방향인지 확인하고, 세로 또는 추가
  방향이 있으면 빌드를 실패시킨다.
- 성공 summary에 `landscapeOrientationVerified=true`를 기록한다.

## 완료 기준

- [ ] Shell fixture와 스크립트 문법 검증이 통과한다.
- [ ] Unity 컴파일과 MobileBuild EditMode 테스트가 통과한다.
- [ ] 새 iOS IPA summary에 `landscapeOrientationVerified=true`가 기록된다.
- [ ] 실기기에서 앱이 세로로 실행되지 않고 두 가로 방향 사이에서 회전한다.
