#!/usr/bin/env node
/**
 * Core Review Detector — project-level UserPromptSubmit hook (wassup)
 *
 * When a review is requested AND 전투 코어 files have changed,
 * injects additionalContext pointing at the core-reviewer agent.
 * (옛 ECS 분기는 battle-core-rebuild unit 9 에서 옛 전투와 함께 제거 — 구 ecs-review-detector.mjs.)
 *
 * Conditions (both must be true):
 *   1. User message contains a review keyword
 *   2. git diff shows 전투 코어 files changed
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

// ── 전투 코어(battle-core-rebuild) file patterns ──────────────────────────
const CORE_PATH_FRAGMENTS = [
  'Assets/_Project/Runtime/Battle/Scripts/BattleCore/',
  'Assets/_Project/Runtime/Battle/Scripts/BattleCoreUnity/',
  'Assets/_Project/Editor/BattleCore/',
  'Assets/_Project/Tests/EditMode/BattleCore/',
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

리뷰는 core-reviewer 에이전트로 진행하세요:
  Agent: core-reviewer — CLAUDE.md 「제약」 · battle-core-architecture.md §8 불변식
</core-review-context>`;
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

    // Condition 2: 전투 코어 files changed
    const cwd = data.cwd || data.directory || process.cwd();
    const coreFiles = getCoreChangedFiles(cwd);
    if (coreFiles.length === 0) {
      process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
      return;
    }

    // Conditions met — inject context
    const ctx = createCoreContext(coreFiles);
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
