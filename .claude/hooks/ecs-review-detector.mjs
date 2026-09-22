#!/usr/bin/env node
/**
 * ECS Review Detector — project-level UserPromptSubmit hook (wassup)
 *
 * When a review is requested AND ECS battle simulation files have changed,
 * injects additionalContext to trigger the two-track-review skill.
 *
 * Conditions (both must be true):
 *   1. User message contains a review keyword
 *   2. git diff shows ECS-related files changed
 */

import { execSync } from 'child_process';

// Inline stdin reader — no external dependency (avoids breaking when OMC's
// global helper path moves; this hook is project-owned).
function readStdin() {
  return new Promise((resolve) => {
    let buf = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (c) => (buf += c));
    process.stdin.on('end', () => resolve(buf));
    process.stdin.on('error', () => resolve(''));
  });
}

// ── Review keyword detection ──────────────────────────────────────────────
// Note: \b word boundary doesn't work with Korean (non-ASCII) chars — use plain alternation
const REVIEW_RE = /(리뷰|검토|투트랙|\breview\b|\bcode[\s-]?review\b|\btwo[\s-]?track\b|\bdual[\s-]?review\b)/i;

// ── ECS file patterns ─────────────────────────────────────────────────────
const ECS_PATH_FRAGMENTS = [
  'Assets/_Project/Scripts/Battle/',
  'BattleBridge.cs',
];

// ── 전투 코어(battle-core-rebuild) file patterns ──────────────────────────
// unit 0 항목 5 — 새 코어 경로는 ecs-reviewer 가 아니라 core-reviewer 로 간다.
const CORE_PATH_FRAGMENTS = [
  'Assets/_Project/Scripts/BattleCore/',
  'Assets/_Project/Scenes/BattleCoreScene.unity',
];

// ── Helpers ───────────────────────────────────────────────────────────────

function extractPrompt(data) {
  if (data.prompt) return data.prompt;
  if (data.message?.content) return data.message.content;
  if (Array.isArray(data.parts)) {
    return data.parts.filter(p => p.type === 'text').map(p => p.text).join(' ');
  }
  return '';
}

function sanitize(text) {
  return text
    .replace(/```[\s\S]*?```/g, '')
    .replace(/`[^`]+`/g, '')
    .replace(/https?:\/\/[^\s)>\]]+/g, '');
}

function getEcsChangedFiles(cwd) {
  const files = new Set();
  const run = (cmd) => {
    try {
      return execSync(cmd, { cwd, timeout: 4000, stdio: ['pipe', 'pipe', 'pipe'] })
        .toString().trim().split('\n').filter(Boolean);
    } catch { return []; }
  };

  // unstaged + staged + last commit (covers "review after commit" pattern)
  [...run('git diff --name-only'), ...run('git diff --name-only --cached'), ...run('git diff --name-only HEAD~1..HEAD')]
    .forEach(f => files.add(f));

  return [...files].filter(f => ECS_PATH_FRAGMENTS.some(p => f.includes(p)));
}

function getCoreChangedFiles(cwd) {
  const files = new Set();
  const run = (cmd) => {
    try {
      return execSync(cmd, { cwd, timeout: 4000, stdio: ['pipe', 'pipe', 'pipe'] })
        .toString().trim().split('\n').filter(Boolean);
    } catch { return []; }
  };
  [...run('git diff --name-only'), ...run('git diff --name-only --cached'), ...run('git diff --name-only HEAD~1..HEAD')]
    .forEach(f => files.add(f));
  return [...files].filter(f => CORE_PATH_FRAGMENTS.some(p => f.includes(p)));
}

function createCoreContext(coreFiles) {
  const fileList = coreFiles.map(f => `  - ${f}`).join('\n');
  return `<core-review-context>
[전투 코어 변경 감지됨]
다음 전투 코어(battle-core-rebuild) 파일이 변경되었습니다:
${fileList}

리뷰는 core-reviewer 에이전트로 진행하세요 (ecs-reviewer 아님 — 새 코어에는 ECS 제약이 적용되지 않는다):
  Agent: core-reviewer — CLAUDE.md 「새 전투 코어 — 절대 제약」 6항 · spec 계약 13 · 장부 정합
  병행: code-reviewer — spec 준수 · 일반 코드 품질
</core-review-context>`;
}

function createContext(ecsFiles) {
  const fileList = ecsFiles.map(f => `  - ${f}`).join('\n');
  return `<ecs-review-context>
[ECS 변경 감지됨]
다음 ECS 배틀 시뮬레이션 파일이 변경되었습니다:
${fileList}

두 가지 리뷰가 권장됩니다:
  Track A: code-reviewer  — spec 준수, lsp 타입 검사, 일반 코드 품질
  Track B: ecs-reviewer   — ECS 경계/컨텍스트, NativeQueue lifecycle, Burst 호환성

투트랙 리뷰를 진행하려면 two-track-review 스킬을 사용하세요:
  Skill: two-track-review
</ecs-review-context>`;
}

// ── Main ──────────────────────────────────────────────────────────────────

async function main() {
  try {
    const input = await readStdin();
    if (!input.trim()) {
      process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
      return;
    }

    let data = {};
    try { data = JSON.parse(input); } catch {}

    const prompt = extractPrompt(data);
    if (!prompt) {
      process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
      return;
    }

    // Condition 1: review keyword
    if (!REVIEW_RE.test(sanitize(prompt))) {
      process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
      return;
    }

    // Condition 2: ECS files and/or 전투 코어 files changed
    const cwd = data.cwd || data.directory || process.cwd();
    const ecsFiles = getEcsChangedFiles(cwd);
    const coreFiles = getCoreChangedFiles(cwd);
    if (ecsFiles.length === 0 && coreFiles.length === 0) {
      process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
      return;
    }

    // Conditions met — inject context (both blocks when both changed)
    const ctx = [
      ecsFiles.length ? createContext(ecsFiles) : '',
      coreFiles.length ? createCoreContext(coreFiles) : '',
    ].filter(Boolean).join('\n');
    process.stdout.write(JSON.stringify({
      continue: true,
      hookSpecificOutput: {
        hookEventName: 'UserPromptSubmit',
        additionalContext: ctx
      }
    }));
  } catch {
    process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
  }
}

main();
