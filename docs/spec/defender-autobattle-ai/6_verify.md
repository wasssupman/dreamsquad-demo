# 6 · 검증

- EditMode 두 lane 초록(신규: `DefenderAiTests`·`DefenderAiStateSystemTests`·`EnemyAiTests`).
- 골든 코퍼스: **값 무변**, 트레이스 채널 22(`DefenderAiState`) 추가로만 갈림 → 재베이크 1회(남의 WIP 격리).
- MCP Play: 소환사(순찰병 생존 → Sustaining 루프 → 상실 원샷 → Ready) · 폭탄맨 · 스나이퍼(Engaging 1.0s) · 배치(Deploying) 상태 전이 로그.
- PlayMode: `PatrolDefenderPlayTest` · `ActionLockTest` · on-place 테스트.
- 사용자 Play 확인 → 커밋 해시 기록 · handoff `7_handoff_summary.md`.
