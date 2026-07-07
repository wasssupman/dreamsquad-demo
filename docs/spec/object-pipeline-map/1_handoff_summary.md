# 1. Handoff Summary

## Commit

- `aeccbc3a` docs(reference): object-pipeline-map — 플레이 오브젝트 생성→렌더 정거장 체크표 + spec 커버리지 규칙

## Implemented

- `docs/reference/object-pipeline-map.md` — 아키타입 10종(방어 유닛/적/투사체/해저드 Zone·Blocking/스킬 해저드/힐/VFX/데미지 넘버/프랍·타일) 정거장 체크표. 조사 에이전트 3건 실측 기반.
- 함정을 확인 포인트로 명문화: 적 SO=`AttackUnitData`(EnemyData 없음) · 히트바=DamageNumber 큐 공유 · 타일 게이지=폴링 · VFX=혼합 트리거(공격 히트는 ProjectileViewPool 경유) · HazardRuntimeEvents=로깅 전용.
- CLAUDE.md 3곳: 구성 원칙에 `파이프라인 커버리지` 필수 섹션(N/A+이유 강제), 워크플로우 5번에 handoff 시 맵 구조 변경 확인, 참조 문서 표 진입점.
- artillery-defender 사후 대조 → 카탈로그 등록을 defender/enemy 데이터 SO 행 확인 포인트로 승격.

## Key Files

- `docs/reference/object-pipeline-map.md`
- `CLAUDE.md` (구성 원칙 / 기본 워크플로우 5 / 참조 문서 표)
- `docs/spec/object-pipeline-map/0_pipeline_map_and_rules.md`

## Verified

- `.cs` 앵커 57건 전부 실존 (36건 경로 직접 매치, 21건 축약 표기는 Scripts 트리 내 유일 매치). 코드/씬 변경 0, compile/test 해당 없음.

## Notes

- 맵은 **대조용 체크표**다 — 동작 설명·이벤트 필드·코드 흐름 산문을 추가하지 말 것. 구현 상세의 source of truth 는 코드.
- 갱신 트리거는 구조 변경만(새 아키타입/정거장, 앵커 이동·개명). 수치/필드/로직 변경은 갱신 대상 아님.
- 앵커는 `Assets/_Project/Scripts/` 기준 상대 경로. 같은 셀 내 축약 표기(파일명만)는 유일 매치 전제 — 동명 파일이 생기면 접두어를 붙일 것.

## Follow-up

- `docs/spec/README.md` Follow-up Backlog "파이프라인 커버리지 — 후속" 그룹 참조 (spec 파일 트리거 훅 · 리뷰 게이트).
