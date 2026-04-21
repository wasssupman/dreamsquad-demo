# Phase 11 이관 스펙 — 범위 미정

> Phase 10 (맵 시스템 재설계) 2026-04-21 종료 후 착수 대기. Phase 10 종료 시점에 `docs/residual-issues.md` 를 전부 drop 했으므로 이관된 잔여 이슈 없음. Phase 11 주제는 clean slate 에서 사용자가 결정한다.

---

## 1. 현재 상태

- Phase 10 완료: seed procedural 맵 생성 + branch/trunk/root 다중 spawn + forest 테마 + 브리핑 map settings UI + seed 로깅.
- Residual 체크리스트 전부 drop. 열린 기술 부채 없음.
- 개발 트리 dirty 한 feature (Phase 10 외부): defender drag/drop 배치, on-place 스킬, Spine/VFX 파이프라인, PixPlays 에셋 import, defender 밸런스 튜닝. 전부 Phase 10 과 독립된 트랙이며 각 feature 담당 커밋에서 처리 필요.

---

## 2. Phase 10 에서 유보된 항목 (Phase 11+ 후보)

기능 확장 후보 — 사용자 결정 시 개별 Phase 주제로 채택:

- **Env 타일 환경 효과** — Phase 10 에서 `MapTileType.Env` 는 시각 구분만. 실제 효과 동작 (속도 감소 / 데미지 / 투사체 막힘 등) 미구현.
- **맵툴 authoring UI** — `ManualMapInput` data shape 만 완료. 실제 에디터 UI + 저장/로드는 미착수.
- **Theme obstacle footprint 확장** — 현재 단일 셀. multi-cell obstacle / 다양한 shape mask 필요 시 Phase 주제로 채택.
- **Multi-goal 맵** — 현재 single goal. multi-goal 시 flow field 복수 계산 + `PathFollowState.targetGoal` 추가 필요.
- **Seed / generatorVersion QA 재현 플로우** — Battle log 의 `MapRecord` 는 이미 기록. 재현 도구 (로그 → 동일 맵 로드) 는 미착수.

---

## 3. 미해결 기술 부채 (Phase 10 에서 미드는 대신 덜어낸 것)

Phase 10 종료 시 drop 된 residual 항목 중, 필요 시 Phase 11+ 에서 재등재:

- `BattleBridge.SpawnMeteorWarningVisual` procedural Quad → prefab 화 (VFX 파이프라인 일관성).
- `GridMath.CellIndex` half-boundary 반올림 EditMode 테스트 보강.
- 방어 유닛 공격 범위 표시 UI (RangeOverlayController).
- Shader Graph 템플릿 (dissolve / glow).

---

## 4. 착수 전 의사결정 필요

1. Phase 11 주 테마 선정 — 위 §2 후보 중 택 1 또는 새 주제.
2. 검증 질문 정의 — Phase 11 이 통과해야 할 binary 판정.
3. 작업 분해 — Phase 11 이 확정되면 `docs/spec/{feature-slug}/` 에 분산 spec 작성.

**현재는 대기 상태**. 사용자가 Phase 11 주제 확정 후 프롬프트 지시 시 spec 폴더 착수.
