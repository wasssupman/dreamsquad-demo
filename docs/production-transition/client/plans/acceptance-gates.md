# Production Client Acceptance Gates

## Intake gate

- Client receipt의 manifest/common/client hash와 official audit copy가 일치한다.
- `common`과 `client`만 소비하며 Game Server 전용 문서를 implementation input으로 사용하지 않는다.
- Imported 규칙이 accepted ADR/compact docs를 override하지 않는다.

## Authority gate

- Client가 gameplay 결과를 계산·확정·제출하는 경로가 없다.
- Animation/VFX/UI/pool callback이 canonical state transition을 발생시키지 않는다.
- Stable IDs와 content catalog mapping이 runtime-local identity와 분리된다.

## Projection·input gate

- Duplicate는 state/cue를 중복 적용하지 않는다.
- Gap/unknown identity는 resync 전까지 추측 적용하지 않는다.
- 대표 intent마다 pending, accepted, rejected와 corrected 상태가 구분된다.
- Snapshot/reconnect 뒤 final projection이 Server state에 수렴한다.

## Experience·release gate

- `demo-experience-map.md`의 모든 included row가 acceptance scenario를 가진다.
- Missing asset/version mismatch는 진단 가능한 fallback이고 gameplay state를 바꾸지 않는다.
- Product가 정한 대표 장치·network condition에서 반응성·정정 이해·결과 신뢰 기준을 통과한다.
- Android/iOS, Addressables, performance와 release 검증은 Somnia Client validation matrix를 따른다.

Transition source의 fixture/evidence는 이 gate의 통과 증거가 아니다. 실제 결과는 Production에서 생성한다.
