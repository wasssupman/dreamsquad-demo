# Mobile Manual Distribution

상태: **커밋 `1861a96a` 로컬 서명 APK·IPA 자동 검증 완료 · 실기기 설치와 Firebase QA 대기 — 2026-07-27**

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
| 4 | `4_build_machine_preflight_fix.md` | Unity Hub 모듈 위치 탐지 회귀 수정과 첫 빌드머신 검증 |
| 5 | `5_first_android_build_hygiene.md` | Android 첫 실행의 orientation 호환과 TMP 프리베이크 보존 |
| 6 | `6_signed_build_recovery_hardening.md` | 실패 재시도 stem, licensing 고아와 Unity 직렬화 노이즈 방어 |
| 7 | `7_handoff_summary.md` | 첫 서명 산출물과 hardening 검증 결과 인계 |

## Feature-wide 계약

- Android가 주 타깃이며 iOS는 **Ad Hoc 내부 QA 빌드만** 지원한다.
- Android/iOS Application Identifier는 모두 `com.playlinks.somnia.dev`다.
- 공개 진입점은 `./scripts/mobile/build.sh {android|ios|both} --version <version>
  --build <number> [--attempt <number>]`다.
- 버전은 명령 인자로 수동 관리하며 동일한 양의 `--build`를 Android `versionCode`와 iOS
  build number에 적용한다.
- 실패 산출물을 보존한 재시도는 명시적 `--attempt <positive-integer>`로 새 stem을 사용하며
  version, build number와 commit은 바꾸지 않는다.
- Firebase는 기존 `somnia-dev` 앱과 `dreamsquad-demo` 그룹을 사용한다. SDK·플랫폼 설정
  파일·Auth REST 설정·dev 게임 서버 연동은 변경하지 않는다.
- Android는 저장소 밖 기본 경로
  `~/Library/Application Support/Playlinks/Signing/Android/somnia-dev.keystore`와 alias
  `somnia-dev`를 사용하고 비밀번호는 실행할 때 숨김 입력한다.
- iOS는 Keychain에 설치된 Team `69DK98XF77`의 `Apple Distribution` 인증서와 설치된
  `somnia_dev_adhoc` 프로파일을 사용한다.
- Unity 플랫폼 모듈은 `Unity.app/Contents/PlaybackEngines`와 Unity Hub 버전 루트의
  `PlaybackEngines` 중 요청 플랫폼의 실제 모듈이 존재하는 위치를 사용한다.
- 현재 사용자 소유의 고아 `Unity.Licensing.Client`는 비밀번호 입력·출력 예약 전에 차단하며
  스크립트가 프로세스를 자동 종료하지 않는다.
- 가로 자동회전 검증은 Unity 6.4의 `UIOrientation.AutoRotation`과 프로젝트에 직렬화된
  `ScreenOrientation.AutoRotation` 값 `5`를 같은 의미로 인정하되, 세로 2방향 금지와
  가로 2방향 허용을 함께 요구한다.
- 실행 전후 Git worktree가 clean이어야 하며 기존 출력은 삭제하거나 덮어쓰지 않는다.
- 프로젝트별 mobile build lock으로 두 로컬 빌드가 같은 Unity 설정을 동시에 다루지 못하게 한다.
- Unity가 만드는 확정된 no-op 직렬화만 빌드 전 원본과 정확히 대조해 복원하고, 그 밖의
  tracked 변경은 보존한 채 실패한다.
- 버전·build·Android 서명 PlayerSettings는 빌드 중에만 적용하고 성공/실패 모두 원복한다.
- 산출물과 중간 파일은 ignored `Builds/Mobile`에 두고 APK/IPA 서명·식별자·버전·아키텍처를
  자동 검증한 뒤 SHA-256을 남긴다.
- 서명 파일·비밀번호·private key·산출물은 저장소나 로그에 기록하지 않는다.
- Somnia와 앱 ID가 같으므로 두 앱은 같은 기기에 공존하지 않으며 저장 데이터가 이어질 수 있다.

## 실산출물 검증 기록

- Android APK와 iOS Ad Hoc IPA는 source commit
  `1861a96a8819841df68edeb53b51bf622fce174a`, version `0.1.0`, build `1`에서 생성했다.
- Android SHA-256은 `b6b4f9187e97a9d8b673363948c6180aa6b876324e2fb2934de76af951c0f60c`,
  iOS SHA-256은 `4210a4614ba19ddee0c0964f1daef8271d8894ccec64e8aed31578fa012cf4db`다.
- 두 summary의 package/bundle, version/build, Android signer·ARM64 IL2CPP, iOS
  codesign·Ad Hoc profile 검증값이 모두 통과했다.
- 이후 현재 브랜치에는 게임 코드 변경과 build hardening이 추가됐다. 위 산출물은 현재 HEAD를
  다시 빌드한 결과가 아니다. 실기기 설치와 Firebase 업로드는 수행하지 않았다.

## 비목표

- GitLab CI, 예약/원격 빌드, 자동 Firebase 업로드, 자동 versionCode 발급
- Android App Bundle 또는 Google Play/App Store 제출
- 별도 Firebase 앱 등록, Firebase Analytics/Messaging 도입
- P12/provisioning profile 자동 설치, Keychain 자동 잠금 해제, keystore 저장소 보관
- iOS를 위한 일반적인 멀티플랫폼 추상화나 iOS 전용 게임 기능
