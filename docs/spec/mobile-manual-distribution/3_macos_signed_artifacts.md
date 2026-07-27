# 3 — macOS Signed Artifacts

## 목적

Somnia dev와 같은 서명 계보의 Android APK와 등록 기기용 iOS Ad Hoc IPA를 생성하고,
Firebase에 올리기 전에 설치 가능성에 직접 영향을 주는 식별자·서명·버전·아키텍처를
자동 검증한다.

## 변경 대상

- `scripts/mobile/build.sh`
- `Assets/_Project/Editor/MobileBuild/`
- `Builds/Mobile/`의 ignored 로컬 산출물

## 구현

### Android

- keystore 기본 경로는
  `~/Library/Application Support/Playlinks/Signing/Android/somnia-dev.keystore`, alias는
  `somnia-dev`다. `--keystore`는 저장소 밖 다른 경로만 허용한다.
- 빌드 전에 keystore 파일과 부모 디렉터리가 현재 사용자 외에 쓰기 가능하지 않은지,
  `keytool`로 alias가 존재하는지 확인한다. 비밀번호나 인증서 원문은 출력하지 않는다.
- APK는 `com.playlinks.somnia.dev`, IL2CPP, ARM64-only, Minimum API 26이며 AAB를 만들지 않는다.
- `aapt2`로 package/versionName/versionCode, `apksigner verify --verbose --print-certs`와
  `keytool` fingerprint 비교로 서명 일치를 확인한다.
- ZIP 목록에서 `lib/arm64-v8a/libil2cpp.so`가 있고 Mono 또는 다른 ABI가 없는지 확인한다.

### iOS

- P12는 로그인 Keychain에 private key와 함께 설치되어 있고 Keychain은 unlocked라고
  가정한다. 설치된 `somnia_dev_adhoc`은 Team `69DK98XF77`,
  `com.playlinks.somnia.dev`, 만료일 `2027-07-20`과 대상 UDID를 가져야 한다.
- Unity Xcode export 뒤 `Unity-iPhone` main target Release에 Manual Signing,
  Team `69DK98XF77`, `Apple Distribution`, profile `somnia_dev_adhoc`만 적용한다.
- Generic iOS Device archive 후 manual signing의 `method=ad-hoc` ExportOptions로 IPA를
  만든다. 인증서/profile import와 CocoaPods 설치는 하지 않는다.
- 정확히 하나의 IPA와 `Payload/*.app`을 요구하고 `.app`에
  `codesign --verify --deep --strict`를 실행한다.
- Info.plist의 bundle ID/version/build, codesign TeamIdentifier, embedded profile의
  Name/UUID/Team/App ID/ExpirationDate/ProvisionedDevices를 기대값과 비교한다.

각 최종 APK/IPA의 SHA-256을 같은 출력 stem의 요약에 기록한다. 원본 인증서, private key,
password, keystore 경로는 로그나 요약에 포함하지 않으며 검증 실패 산출물은 성공으로
보고하지 않는다.

## 완료 기준

- [x] APK signer SHA-256이 alias `somnia-dev` 인증서와 같고 package/version/build가 일치한다.
- [x] APK에 ARM64 IL2CPP만 포함되고 `apksigner` 검증이 성공한다.
- [x] IPA의 strict codesign과 bundle/version/build/Team 검증이 성공한다.
- [x] embedded profile이 예상 Name/App ID/Team, 유효한 만료일과 등록 기기를 가진다.
- [x] APK/IPA마다 SHA-256이 생성되고 로그·요약에 비밀값이 없다.
- [x] 같은 출력 stem이 존재할 때 중단하며 기존 archive/IPA/APK를 덮어쓰지 않는다.

확인: 2026-07-27, artifact source `1861a96a8819841df68edeb53b51bf622fce174a`.
