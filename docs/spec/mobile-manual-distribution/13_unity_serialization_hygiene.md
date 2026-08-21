# 13 — Unity 6.4 Spine 직렬화 위생

## 목적

Unity 6.4 iOS export가 성공한 뒤에도 Spine 구형 에셋을 자동 직렬화해 build wrapper가
Xcode archive 전에 중단되는 회귀를 막는다. 2026-08-20 `0.1.0 (13)` iOS 시도에서
`m_LockedProperties` 공백 3건, `serializedMaterialOverrides: []` 2건, 생성 material과
meta 2건, Mobile RP의 이전 필드 제거 1건이 관측됐다.

## 변경 대상

- `scripts/mobile/build.sh`
- `scripts/mobile/tests/build_sh_test.sh`
- `docs/spec/mobile-manual-distribution/README.md`
- `docs/spec/mobile-manual-distribution/13_unity_serialization_hygiene.md`

## 구현

- 빌드 시작 시 clean baseline에서 위 **정확한 Spine 경로**의 baseline과 예상 no-op variant를
  만든다. material의 공백 차이와 atlas의 빈 override 배열은 내용 전체가 variant와 일치할 때만
  허용한다.
- Unity가 만든 정확한 untracked material·meta 두 파일은 시작 시 부재, regular file, 그리고
  허용 tracked variant 묶음과 동시 존재가 모두 성립할 때만 삭제한다. symlink, 추가 파일,
  부분 변경, HEAD/index 변동은 기존처럼 실패한다.
- 복원은 lock 안의 임시 파일을 이용해 원자적으로 수행하고, 모든 원본 hash와 `git status`가
  clean임을 다시 확인한다.
- Shell fixture는 정상 8건 묶음 복원, extra 변경 거부, symlink 거부, 예상 untracked 파일만
  삭제하는지 고정한다.

## 완료 기준

- [ ] `bash -n scripts/mobile/build.sh`와 `scripts/mobile/tests/build_sh_test.sh`가 통과한다.
- [ ] 관측된 Unity 6.4 Spine no-op 묶음은 byte-for-byte 원복되어 clean worktree가 된다.
- [ ] 같은 묶음에 임의 tracked/untracked 변경 또는 symlink가 있으면 원복하지 않고 실패한다.
- [ ] 후속 iOS build가 Xcode archive/export와 IPA 검증까지 진행한다.
