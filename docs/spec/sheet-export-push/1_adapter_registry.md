# 1. Adapter 등록 테이블 — (유닛 2 로 흡수됨)

## 결론

**별도 `SheetTabRegistry` 는 만들지 않는다.** 구현 착수 시점(2026-07-22)에 판단: registry(keyKind enum + collector delegate)의 소비자는 payload 빌더 하나뿐이라 제약 8("나중을 위한 추상 레이어 금지")에 걸리는 과잉 추상화다.

이유:
- **키(id / (cardId,slot))는 서버(Apps Script) 관심사** — Unity 클라이언트는 행만 보내고 upsert 는 서버가 한다. 클라이언트 registry 의 keyKind 는 소비처 없는 dead data.
- **탭명은 창(유닛 4)에서 흘러들어온다** — import 와 같은 탭을 겨냥하려면 `_defenderSheet`/`_enemySheet`/`_dcSheets`(EditorPrefs)를 그대로 push 에도 쓴다. 하드코딩 상수가 아니다.
- **병합은 탭명 키 dict 조립** — 위치기반(`r[0..5]`)이 아니라 이름 기반이라, import 쪽 "탭 배선 매핑 테이블화" 백로그가 겨냥한 fragility 가 push 조립에는 없다.

따라서 유닛 2 의 payload 빌더가 탭명을 파라미터로 받아 탭명 키로 직접 병합한다. registry 타입 불필요.

## 완료 기준

- [x] registry 미생성 결정 기록(2026-07-22). 기능은 유닛 2 가 커버.
