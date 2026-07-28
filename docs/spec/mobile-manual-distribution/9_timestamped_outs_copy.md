# 9 — Timestamped Outs Copy

## 목적

기존 `Builds/Mobile`의 플랫폼별 원본·로그·중간 산출물을 보존하면서, 사람이 바로 찾아
전달할 수 있는 검증된 APK·IPA 복사본을 빌드 시각이 포함된 이름으로 `Builds/outs`에 모은다.

## 변경 대상

- `scripts/mobile/build.sh`
- `scripts/mobile/tests/build_sh_test.sh`
- `docs/spec/mobile-manual-distribution/{README.md,1_*.md,2_*.md}`

## 구현

- `configure_paths`에서 로컬 빌드 시각을 `YYYYMMDD-HHMMSS`로 한 번 계산한다.
- 복사본 이름은
  `dreamquad-demo--{version}-{build}-YYYYMMDD-HHMMSS-{sha8}[-attemptN].{apk|ipa}`다.
- `both` 빌드는 APK와 IPA에 동일한 timestamp를 사용한다.
- 요청한 모든 플랫폼의 빌드·서명 검증과 마지막 clean-worktree 검사가 성공한 뒤에만 복사한다.
- `Builds/outs`만 필요할 때 생성하며 `Builds/Mobile`을 포함한 기존 폴더를 이동·삭제하지 않는다.
- `Builds/outs` 또는 대상 파일이 symlink이거나 대상 파일이 이미 있으면 덮어쓰지 않고 실패한다.
- 복사는 같은 디렉터리의 임시 파일에 수행하고 원본과 byte 비교한 뒤, 기존 대상이 있으면
  실패하는 원자적 hard link로 최종 이름을 게시하고 임시 이름을 제거한다.

## 완료 기준

- [x] `bash -n scripts/mobile/build.sh scripts/mobile/tests/build_sh_test.sh`가 통과한다.
- [ ] Shell 회귀 테스트가 고정 timestamp 이름, 양 플랫폼 복사와 덮어쓰기 거부를 검증한다.
- [ ] 실제 macOS 빌드에서 `Builds/Mobile` 원본과 `Builds/outs` 복사본이 함께 남는다.
