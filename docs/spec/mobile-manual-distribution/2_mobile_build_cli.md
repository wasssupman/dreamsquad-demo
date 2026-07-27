# 2 — Mobile Build CLI

## 목적

macOS Shell과 Unity batchmode로 양 플랫폼을 빌드하되 비밀값과 PlayerSettings 변경을 남기지 않는다.

## 변경 대상

- `scripts/mobile/build.sh`
- `Assets/_Project/Editor/MobileBuild/Wassup.Editor.MobileBuild.asmdef`
- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Tests/EditMode/MobileBuild/`
- `Assets/_Project/Tests/EditMode/Wassup.Tests.EditMode.asmdef`

## 구현

```bash
./scripts/mobile/build.sh {android|ios|both} \
  --version <major.minor[.patch]> --build <positive-integer> \
  [--attempt <positive-integer>] \
  [--keystore <absolute-or-home-relative-path>]
```

- 인자, Unity/모듈/도구, clean worktree를 먼저 검증한다. `both`는 출력 충돌을 모두 확인한
  뒤 Android → iOS 순서다.
- ignored `Library/DreamSquadMobileBuild.lock`을 원자적으로 점유해 같은 프로젝트의 mobile
  build를 한 번에 하나만 허용한다. stale lock은 자동 삭제하지 않는다.
- 실패 출력은 삭제하지 않는다. 동일 version/build/commit을 다시 실행해야 하면 사용자가
  `--attempt`를 명시해 `{sha8}-attempt{N}` 새 stem을 예약한다.
- Unity 기본 경로는
  `/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity`이며
  `UNITY_EDITOR_PATH`로만 재정의한다. 플랫폼 모듈과 Android SDK/OpenJDK는 macOS Hub의
  버전 루트 `PlaybackEngines`를 우선 포함해 실제 요청 모듈이 존재하는 위치에서 찾는다.
- keystore/key password는 숨김 입력하며 key 값을 비우면 keystore 값을 재사용한다. 비밀값은
  필요한 자식 환경에서만 사용하고 command line·최종 로그·요약에 남기지 않으며 trap에서 제거한다.
- Unity 자식에는 `DREAMSQUAD_BUILD_VERSION`, `DREAMSQUAD_BUILD_NUMBER`,
  `DREAMSQUAD_BUILD_OUTPUT`을 전달하고 Android에는 `DREAMSQUAD_ANDROID_KEYSTORE`,
  `DREAMSQUAD_ANDROID_KEYSTORE_PASSWORD`, `DREAMSQUAD_ANDROID_KEY_PASSWORD`도 전달한다.
- Unity `-executeMethod`는
  `Wassup.Editor.MobileBuild.DreamSquadMobileBuildCli.BuildAndroidQa`와
  `ExportIosQa`다. 오류는 비밀값 없는 메시지와 non-zero exit로 반환한다.
- Unity 계층은 `OutgameScene → BattleScene`을 명시한 `BuildPipeline.BuildPlayer`를 사용한다.
  Unity/제품/앱 ID/플랫폼 설정을 preflight하고 `Development | AllowDebugging`으로 빌드한다.
- `bundleVersion`과 동일 build의 Android `bundleVersionCode`/iOS `buildNumber`, Android
  signing 값은 실행 중에만 적용한다. snapshot을 `finally`에서 먼저 복원한 뒤
  `AssetDatabase.SaveAssets()`로 복원 상태를 확정한다. Shell 계층은 이때 발생하는 허용된
  no-op 직렬화만 빌드 전 byte snapshot으로 되돌린다.
- `stem=DreamSquad-Demo-{version}-{build}-{sha8}[-attemptN]`로 두고 APK는
  `Builds/Mobile/<stem>/Android/<stem>.apk`, iOS는 같은 root 아래
  `iOS/Xcode`, `iOS/<stem>.xcarchive`, `iOS/Export`, `iOS/<stem>.ipa`에 둔다.
  플랫폼 디렉터리가 있으면 덮어쓰지 않는다.
- 모든 요청 플랫폼의 빌드와 검증이 성공하고 worktree가 clean임을 확인한 뒤, 최종 APK·IPA를
  `Builds/outs/dreamquad-demo--{version}-{build}-YYYYMMDD-HHMMSS-{sha8}[-attemptN].{apk|ipa}`로
  복사한다. timestamp는 스크립트 실행당 한 번 계산하며 기존 `Builds/Mobile` 출력과
  `Builds/outs` 파일은 덮어쓰지 않는다.

## 완료 기준

- [x] 잘못된 target·누락/잘못된 version/build·Unity/모듈 부재가 빌드 전에 실패한다.
- [x] dirty worktree와 출력 충돌이 실패하고 기존 산출물을 변경하지 않는다.
- [x] 생성된 `BuildPlayerOptions`가 정확한 target·씬·Development/Debugging을 가진다.
- [x] 성공/예외 모두 PlayerSettings snapshot을 원복한다.
- [ ] unit 6 hardening이 적용된 clean commit의 실빌드 종료 후 worktree가 자동으로 clean이다.
- [x] 로그·프로세스 명령행에 password, private key와 keystore 경로가 노출되지 않는다.
- [x] MobileBuild EditMode 60개와 Shell 회귀 테스트, 두 스크립트의 `bash -n`이 통과한다.
- [x] Shell 회귀 테스트가 Hub 버전 루트와 `Unity.app/Contents` 양쪽 모듈 배치를 검증한다.
