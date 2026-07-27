# 6 — Signed Build Recovery Hardening

## 목적

첫 양 플랫폼 실빌드에서 확인한 실패 복구와 macOS Unity batchmode 위생을 자동화한다.
기존 실패 산출물, version/build number, 서명 계약을 바꾸지 않고 새 시도를 안전하게 구분한다.

## 변경 대상

- `scripts/mobile/build.sh`
- `scripts/mobile/tests/build_sh_test.sh`
- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `docs/spec/mobile-manual-distribution/{README.md,1_*.md,2_*.md,3_*.md,4_*.md,5_*.md}`
- `docs/reference/lessons/02-dev-workflow-git-scene.md`

## 구현

- `--attempt <positive-integer>`는 `{sha8}-attempt{N}` 새 stem만 선택한다.
  version/build/commit과 summary 검증값은 바꾸지 않고 기존 출력은 계속 보존한다.
- ignored project lock을 원자적으로 점유해 mobile build 동시 실행을 막는다. stale lock은
  자동 삭제하지 않는다.
- macOS process snapshot에서 현재 UID, `PPID=1`, 실행 파일 basename이 정확히
  `Unity.Licensing.Client`인 항목만 숫자 PID로 보고하고 중단한다. argv·경로·사용자명은
  출력하지 않으며 자동 종료하지 않는다. 비밀번호/출력 예약 전과 Unity 실행 직전에 검사한다.
- Unity `finally`는 snapshot 복원 후 `AssetDatabase.SaveAssets()`로 복원값을 확정한다.
  Shell은 tracked 두 파일의 clean 원본을 byte snapshot한다.
- 각 Unity 실행 직후에만 tracked 복원을 arm한다. HEAD/index 고정, unrelated
  tracked/untracked 없음, 아래 전체-file 후보 exact match가 모두 성립할 때만 같은
  filesystem의 임시 regular file을 atomic rename해 원복한다.
  - 빈 Android keystore 이름의 `{inproject}: ` canonicalization
  - 기본 iPhone static/dynamic batching 항목 추가
  - URP의 obsolete point-sampling prefilter 필드 제거
- provenance를 판별할 수 없는 ignored Unity editor 설정은 자동 삭제·복원하지 않는다.
- 종료 신호는 Unity 종료와 raw log 정리·복원이 끝날 때까지 지연한다. 모든 불일치와 복원 실패는
  기존 파일을 보존하고 non-zero로 끝낸다.

## 완료 기준

- [x] `--attempt` 생략/정상/중복/범위 오류와 새 stem 충돌 방지가 검증된다.
- [x] licensing fixture가 동일 UID 고아만 찾고 오류에는 PID만 남긴다.
- [x] lock, 신호, symlink와 Git 검사 실패가 fail-closed로 검증된다.
- [x] 세 직렬화 그룹의 단독/조합은 복원되고 불완전/추가 diff는 보존·실패한다.
- [x] `bash -n`, Shell 회귀 테스트와 격리 MobileBuild EditMode 60개가 통과한다.
- [x] artifact source와 미실행 실기기/Firebase QA가 README에 구분된다.
- [ ] hardening 적용 clean commit에서 target `both`를 다시 실행해 자동 clean을 통합 검증한다.

확인: 2026-07-27, 구현 commit `4c054129a27d4224d4b1b56d4574b20dca4b47e4`.
