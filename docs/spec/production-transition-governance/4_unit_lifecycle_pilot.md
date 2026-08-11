# D4 — unit-lifecycle 파일럿

## 목적

`spawn → attack-started → damage → death → despawn` 한 조각으로 Server 권위 의미,
Shared semantic vocabulary, Client projection/cue가 실제로 분리·연결되는지 검증한다.

## 변경 대상

- `docs/production-transition/game-server/cards/unit-lifecycle.md`
- `docs/production-transition/shared/cards/unit-lifecycle.md`
- `docs/production-transition/client/cards/unit-lifecycle.md`
- `docs/production-transition/{shared,client,game-server}/fixtures/unit-lifecycle/`

## 구현

- Server card는 권위 상태 전이와 결과 의미만 소유한다.
- Shared card는 stable ID, semantic state/event/result와 ordering만 소유한다.
- Client card는 projection, pending 표시, cue와 correction/reconnect 연출만 소유한다.
- Shared에 Unity/ECS 타입, wire DTO, 인증 또는 transport 결정을 넣지 않는다.
- 실제 gameplay 의도가 승인되지 않은 항목은 추측하지 않고 blocking decision에 연결한다.
- fixture는 schema/hash/package 분리를 확인하기 위한 문서 데이터일 뿐 runtime replay가 아니다.

## 완료 기준

- [x] 세 card가 서로의 ID와 dependency를 명시한다.
- [x] preparation 검증이 통과한다.
- [x] owner review와 gameplay 결정이 없으면 cutover 검증이 의도대로 실패한다.
- [x] 코드, Unity asset과 production import가 생기지 않는다.
