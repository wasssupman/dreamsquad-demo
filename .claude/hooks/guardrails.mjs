#!/usr/bin/env node
/**
 * Guardrails — project-level PreToolUse hook (wassup)
 *
 * CLAUDE.md 의 산문 규칙 중 되돌리기 어려운 것만 기계로 막는다.
 *   1. git push 강제(--force · -f · --force-with-lease · +refspec) — 보호 브랜치 미러가 갈라진다
 *   2. git commit --amend — 여러 세션이 한 인덱스를 쓴다(남의 스테이징을 삼킨다)
 *   3. Unity MCP run_tests 의 PlayMode 는 `Wassup.Tests.PlayMode.Core` 만 —
 *      아웃게임 PlayMode 어셈블리는 [Explicit] 이 안 걸러져 실서버에 가입을 시도한다
 *   4. Unity MCP refresh_unity mode=force — 전 에셋 reimport + MCP 브리지 단절
 * push 승인 자체는 settings.json 의 permissions.ask 가 맡는다(이 훅은 거절만 한다).
 */

function readStdin() {
  return new Promise((resolve) => {
    let buf = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (c) => (buf += c));
    process.stdin.on('end', () => resolve(buf));
    process.stdin.on('error', () => resolve(''));
  });
}

function deny(reason) {
  process.stdout.write(JSON.stringify({
    hookSpecificOutput: {
      hookEventName: 'PreToolUse',
      permissionDecision: 'deny',
      permissionDecisionReason: `[guardrails] ${reason}`,
    },
  }));
  process.exit(0);
}

// 셸 명령을 단순 구분자(&& || ; | 줄바꿈)로 잘라 각 조각의 첫 토큰을 본다 — `echo git push -f` 는 걸리지 않는다.
function segments(command) {
  return command.split(/&&|\|\||;|\||\n/).map((s) => s.trim()).filter(Boolean);
}

function tokens(segment) {
  // 앞쪽 환경변수 할당(FOO=bar git …)과 `command`/`env` 접두는 건너뛴다.
  const t = segment.split(/\s+/);
  while (t.length && (/^[A-Za-z_][A-Za-z0-9_]*=/.test(t[0]) || t[0] === 'command' || t[0] === 'env')) t.shift();
  return t;
}

// git 의 전역 옵션(-C <dir> · -c k=v · --no-pager 등)을 건너뛰고 서브커맨드 이후 인자를 돌려준다.
function gitArgs(t) {
  if (t[0] !== 'git') return null;
  let i = 1;
  while (i < t.length && t[i].startsWith('-')) {
    if (t[i] === '-C' || t[i] === '-c') i += 2; else i += 1;
  }
  return { sub: t[i], rest: t.slice(i + 1) };
}

function checkBash(command) {
  for (const seg of segments(command)) {
    const g = gitArgs(tokens(seg));
    if (!g) continue;
    if (g.sub === 'push') {
      const forced = g.rest.some((a) =>
        a === '--force' || a === '-f' || a.startsWith('--force-with-lease') || a.startsWith('--force-if-includes') ||
        (/^-[a-zA-Z]+$/.test(a) && a.includes('f')) || (a.startsWith('+') && a.length > 1));
      if (forced) deny('강제 push 는 막혀 있다 — GitLab 보호 브랜치 미러가 갈라지고 되돌릴 수 없다. 필요하면 사용자가 직접 한다.');
    }
    if (g.sub === 'commit' && g.rest.some((a) => a === '--amend')) {
      deny('git commit --amend 는 막혀 있다 — 여러 세션이 한 인덱스를 써서 남의 스테이징을 삼킨다. 새 커밋을 만든다.');
    }
  }
}

function asList(v) {
  if (Array.isArray(v)) return v.map(String);
  if (typeof v === 'string' && v.trim()) {
    try { const p = JSON.parse(v); if (Array.isArray(p)) return p.map(String); } catch { /* not JSON */ }
    return v.split(',').map((s) => s.trim()).filter(Boolean);
  }
  return [];
}

function checkRunTests(input) {
  const mode = String(input.mode ?? input.testMode ?? 'EditMode').toLowerCase();
  if (mode !== 'playmode') return;
  const assemblies = asList(input.assembly_names ?? input.assemblyNames);
  if (assemblies.length === 1 && assemblies[0] === 'Wassup.Tests.PlayMode.Core') return;
  deny('PlayMode 테스트는 assembly_names=["Wassup.Tests.PlayMode.Core"] 로만 돌린다 — 아웃게임 PlayMode 어셈블리는 [Explicit] 이 안 걸러져 실서버에 가입을 시도한다(CLAUDE.md 「검증」).');
}

function checkRefresh(input) {
  if (String(input.mode ?? '').toLowerCase() === 'force') {
    deny('refresh_unity mode=force 는 막혀 있다 — 전 에셋 reimport 로 수 분 멈추고 MCP 브리지가 끊긴다. mode=if_dirty 를 쓴다(lessons/01).');
  }
}

async function main() {
  let data;
  try { data = JSON.parse(await readStdin() || '{}'); } catch { process.exit(0); }
  const tool = String(data.tool_name ?? '');
  const input = data.tool_input ?? {};
  if (tool === 'Bash' && typeof input.command === 'string') checkBash(input.command);
  else if (/__run_tests$/.test(tool)) checkRunTests(input);
  else if (/__refresh_unity$/.test(tool)) checkRefresh(input);
  process.exit(0);
}

main();
