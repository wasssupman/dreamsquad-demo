# 0 — Project Settings

## 목적

기존 게임 동작과 씬 구성을 유지하면서 Android APK와 iOS Xcode 프로젝트가
Somnia dev 앱 식별자로 생성되도록 최소 PlayerSettings와 저장소 안전 규칙을 적용한다.

## 변경 대상

- `ProjectSettings/ProjectSettings.asset`
- `.gitignore`
- `CLAUDE.md`
- `docs/TRD.md`

## 구현

- Company Name: `Playlinks`
- Product Name: `DreamSquad Demo`
- Android Application Identifier: `com.playlinks.somnia.dev`
- iOS Bundle Identifier: `com.playlinks.somnia.dev`
- Android 최소 API: 26
- 다음 기존 설정은 유지한다.
  - Unity `6000.4.3f1`
  - 씬 순서 `OutgameScene → BattleScene`
  - Android IL2CPP, ARM64
  - iOS 15.0, iPhone/iPad, Landscape
- Android keystore 경로·비밀번호와 Apple 서명값은 PlayerSettings에 저장하지 않는다.
  로컬 스크립트가 실행 중 외부 keystore와 고정 alias `somnia-dev`를 주입했다가 원복하며,
  생성한 iOS Xcode export에는 Team/profile을 적용한다.
- `.gitignore`는 keystore/JKS/P12/mobileprovision 및 APK/IPA/Xcode 로컬 출력이
  추적되지 않도록 차단한다.
- 프로젝트 정책은 Android 주 타깃을 유지하면서 iOS Ad Hoc 내부 QA 빌드만 예외로 허용한다.

## 완료 기준

- [x] PlayerSettings의 Android/iOS 식별자가 정확히 `com.playlinks.somnia.dev`다.
- [x] Android 최소 API 26, IL2CPP, ARM64와 기존 씬 순서가 유지된다.
- [x] iOS 15.0과 Landscape 설정이 유지된다.
- [x] 서명 재료와 로컬 모바일 빌드 출력이 Git에 노출되지 않는다.
- [x] iOS 내부 QA 예외가 프로젝트 정책 문서에 반영된다.
