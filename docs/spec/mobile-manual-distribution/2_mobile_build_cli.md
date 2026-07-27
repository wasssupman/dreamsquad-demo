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
  [--keystore <absolute-or-home-relative-path>]
```

- 인자, Unity/모듈/도구, clean worktree를 먼저 검증한다. `both`는 출력 충돌을 모두 확인한
  뒤 Android → iOS 순서다.
- Unity 기본 경로는
  `/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity`이며
  `UNITY_EDITOR_PATH`로만 재정의한다.
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
  signing 값은 실행 중에만 적용한다. snapshot을 `finally`에서 복원하며 AssetDatabase에
  저장하지 않는다.
- `stem=DreamSquad-Demo-{version}-{build}-{sha8}`로 두고 APK는
  `Builds/Mobile/<stem>/Android/<stem>.apk`, iOS는 같은 root 아래
  `iOS/Xcode`, `iOS/<stem>.xcarchive`, `iOS/Export`, `iOS/<stem>.ipa`에 둔다.
  플랫폼 디렉터리가 있으면 덮어쓰지 않는다.

## 완료 기준

- [ ] 잘못된 target·누락/잘못된 version/build·Unity/모듈 부재가 빌드 전에 실패한다.
- [ ] dirty worktree와 출력 충돌이 실패하고 기존 산출물을 변경하지 않는다.
- [ ] 생성된 `BuildPlayerOptions`가 정확한 target·씬·Development/Debugging을 가진다.
- [ ] 성공/예외 모두 PlayerSettings snapshot이 원복되고 실행 후 worktree가 clean이다.
- [ ] 로그·프로세스 명령행에 password, private key와 keystore 경로가 노출되지 않는다.
- [ ] EditMode CLI/preflight/snapshot 테스트와 `bash -n scripts/mobile/build.sh`가 통과한다.
