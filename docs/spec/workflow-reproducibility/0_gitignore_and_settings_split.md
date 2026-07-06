# 0 — `.gitignore` 좁히기 + `settings.json` 분할

## 목적

표준 Claude Code 컨벤션대로 `.claude/` 공유분을 정상 추적하고, 개인분만 무시한다. 지금은 `.gitignore:88` `.claude/*` 가 전부 무시하고 스킬만 `-f` 강제추가된 상태라, **훅 배선(`settings.json`)이 유실**되고, `settings.local.json` 의 **permission allow 180개**도 그 폴더에만 갇힌다. fresh clone(새 경로) 시 훅·permission 이 재현되게 이식한다.

## 변경 대상

- `.gitignore`
- `.claude/settings.json` (현재 gitignored → 공유 커밋본으로)
- `.claude/settings.local.json` (개인 유지, 이미 존재 ~13KB)

## 구현

1. **`.gitignore` 좁히기**: `.claude/*` 라인 제거 → 개인분만 명시적으로 무시.
   ```gitignore
   .claude/settings.local.json
   .claude/worktrees/
   CLAUDE.local.md
   ```
   이후 `agents/`·`skills/`·`hooks/`·`settings.json` 은 자동 추적(향후 `-f` 불필요).
2. **`settings.json` 분할** (thin 결정 반영):
   - 현재 내용 = `enabledPlugins`(context7·oh-my-claudecode@omc) + `hooks`(ecs-review-detector).
   - **공유 커밋본** `.claude/settings.json`: `hooks` 만 남긴다.
   - **개인** `.claude/settings.local.json`: `enabledPlugins` 를 이쪽으로 **병합**(기존 13KB 내용 보존, clobber 금지).
   - 효과: 내 로컬 effective 설정은 동일(project+local 병합). 팀원은 훅만 받고 OMC 는 강제 안 됨.
3. **permission 공용분 승격** (rev — as-built): `settings.local.json` allow 188개를 분류한 결과, 대부분이 **과거 세션의 일회성 명령**(특정 파일 sed/awk/mv/curl)이라 공유 가치가 없었다. 승격은 **안전한 read-only 상시 권한 5개만**: `git status/diff/log`·`grep`·`rg`. MCP 권한은 사용자 지시(범위 밖)로 제외, 쓰기/광범위/네트워크 권한은 커밋 allowlist 가 클론한 누구에게나 자동 적용되므로 의도적으로 개인 유지.
4. **훅 경로** (rev — as-built): `$PWD` → **`$CLAUDE_PROJECT_DIR`** 로 하드닝(공식 hooks 문서 지원 확인). 클론 경로와 무관하게 동작.

## 완료 기준

- `git check-ignore .claude/settings.json` → 무시 안 됨. `git ls-files .claude/settings.json` 에 등장.
- `.claude/settings.local.json` 은 여전히 `git check-ignore` 로 무시됨(OMC enabledPlugins 개인 유지 확인).
- 클린 프로필/별도 클론에서 UserPromptSubmit 훅이 로드되고 ECS 변경 프롬프트 시 발화.
- **새 경로 클론**에서 승격된 read-only 5종(git status/diff/log·grep·rg)이 프롬프트 없이 즉시 적용. (그 외 권한은 각자 승인해 `settings.local.json` 에 쌓는 것이 의도된 동작.)
- 내 로컬 세션에서 OMC·context7 여전히 활성(effective 설정 무손실).

확인 2026-07-06 — 커밋 `6658cc7`. JSON 유효·check-ignore·스킬 추적 유지·로컬 effective 설정 무손실 검증 완료. 클린 프로필 훅 발화는 fresh clone 검증(unit 3 완료 기준)에서 최종 확인.
