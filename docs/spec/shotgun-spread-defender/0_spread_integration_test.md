# 0 — 동프레임 스프레드 통합 테스트

## 목적

스프레드 경로를 유닛 저작 **전에** 검증해 회귀 가드로 고정한다(엔진은 구현돼 있으나 실증 유닛 0).
`VolleyMath.SpreadDirection` 순수 수학은 `VolleyMathTests` 에 이미 pinned — 여기서 고정할 것은
**통합 구간**: 볼리 arm 이 interval 0 에서 전탄을 같은 프레임에 스폰하고, 각 탄이 부채꼴 방향을 받는가.

## 변경 대상

- `Assets/_Project/Tests/EditMode/DirectionalVolleyIntegrationTests.cs` (기존 하네스 확장)

## 구현

기존 `DirectionalVolleyIntegrationTests` 의 월드/유닛 셋업을 재사용해 케이스 추가:

1. **동프레임 전탄**: shotCount 5 · interval 0 · spread 90 으로 arm → 레인에 적 → 1틱 후 스폰된
   방향 투사체가 정확히 5개.
2. **부채꼴 방향**: 5탄의 방향이 baseDir 기준 −45°/−22.5°/0°/+22.5°/+45° (allclose). 균등 분배
   계약(`SpreadDirection` t = index/(n−1) − 0.5)이 통합 구간에서도 유지되는지.
3. **쿨다운 무연장**: interval 0 → `CooldownAfterVolley` 연장 0 — 다음 볼리 가능 시각이 쿨다운
   그대로인지.

케이스 1~2가 기존 하네스에 이미 있으면(버스트 케이스의 spread 변형) 중복 작성하지 않고 파라미터만
확장한다.

## 완료 기준

- [x] EditMode 신규 케이스 green + 기존 볼리/발사 테스트 무회귀 (`bfbc8387`).

> 이 5발 균등 분포 테스트는 초기 엔진 실증 이력이다. 현재 10발 불규칙 계약은
> `projectile-shot-sequence`의 `DirectionalVolleyIntegrationTests`가 검증한다.
