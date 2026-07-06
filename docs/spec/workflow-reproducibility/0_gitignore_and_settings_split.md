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
3. **permission 공용분 승격** (재현성 핵심): `settings.local.json` allow 180개를 분류 —
   - **공용·안전분** → `settings.json` `permissions.allow` 승격: 프로젝트 반복 작업만(예: Unity MCP 도구 호출, 표준 빌드/테스트/`git status` 류). fresh clone 시 프롬프트 폭증 방지.
   - **개인·광범위분** → `settings.local.json` 유지: 무제한 `Bash(*)`, 홈 밖 경로, 실험적 허용 등.
   - 판정 기준: "다른 경로 클론에서도 이 프로젝트 작업에 반복 필요한가 + 안전한가" 예 → 승격.
4. **훅 경로**: 현행 `node "$PWD/.claude/hooks/ecs-review-detector.mjs"` 유지. (`$CLAUDE_PROJECT_DIR` 하드닝은 선택, 이 단위 범위 밖.)

## 완료 기준

- `git check-ignore .claude/settings.json` → 무시 안 됨. `git ls-files .claude/settings.json` 에 등장.
- `.claude/settings.local.json` 은 여전히 `git check-ignore` 로 무시됨(OMC enabledPlugins 개인 유지 확인).
- 클린 프로필/별도 클론에서 UserPromptSubmit 훅이 로드되고 ECS 변경 프롬프트 시 발화.
- **새 경로 클론**에서 승격된 permission 이 즉시 적용(반복 작업 프롬프트 없음).
- 내 로컬 세션에서 OMC·context7 여전히 활성(effective 설정 무손실).
