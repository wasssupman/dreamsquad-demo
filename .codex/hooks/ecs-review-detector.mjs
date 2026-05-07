#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';

const REVIEW_RE = /(리뷰|검토|검수|봐줘|문제|괜찮|투트랙|\breview\b|\baudit\b|\bcheck\b|\binspect\b|\bcode[\s-]?review\b|\btwo[\s-]?track\b|\bdual[\s-]?review\b)/i;
const ECS_PATH_RES = [
  /^Assets\/_Project\/Scripts\/Battle\//,
  /^Assets\/_Project\/Scripts\/Bridge\/BattleBridge\.cs$/,
];

function readStdin() {
  try {
    return readFileSync(0, 'utf8');
  } catch {
    return '';
  }
}

function writeJson(value) {
  process.stdout.write(JSON.stringify(value));
}

function ok() {
  writeJson({ continue: true, suppressOutput: true });
}

function sanitize(text) {
  return String(text || '')
    .replace(/```[\s\S]*?```/g, '')
    .replace(/`[^`]+`/g, '')
    .replace(/https?:\/\/[^\s)>\]]+/g, '');
}

function gitRoot(cwd) {
  try {
    return execFileSync('git', ['rev-parse', '--show-toplevel'], {
      cwd,
      encoding: 'utf8',
      timeout: 2000,
      stdio: ['ignore', 'pipe', 'ignore'],
    }).trim();
  } catch {
    return cwd;
  }
}

function gitChangedFiles(root, args) {
  try {
    return execFileSync('git', args, {
      cwd: root,
      encoding: 'utf8',
      timeout: 4000,
      stdio: ['ignore', 'pipe', 'ignore'],
    })
      .split('\n')
      .map((line) => line.trim())
      .filter(Boolean);
  } catch {
    return [];
  }
}

function collectChangedFiles(root) {
  const files = new Set([
    ...gitChangedFiles(root, ['diff', '--name-only']),
    ...gitChangedFiles(root, ['diff', '--name-only', '--cached']),
  ]);
  return [...files].sort();
}

function isEcsFile(path) {
  return ECS_PATH_RES.some((re) => re.test(path));
}

function markerPath(root, turnId) {
  return join(root, '.codex', 'tmp', 'two-track-review', `${turnId}.json`);
}

function writeMarker(root, data) {
  if (!data.turn_id) return;
  const path = markerPath(root, data.turn_id);
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, `${JSON.stringify(data, null, 2)}\n`, 'utf8');
}

function createContext(ecsFiles) {
  const fileList = ecsFiles.map((file) => `- ${file}`).join('\n');
  return `<codex-two-track-review-context>
ECS battle simulation changes were detected for this review request.

Changed ECS files:
${fileList}

Apply docs/reference/codex-review-guide.md:

Track A: common Codex review stance
- Findings first, ordered by severity.
- Check compile/runtime failure paths, data/state corruption risk, Unity lifecycle/null-reference risks, spec/CLAUDE hard constraints, and missing tests.
- Do not replace this with the ECS review; Track A still applies to ECS files.

Track B: $ecs-reviewer
- Review Unity Hybrid ECS / Entities 6.4 risks.
- Focus on BattleBridge gateway violations, context ownership, NativeQueue/NativeContainer lifecycle, ECB structural changes, Burst/job compatibility, and system ordering.

Final verdict:
- If Track A or Track B needs attention, the final verdict is BLOCK / REQUEST CHANGES.
- If both approve, the final verdict is APPROVE.
- Include explicit Track A, Track B, and Final Verdict sections.
</codex-two-track-review-context>`;
}

function main() {
  const raw = readStdin();
  if (!raw.trim()) {
    ok();
    return;
  }

  let input;
  try {
    input = JSON.parse(raw);
  } catch {
    ok();
    return;
  }

  const prompt = sanitize(input.prompt || '');
  if (!REVIEW_RE.test(prompt)) {
    ok();
    return;
  }

  const root = gitRoot(input.cwd || process.cwd());
  const changedFiles = collectChangedFiles(root);
  const ecsFiles = changedFiles.filter(isEcsFile);
  if (ecsFiles.length === 0) {
    ok();
    return;
  }

  writeMarker(root, {
    turn_id: input.turn_id || null,
    session_id: input.session_id || null,
    created_at: new Date().toISOString(),
    ecs_files: ecsFiles,
    block_count: 0,
  });

  writeJson({
    continue: true,
    hookSpecificOutput: {
      hookEventName: 'UserPromptSubmit',
      additionalContext: createContext(ecsFiles),
    },
  });
}

try {
  main();
} catch {
  ok();
}
