# Legacy Transition Archive

> **Historical · non-normative · non-export · no maintenance**

이 폴더는 규칙·계획 중심 개편 이전의 조사, evidence 설계, registry, fixture와 foundation
pilot을 보존한다. Git history와 함께 과거 판단을 추적하기 위한 자료일 뿐 현재 Demo,
living transition 계약 또는 Production 구현의 정본이 아니다.

## 보존된 자료

- `architecture/`, `product/`, `evidence/`: 2026-07-29 기준 조사와 후보
- `migration-dossier/`: legacy Game Server 13-domain 준비 자료
- `demo-baseline.md`, `source-map.md`: 과거 Demo snapshot과 provenance map
- `governance/`: registry/review/decision/manifest와 foundation pilot
- `shared/`, `client/`, `game-server/`: unit-lifecycle card와 fixture

## 금지

- normal Demo 또는 transition maintenance의 기본 읽기 목록에 추가
- official freeze manifest나 consumer bundle에 포함
- archive의 stale 문구를 living 규칙·coverage·decision에 우선
- Demo 변경에 맞춰 이 자료를 갱신

필요한 stable 규칙은 `common/`, `client/`, `game-server/`의 living 문서로 새 ID와 함께
승격한다. 승격 뒤에도 이 archive 자체는 수정하지 않는다.

Archive 내부 문서와 상대 링크는 **이동 전 원래 위치를 기준으로 한 historical bytes**다.
Living 문서 link validation은 `archive/legacy/**`를 제외하며, 깨진 과거 링크를 고치기 위해
archive를 재작성하지 않는다.
