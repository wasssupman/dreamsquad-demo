# Unit 2 — 파이프라인 스모크 (예제 스켈레톤 임시 wiring)

> 권장이지만 스킵 가능. 신규 리소스가 곧바로 수급되면 unit 4 로 직행해도 된다. 다만 이 unit 을 거치면 "런타임 문제 vs 리소스 문제" 를 분리해서 판정할 수 있다.

## 목적

4.2 예제 스켈레톤(Spine Examples 의 spineboy 등)을 Defender 1종 + Enemy 1종에 임시 연결해, 신규 리소스 도착 전에 SpineUnitView 파이프라인 전체가 4.2 런타임에서 성립함을 Play 로 검증한다.

## 변경 대상

- 수정(임시): `Assets/_Project/Data/Defenders/Defender_Scout.asset` — `skeletonDataAsset` + 애니메이션 이름 필드(idle/attack/drag)를 예제 스켈레톤 기준으로
- 수정(임시): `Assets/_Project/Data/Enemies/Enemy_Vanguard.asset` — 동일
- 코드 수정 없음 (수정이 필요해지면 그 자체가 발견사항 — 원인 기록)

## 구현

1. Spine Examples 에서 애니메이션 구성이 단순한 스켈레톤 선택 (idle/walk/attack 유사 클립 보유 기준).
2. 두 데이터 에셋에 임시 wiring. 애니메이션 이름이 프로젝트 관례와 다르면 데이터 필드로만 흡수 (`ResolveAnimation` 의 후보 fallback 활용).
3. Play 검증 시나리오:
   - Defender 드래그 배치 → 드래그 프리뷰(SkeletonAnimation AddComponent 경로) 표시
   - 배치 후 idle 재생, 공격 시 attack→idle 체인
   - Enemy 스폰 → 이동 → FaceToward 좌우 반전(ScaleX 부호) → 사망 페이드(`Skeleton.A`)
   - `SkeletonFlipX.asset` 을 예제 SkeletonData 의 modifiers 에 붙였다 떼며 반전 동작 확인
4. URP 렌더 확인: 씬/게임 뷰에서 마젠타/미표시 없이 렌더되는지 (Spine/Skeleton unlit 셰이더 경로).
5. 발견사항을 이 문서 하단에 기록 (4.2 에서 달라진 임포트 동작, 셰이더, 애니 mix 등).

## 완료 기준

- [ ] 위 Play 시나리오 전부 통과, 콘솔 에러 0
- [ ] URP 에서 스켈레톤 정상 렌더 확인
- [ ] 임시 wiring 이라는 사실이 커밋 메시지에 명시됨 (unit 4 에서 원복 예정)
