# 2 — AGENTS ↔ CLAUDE 단일화 (symlink)

## 목적

Codex(AGENTS.md) 와 Claude(CLAUDE.md) 가 **동일 정책**을 읽게 해, CLAUDE.md 절대제약/워크플로우가 Codex 로도 자동 전달되게 한다. 지금은 AGENTS.md 가 "CLAUDE.md 필독" soft 포인터 + stale 상태 스냅샷일 뿐이다.

## 변경 대상

- `AGENTS.md` (symlink 로 대체)
- CLAUDE.md (불변, 단일 소스)

## 구현

1. **stale 스냅샷 제거 확인**: AGENTS.md 의 "Current Spec Status" 는 stale-prone 이고 `catchup` 스킬이 git/spec 에서 상태를 재구성하므로 정책 문서에 둘 필요 없다. symlink 하면 자연 소멸(내용이 CLAUDE.md 로 대체됨).
2. **symlink 생성**: 레포 루트에서
   ```bash
   rm AGENTS.md && ln -s CLAUDE.md AGENTS.md
   ```
   git 은 심링크를 tracked blob(대상 경로 문자열)으로 저장 → drift 0, 단일 소스.
3. **주의**: repo-relative 심링크(`CLAUDE.md`) 사용(절대경로 금지). Windows 팀원은 심링크에 dev mode 필요 → 합류 시 `@import` 대안으로 전환(후속 후보). 현 팀 macOS 전제.

## 주의 (실행 중 발견)

- **AGENTS.md 는 deepinit 이 생성·관리**하는 파일이고, 그 내용이 OMC 세션-restore 첫 화면의 "Current Spec Status" 로 표시된다. symlink 후 그 블록은 CLAUDE.md 로 대체된다(원래 stale — attack-hit-delay 에 멈춰 있었음. 현재 상태는 `catchup` 스킬로 재구성).
- **deepinit 재실행 시 AGENTS.md 를 실제 파일로 재생성**하므로 symlink 이 풀린다 → 재실행했다면 `rm AGENTS.md && ln -s CLAUDE.md AGENTS.md` 재적용.
- 되돌리기: `git revert` 또는 `rm AGENTS.md && git checkout AGENTS.md` (실제 파일 복원).

## 완료 기준

- `readlink AGENTS.md` == `CLAUDE.md`. `cat AGENTS.md` 가 CLAUDE.md 전문 출력. ✅ 2026-07-06
- git 에 심링크 커밋(typechange), 클론 시 재현. ✅ 커밋 `6467f4b`
- Codex 세션 시작 시 CLAUDE.md 정책(절대제약·맥락분리) 로드 확인. ⏳ 다음 Codex 세션에서 라이브 확인 대기.
