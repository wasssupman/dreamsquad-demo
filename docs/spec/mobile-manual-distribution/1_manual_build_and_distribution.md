# 1 — Local Build and Manual Distribution

## 목적

macOS 빌드머신에서 버전 인자를 명시해 서명 APK/Ad Hoc IPA를 생성하고 Firebase
`dreamsquad-demo` 그룹에 수동 배포하는 운영 절차를 고정한다.

## 변경 대상

- `scripts/mobile/build.sh`
- 저장소 밖 서명 재료와 ignored `Builds/Mobile` 출력
- Firebase Console의 수동 릴리스

## 구현

### 준비와 실행

1. `git status --porcelain`이 비어 있는지 확인한다.
2. Unity `6000.4.3f1` Android/iOS Build Support, Xcode, Unity 라이선스를 확인한다.
3. `--version`은 `숫자.숫자[.숫자]`, `--build`는 기존 양 플랫폼 릴리스보다 큰 양의
   정수로 정한다.
4. 필요한 플랫폼을 실행한다.

```bash
./scripts/mobile/build.sh android --version 0.1.0 --build 123
./scripts/mobile/build.sh ios     --version 0.1.0 --build 123
./scripts/mobile/build.sh both    --version 0.1.0 --build 123
```

Unity 기본 경로는
`/Applications/Unity/Hub/Editor/6000.4.3f1/Unity.app/Contents/MacOS/Unity`이며 다른
위치는 `UNITY_EDITOR_PATH`로 지정한다. 출력 stem은
`Builds/Mobile/DreamSquad-Demo-{version}-{build}-{sha8}[-attemptN]`이다. 기존 출력은
덮어쓰지 않는다. 실패 산출물을 보존하고 같은 version/build/commit을 재시도할 때만
`--attempt <positive-integer>`를 명시한다.

빌드 전체가 성공하면 검증된 최종 파일은 기존 출력과 별도로
`Builds/outs/dreamquad-demo--{version}-{build}-YYYYMMDD-HHMMSS-{sha8}[-attemptN].apk`
또는 `.ipa`에도 복사된다. `both` 실행의 두 복사본은 같은 실행 시각을 사용하며,
`Builds/Mobile` 아래의 원본·로그·중간 산출물은 그대로 남는다.

### Android APK

기본 keystore는
`~/Library/Application Support/Playlinks/Signing/Android/somnia-dev.keystore`, alias는
`somnia-dev`다. 다른 저장소 밖 경로는 `--keystore <path>`로 지정한다. keystore password는
숨김 입력하고 key password를 비우면 같은 값을 사용한다.

스크립트가 package/version/build, Somnia QA 서명, ARM64 IL2CPP-only와 SHA-256을 검증한다.

### iOS Ad Hoc IPA

로그인 Keychain은 private key가 포함된 `Apple Distribution` 인증서를 설치하고 unlock한
상태여야 한다. Team은 `69DK98XF77`, 설치된 profile은 `somnia_dev_adhoc`이다. 스크립트는
Xcode export, Manual Signing archive, Ad Hoc IPA export와 bundle/version/build/Team,
embedded profile, strict codesign, SHA-256 검증을 수행한다. P12/profile import는 하지 않는다.

### Firebase App Distribution

1. `somnia-dev`에서 해당 앱을 선택한다.
   - Android: `1:387788279107:android:90cb464e339dd7c9e5f3f6`
   - iOS: `1:387788279107:ios:1796ca5025a6ab8be5f3f6`
2. 검증된 산출물을 `dreamsquad-demo` 그룹에 올리고 제품·플랫폼·version/build를 기록한다.
3. 기존 Somnia를 삭제한 뒤 링크로 설치해 로비, 익명 로그인, dev API와 전투를 확인한다.

## 완료 기준

- [ ] Somnia 제거 후 Android/iOS clean install과 공통 smoke가 성공한다.
- [ ] 다음 Android build가 동일 서명으로 업데이트 설치된다.
- [ ] 등록된 iOS 기기에서 IPA가 설치·실행된다.
- [ ] Firebase Android/iOS 링크가 `dreamsquad-demo` 그룹에서 동작한다.
- [x] source `1861a96a8819841df68edeb53b51bf622fce174a`의 APK/IPA 자동 검증과
  SHA-256 생성이 성공했고 저장소에 서명 재료를 추가하지 않았다. (2026-07-27)
