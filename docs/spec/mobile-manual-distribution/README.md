# Mobile Manual Distribution

상태: **저장소 구성 완료 · 빌드머신 검증 대기 — 2026-07-27**

## 목표

Unity `6000.4.3f1` 빌드 머신에서 DreamSquad Demo의 내부 QA용 Android APK와
iOS Ad Hoc IPA를 수동 생성하고, Firebase App Distribution에 수동 업로드할 수 있게 한다.

## 작업 단위

| 번호 | 문서 | 목적 |
|---|---|---|
| 0 | `0_project_settings.md` | Android/iOS 앱 식별자, 제품 정보, 플랫폼 정책과 저장소 안전 설정 |
| 1 | `1_manual_build_and_distribution.md` | 수동 버전·서명·빌드·Firebase 업로드 및 검증 절차 |

## Feature-wide 계약

- Android가 주 타깃이며 iOS는 **Ad Hoc 내부 QA 빌드만** 지원한다.
- Android/iOS Application Identifier는 모두 `com.playlinks.somnia.dev`다.
- Firebase 프로젝트는 기존 `somnia-dev`를 사용하고 테스터 그룹은 `dreamsquad-demo`다.
  - Android App ID: `1:387788279107:android:90cb464e339dd7c9e5f3f6`
  - iOS App ID: `1:387788279107:ios:1796ca5025a6ab8be5f3f6`
- 기존 Firebase Auth REST API 키와 dev 게임 서버 연동은 변경하지 않는다.
- Firebase Unity SDK, `google-services.json`, `GoogleService-Info.plist`를 추가하지 않는다.
- 버전 문자열, Android `versionCode`, iOS build number는 배포 담당자가 수동 관리한다.
- Android는 Somnia QA keystore, iOS는 Team `69DK98XF77`의 Apple Distribution 인증서와
  `somnia_dev_adhoc` 프로파일을 사용한다.
- 서명 파일, 비밀번호, APK/IPA/Xcode 산출물은 저장소에 커밋하지 않는다.
- Somnia와 앱 ID가 같으므로 두 앱은 같은 기기에 공존하지 않으며 저장 데이터가 이어질 수 있다.

## 비목표

- GitLab CI, 자동 빌드, 자동 Firebase 업로드, 자동 versionCode 발급
- Android App Bundle 또는 Google Play/App Store 제출
- 별도 Firebase 앱 등록, Firebase Analytics/Messaging 도입
- iOS를 위한 일반적인 멀티플랫폼 추상화나 iOS 전용 게임 기능
