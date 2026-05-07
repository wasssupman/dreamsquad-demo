#!/usr/bin/env node

import { execFileSync } from 'node:child_process';
import { existsSync, readFileSync, unlinkSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const MAX_BLOCKS = 1;

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

function ok(extra = {}) {
  writeJson({ continue: true, suppressOutput: true, ...extra });
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

function markerPath(root, turnId) {
  return join(root, '.codex', 'tmp', 'two-track-review', `${turnId}.json`);
}

function readMarker(path) {
  try {
    return JSON.parse(readFileSync(path, 'utf8'));
  } catch {
    return null;
  }
}

function writeMarker(path, marker) {
  writeFileSync(path, `${JSON.stringify(marker, null, 2)}\n`, 'utf8');
}

function hasTwoTrackResult(message, ecsFiles) {
  const text = String(message || '');
  if (!text.trim()) return false;

  const hasTrackA = /\bTrack\s*A\b|common\s+review|일반\s*리뷰/i.test(text);
  const hasTrackB = /\bTrack\s*B\b|\$?ecs-reviewer|ECS\s+Reviewer/i.test(text);
  const hasVerdict = /Final\s+Verdict|최종\s*판정|APPROVE|BLOCK|REQUEST\s+CHANGES|needs-attention/i.test(text);
  const mentionsEcsScope =
    /ECS\s*(변경|files?|scope)|ECS\s+변경\s*파일/i.test(text) ||
    ecsFiles.some((file) => text.includes(file));

  return hasTrackA && hasTrackB && hasVerdict && mentionsEcsScope;
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

  if (!input.turn_id) {
    ok();
    return;
  }

  const root = gitRoot(input.cwd || process.cwd());
  const path = markerPath(root, input.turn_id);
  if (!existsSync(path)) {
    ok();
    return;
  }

  const marker = readMarker(path);
  if (!marker) {
    ok();
    return;
  }

  if (hasTwoTrackResult(input.last_assistant_message, marker.ecs_files || [])) {
    try {
      unlinkSync(path);
    } catch {}
    ok();
    return;
  }

  const blockCount = Number(marker.block_count || 0);
  if (input.stop_hook_active || blockCount >= MAX_BLOCKS) {
    ok({
      systemMessage:
        'ECS two-track review marker is still present, but the Stop hook already continued once. Leaving the turn unblocked to avoid a loop.',
    });
    return;
  }

  marker.block_count = blockCount + 1;
  marker.last_blocked_at = new Date().toISOString();
  writeMarker(path, marker);

  writeJson({
    decision: 'block',
    reason:
      'ECS 변경 리뷰 요청이었지만 Track A common review, Track B $ecs-reviewer, ECS 변경 범위, 최종 판정 중 일부가 누락되었습니다. docs/reference/codex-review-guide.md에 따라 두 트랙을 모두 수행하고 더 엄격한 쪽을 최종 판정으로 출력하세요.',
  });
}

try {
  main();
} catch {
  ok();
}
