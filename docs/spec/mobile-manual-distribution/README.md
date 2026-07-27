# Mobile Manual Distribution

상태: **로컬 빌드 자동화 추가 · 빌드머신 실산출물 검증 대기 — 2026-07-27**

## 목표

macOS의 Unity `6000.4.3f1` 빌드 머신에서 DreamSquad Demo의 내부 QA용 서명 Android
APK와 iOS Ad Hoc IPA를 재현 가능한 로컬 명령으로 만들고 Firebase App Distribution에는
배포 담당자가 수동 업로드한다.

## 작업 단위

| 번호 | 문서 | 목적 |
|---|---|---|
| 0 | `0_project_settings.md` | Android/iOS 앱 식별자, 제품 정보, 플랫폼 정책과 저장소 안전 설정 |
| 1 | `1_manual_build_and_distribution.md` | 로컬 CLI 실행, Firebase 수동 업로드와 실기기 QA 절차 |
| 2 | `2_mobile_build_cli.md` | Shell 진입점과 Unity Editor 빌드 계층, 입력·복원·실패 계약 |
| 3 | `3_macos_signed_artifacts.md` | Android/iOS 서명 산출물 생성과 자동 검증 계약 |

## Feature-wide 계약

- Android가 주 타깃이며 iOS는 **Ad Hoc 내부 QA 빌드만** 지원한다.
- Android/iOS Application Identifier는 모두 `com.playlinks.somnia.dev`다.
- 공개 진입점은 `./scripts/mobile/build.sh {android|ios|both} --version <version> --build <number>`다.
- 버전은 명령 인자로 수동 관리하며 동일한 양의 `--build`를 Android `versionCode`와 iOS
  build number에 적용한다.
- Firebase는 기존 `somnia-dev` 앱과 `dreamsquad-demo` 그룹을 사용한다. SDK·플랫폼 설정
  파일·Auth REST 설정·dev 게임 서버 연동은 변경하지 않는다.
- Android는 저장소 밖 기본 경로
  `~/Library/Application Support/Playlinks/Signing/Android/somnia-dev.keystore`와 alias
  `somnia-dev`를 사용하고 비밀번호는 실행할 때 숨김 입력한다.
- iOS는 Keychain에 설치된 Team `69DK98XF77`의 `Apple Distribution` 인증서와 설치된
  `somnia_dev_adhoc` 프로파일을 사용한다.
- 실행 전후 Git worktree가 clean이어야 하며 기존 출력은 삭제하거나 덮어쓰지 않는다.
- 버전·build·Android 서명 PlayerSettings는 빌드 중에만 적용하고 성공/실패 모두 원복한다.
- 산출물과 중간 파일은 ignored `Builds/Mobile`에 두고 APK/IPA 서명·식별자·버전·아키텍처를
  자동 검증한 뒤 SHA-256을 남긴다.
- 서명 파일·비밀번호·private key·산출물은 저장소나 로그에 기록하지 않는다.
- Somnia와 앱 ID가 같으므로 두 앱은 같은 기기에 공존하지 않으며 저장 데이터가 이어질 수 있다.

## 비목표

- GitLab CI, 예약/원격 빌드, 자동 Firebase 업로드, 자동 versionCode 발급
- Android App Bundle 또는 Google Play/App Store 제출
- 별도 Firebase 앱 등록, Firebase Analytics/Messaging 도입
- P12/provisioning profile 자동 설치, Keychain 자동 잠금 해제, keystore 저장소 보관
- iOS를 위한 일반적인 멀티플랫폼 추상화나 iOS 전용 게임 기능
