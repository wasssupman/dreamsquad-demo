# Background Props Spec

**상태**: legacy
**메모**: 이 문서군은 2026-04-24 이전의 `background prop + terrain surface rule` 시도를 기록한다. 현재 방향은 이 문서군을 최종 기준으로 사용하지 않는다.

## 왜 legacy 인가

기존 문서와 구현은 다음 문제를 드러냈다.

- 배경 프랍 배치와 바닥 시각화가 서로 다른 규칙으로 발전했다.
- `Env/Deco` 를 자연스럽게 보이게 하려는 시도가 `continuous terrain top` 같은 렌더 실험으로 흐르면서, 그리드 기반 보드의 명확한 zone 표현과 충돌했다.
- 사용자가 원하는 것은 오픈월드형 절차 지형이 아니라, **정형화된 보드에서 `Walk`, `Place`, `Env` 가 명확하면서도 자연스럽게 보이는 시각화 시스템**이다.

## 이후 기준 문서

새 작업은 `docs/spec/board-visualization/` 문서군을 기준으로 진행한다.

- `board visual plan`
- `zone transition`
- `decor placement`
- `implementation review loop`

기존 문서 중 재사용 가능한 내용은 아래 정도로 한정한다.

- `PropData` / billboard prefab generator 계약
- footprint 기반 배치 기본 제약
- theme asset 경로 규칙 일부

나머지 지형 surface rule, continuous terrain, decal 실험은 참고 기록으로만 취급한다.
