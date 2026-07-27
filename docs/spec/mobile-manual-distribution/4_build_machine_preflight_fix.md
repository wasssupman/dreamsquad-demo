# 4 — Build Machine Preflight Fix

## 목적

첫 macOS 실산출물 검증에서 드러난 Unity Hub 플랫폼 모듈 경로 오판을 수정한다. 확정한
version/build와 서명 계약은 바꾸지 않고 Android/iOS preflight가 실제 설치를 인식하게 한다.

## 변경 대상

- `scripts/mobile/build.sh`
- `scripts/mobile/tests/build_sh_test.sh`
- `docs/spec/mobile-manual-distribution/README.md`
- `docs/spec/mobile-manual-distribution/2_mobile_build_cli.md`

## 구현

- Unity 실행 파일은 기존 기본 경로와 `UNITY_EDITOR_PATH` 계약을 유지한다.
- 요청 플랫폼의 `AndroidPlayer`/`iOSSupport`는 다음 두 macOS 배치를 순서대로 확인한다.
  - Unity Hub 에디터 버전 루트의 `PlaybackEngines`
  - `Unity.app/Contents/PlaybackEngines`
- Android `aapt2`, `apksigner`, `keytool` 탐색은 preflight가 확정한 `AndroidPlayer`
  모듈 경로를 재사용한다. 실행 파일 위치에서 별도로 잘못 계산하지 않는다.
- Shell 회귀 테스트는 Hub 버전 루트 배치, Contents 배치, 모듈 부재 실패를 임시 fixture로
  검증한다.
- iOS 서명 파일은 수정하지 않는다. 같은 이름의 구 프로파일과 최신 프로파일이 함께 설치된
  경우 기존 안전 계약대로 1개만 남긴 뒤 실행한다.

## 완료 기준

- [x] `bash -n scripts/mobile/build.sh`와 `bash -n scripts/mobile/tests/build_sh_test.sh`가 통과한다.
- [x] `scripts/mobile/tests/build_sh_test.sh`가 세 모듈 탐지 케이스를 통과한다.
- [x] 실제 Unity `6000.4.3f1`의 Android/iOS Build Support와 Android 도구를 인식한다.
- [x] `somnia_dev_adhoc` 최신 프로파일 1개와 예상 배포 identity preflight가 통과한다.
- [x] clean source `1861a96a8819841df68edeb53b51bf622fce174a`에서 version `0.1.0`,
  build `1`, target `both`가 기존 출력 없이 시작된다.
- [x] APK/IPA 자동 검증과 SHA-256 생성이 성공한다.
- [ ] 첫 실빌드에서 수동 복원이 필요했던 Unity 직렬화 노이즈가 unit 6 적용 실빌드에서는
  자동 복원되고 worktree가 clean이다.
