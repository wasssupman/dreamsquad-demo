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

    // Condition 2: ECS files changed
    const cwd = data.cwd || data.directory || process.cwd();
    const ecsFiles = getEcsChangedFiles(cwd);
    if (ecsFiles.length === 0) {
      process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
      return;
    }

    // Both conditions met — inject context
    process.stdout.write(JSON.stringify({
      continue: true,
      hookSpecificOutput: {
        hookEventName: 'UserPromptSubmit',
        additionalContext: createContext(ecsFiles)
      }
    }));
  } catch {
    process.stdout.write(JSON.stringify({ continue: true, suppressOutput: true }));
  }
}

main();
