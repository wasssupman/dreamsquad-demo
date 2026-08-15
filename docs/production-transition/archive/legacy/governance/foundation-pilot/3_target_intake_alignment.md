# C1/S1 — Production 준비 문서 정합

> **DORMANT · OWNER-GATED · NON-ACTIONABLE HISTORY.** 현재 Demo의 spec·작업 큐·검증 gate가 아니며 Project owner의 명시적 transition 활성화 전에는 외부 저장소 작업을 실행·제안하지 않는다.

## 목적

Client와 Game Server의 기존 조사 자료를 지우지 않고 전역 Demo 계약 아래의
non-authoritative preparation으로 정렬한다.

## 변경 대상

- `C:/Work/somnia-client/docs/demo-migration/`의 기존 미추적 7개
- `C:/Work/somnia-game-server/README.md`
- `C:/Work/somnia-game-server/docs/plans/`의 active roadmap와 migration 준비 계획
- Server의 active baseline/contract/register 문서

## 구현

- Client의 `accepted`와 continuous capsule adoption을 `preparatory`와 future one-time
  intake로 바꾼다. shared 의미와 Client projection/presentation 책임을 분리한다.
- Server의 re-freeze를 제거하고 `DEMO-SNAP-001`, `SIM-CONTRACT-SNAP-001`을 historical
  provisional seed로 고정한다.
- 양쪽 모두 official `docs/migration-input/.../<freeze-id>/`를 만들지 않는다.
- 완료된 Server Phase 1 실행 계획, runtime, API, asset, package와 project 설정은
  수정하지 않는다.

## 완료 기준

- [x] 두 target 저장소가 중간 Demo package를 intake하지 않는다.
- [x] 기존 자료가 production gameplay/protocol 승인처럼 읽히지 않는다.
- [x] Client 사용자 소유 미추적 파일을 삭제·clean·일괄 대체하지 않는다.
- [x] 별도 사용자 승인 뒤 각 target commit만 생성하고 push는 수행하지 않는다.

검증 기록(2026-08-11): Client `372ce99d47c9475b9d660932cd7e76f300095feb`,
Game Server `e2aa25f1295f16d917fc9202d661190da3c94be0`; 양쪽 worktree clean, target push 없음.
