# 1 — Manual Build and Distribution

## 목적

APK와 Ad Hoc IPA를 수동 서명·검증하고 Firebase `dreamsquad-demo` 그룹에 배포한다.

## 변경 대상

- 빌드 머신의 Unity/Xcode 로컬 설정·출력
- Firebase Console의 수동 릴리스

저장소 코드, Firebase SDK와 CI는 변경하지 않는다.

## 구현

### 공통 준비

1. Unity `6000.4.3f1`의 컴파일 상태와 `OutgameScene → BattleScene` 순서를 확인한다.
2. `Version`, Android `Bundle Version Code`, iOS `Build`를 입력한다. Build 값은 각 Firebase
   앱의 기존 Somnia/DreamSquad 배포보다 커야 한다.
3. 데이터 승계를 의도하지 않으면 기기의 기존 Somnia를 삭제한다.

### Android APK

1. Android로 전환하고 Development Build·Script Debugging을 켜며 App Bundle은 끈다.
2. IL2CPP, ARM64, Minimum API 26과 Somnia QA keystore/alias를 확인한다.
3. 저장소 외부에 APK를 빌드한다.
4. `aapt2`/`apksigner`로 package `com.playlinks.somnia.dev`, versionName/versionCode와
   Somnia QA 인증서 fingerprint를 확인한다.
5. `zipinfo -1 <apk>`로 `lib/arm64-v8a/libil2cpp.so`가 있고 다른 ABI나 Mono가 없는지 확인한다.

### iOS Ad Hoc IPA

1. iOS Development Build·Script Debugging으로 Xcode 프로젝트를 내보낸다.
2. `Unity-iPhone` Release를 Manual Signing으로 두고 Team `69DK98XF77`,
   `Apple Distribution`, `somnia_dev_adhoc`을 지정한다.
3. 프로파일 만료일과 대상 UDID를 확인한 뒤 Generic iOS Device를 Archive하고 Ad Hoc IPA로 내보낸다.
4. IPA를 풀어 `Payload/*.app`의 bundle ID, version/build와 embedded profile을 확인하고,
   해당 `.app`에 `codesign --verify --deep --strict`를 실행한다.

### Firebase App Distribution

1. `somnia-dev`에서 Android App ID `1:387788279107:android:90cb464e339dd7c9e5f3f6` 또는
   iOS App ID `1:387788279107:ios:1796ca5025a6ab8be5f3f6`를 선택한다.
2. 산출물을 `dreamsquad-demo` 그룹에 올리고 릴리스 노트에 제품·플랫폼·버전·build를 적는다.
3. 초대 링크로 설치해 로비, 익명 로그인, dev API와 전투 진입을 확인한다.

`dreamsquad-demo` 그룹은 테스터 대상을 구분할 뿐 Firebase 앱과 릴리스 이력을
Somnia에서 분리하지 않는다.

## 완료 기준

- [ ] Somnia 제거 후 APK를 설치해 로비·로그인·전투 진입이 성공한다.
- [ ] 다음 DreamSquad APK가 동일 서명으로 업데이트 설치된다.
- [ ] 등록된 iOS 기기에 Ad Hoc IPA가 설치되고 동일 smoke가 성공한다.
- [ ] Firebase의 Android/iOS 설치 링크가 `dreamsquad-demo` 그룹에서 동작한다.
- [ ] 빌드 후 서명 파일, 산출물 또는 예상하지 않은 tracked 변경이 남지 않는다.
