# 10 — iOS AllowDebugging 제외

## 목적

iOS Ad Hoc archive에서 IL2CPP `GameAssembly` static archive가 4GB member-offset 한계를 넘는
문제를 피하면서, Android QA의 원격 디버깅 계약은 유지한다.

## 변경 대상

- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs`
- `docs/spec/mobile-manual-distribution/README.md`

## 구현

- `CreateBuildPlayerOptions`는 두 플랫폼 모두 `BuildOptions.Development`를 설정한다.
- Android에만 `BuildOptions.AllowDebugging`을 추가한다.
- iOS Ad Hoc은 development QA 동작을 유지하되 managed debugger와 그 심볼을 포함하지 않는다.
- EditMode 테스트는 Android의 debugging 유지와 iOS의 debugging 제외를 각각 고정한다.

## 완료 기준

- [x] Android는 `Development | AllowDebugging`, iOS는 `Development` 옵션만 생성하는 EditMode
  회귀 테스트가 있다.
- [ ] Unity EditMode 테스트가 통과한다.
- [ ] 사용자가 `./scripts/mobile/build.sh ios --version 0.1.0 --build 8 --attempt 1`을 실행해
  `GameAssembly` archive와 IPA 생성이 모두 통과함을 확인한다.
