# D1 — 전역 cutover 계약

## 목적

문서별 freeze나 저장소별 독립 migration을 없애고, 두 production 저장소로 fan-out하는
하나의 coordinated transition event를 정의한다.

## 변경 대상

- `docs/spec/production-transition-governance/`
- `docs/production-transition/README.md`
- `docs/production-transition/{shared,client,game-server,governance}/`

## 구현

- preparation 상태는 `preparing → cutover_candidate`까지만 반복 가능하다.
- 공식 publication이 bytes를 고정하는 순간 `cutover_in_progress`가 되며 이 사건이 유일한
  freeze다.
- 양쪽 import receipt가 동일 manifest/hash를 확인하면 `cutover_complete`가 된다.
- publication 전 오류는 candidate를 폐기하고 다시 준비한다. publication 뒤에는 동일 bytes
  import 재개만 허용하고 의미 오류는 production errata/ADR/change control로 처리한다.
- destination은 Client와 Game Server에 각각 고정하며 preparation 중에는 해당 경로를
  생성하지 않는다.

## 완료 기준

- [x] 전역 정본에 한 번의 freeze 의미와 상태 전이가 있다.
- [x] re-freeze, 부분 update, 지속 intake가 허용되지 않는다.
- [x] Client와 Server가 동일 freeze ID의 byte-identical Shared를 받는다.
- [x] runtime/API/asset/project 설정은 변경되지 않는다.
