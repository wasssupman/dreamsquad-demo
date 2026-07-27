# Mobile Manual Distribution — Handoff

## Commit

- Build hardening: `4c054129a27d4224d4b1b56d4574b20dca4b47e4`
  (`fix(mobile-build): 실빌드 복구 경로 강화`)
- Artifact source: `1861a96a8819841df68edeb53b51bf622fce174a`
  (`chore(mobile-build): 라이선스 복구 후 빌드 재예약`)
- artifact source는 rebase 전 이력이며 현재 branch HEAD와 같은 tree가 아니다.

## Implemented

- version `0.1.0`, build `1`의 Android APK와 iOS Ad Hoc IPA를 한 번의 `both` 실행으로 생성했다.
- APK의 package/version/build, `somnia-dev` signer와 ARM64 IL2CPP-only를 검증했다.
- IPA의 bundle/version/build/Team, strict codesign과 embedded Ad Hoc profile을 검증했다.
- 같은 commit 재시도용 명시적 `--attempt N` output stem을 추가했다.
- 현재 UID의 detached `Unity.Licensing.Client`를 Unity 실행 전에 탐지하되 자동 종료하지 않는다.
- 프로젝트별 ignored lock으로 동시에 두 mobile build가 설정을 다루지 못하게 했다.
- Unity snapshot 복원 뒤 `AssetDatabase.SaveAssets()`로 복원 상태를 확정한다.
- 알려진 tracked 직렬화 no-op만 whole-file exact match 후 atomic restore한다.
- HEAD/index, unrelated diff, symlink와 지연된 예상 밖 쓰기는 보존하고 실패한다.
- provenance를 판별할 수 없는 ignored Unity editor 설정은 자동 복원하지 않는다.

## Key Files

- `scripts/mobile/build.sh`
- `scripts/mobile/tests/build_sh_test.sh`
- `Assets/_Project/Editor/MobileBuild/DreamSquadMobileBuildCli.cs`
- `Assets/_Project/Tests/EditMode/MobileBuild/DreamSquadMobileBuildCliTests.cs`
- `docs/spec/mobile-manual-distribution/README.md`
- `docs/spec/mobile-manual-distribution/6_signed_build_recovery_hardening.md`
- `docs/reference/lessons/02-dev-workflow-git-scene.md`

## Verified

- `bash -n scripts/mobile/build.sh`: pass
- `bash -n scripts/mobile/tests/build_sh_test.sh`: pass
- `scripts/mobile/tests/build_sh_test.sh`: pass
- `./scripts/mobile/build.sh --help`: pass
- Runtime, MobileBuild Editor와 EditMode test assemblies Roslyn compile: pass
- 격리 Unity `6000.4.3f1` MobileBuild EditMode: 60/60 pass
- Android SHA-256:
  `b6b4f9187e97a9d8b673363948c6180aa6b876324e2fb2934de76af951c0f60c`
- iOS SHA-256:
  `4210a4614ba19ddee0c0964f1daef8271d8894ccec64e8aed31578fa012cf4db`
- Firebase 업로드는 실행하지 않았다.

## Notes

- 산출물은 `Builds/Mobile/DreamSquad-Demo-0.1.0-1-1861a96a/`에 보존돼 있다.
- 첫 실빌드 뒤 keystore canonicalization, iPhone batching과 URP obsolete-field diff는
  수동 복원했다. hardening은 이 exact 세 그룹만 자동 복원한다.
- 위 APK/IPA는 hardening commit이나 현재 게임 코드의 재빌드 결과가 아니다.
- 비밀번호는 사용자 소유 Terminal의 hidden prompt에서만 입력한다.
- 실패 산출물과 기존 output stem은 삭제하거나 덮어쓰지 않는다.

## Follow-up

- hardening이 포함된 clean commit에서 `target both`를 새 stem으로 한 번 통합 검증한다.
- 종료 직후 `git status --porcelain`이 자동으로 비어 있는지 확인한다.
- Android clean/update install과 등록 iOS 기기 설치·공통 smoke를 수행한다.
- 필요할 때만 검증 산출물을 Firebase `dreamsquad-demo` 그룹에 수동 업로드한다.
