# 4. Validation

## 목적

hazard caster defender 가 runtime, visual, lifecycle 관점에서 안전하게 동작하는지 확인한다.

## 변경 대상

- Add/Modify: `Assets/_Project/Tests/EditMode/*HazardCaster*Tests.cs`
- Use: Unity PlayMode smoke via BattleScene
- Use: Unity console

## 구현

EditMode 테스트:

- `HazardCastSystem` 이 범위 안 공격 유닛 cell 로 request 를 생성한다.
- 범위 밖 공격 유닛은 무시한다.
- cooldown 중복 cast 를 막는다.
- target 이 request 후 파괴되어도 target cell snapshot 으로 request 가 유지된다.
- caster 가 request 후 파괴되면 BattleBridge drain 이 drop 한다.
- `width/height` 는 MVP 에서 1로 고정된다.
- Zone/Blocking request kind 와 dataIndex 가 구분된다.

PlayMode smoke:

- Fire caster defender 가 공격 유닛 위치에 fire hazard 를 생성한다.
- Ice caster defender 가 공격 유닛 위치에 ice hazard 를 생성한다.
- Poison caster defender 가 공격 유닛 위치에 poison hazard 를 생성한다.
- Blocking caster defender 가 공격 유닛 위치 또는 유효 cell 에 blocking hazard 를 생성한다.
- 각 caster 는 정해진 cooldown 으로 반복 cast 한다.
- Zone hazard 는 기존 `ZoneApplySystem` 을 통해 공격 유닛에게 적용된다.
- hazard visual 이 생성되고 lifetime 후 정리된다.

콘솔 기준:

- compile error 0
- `CcApplySystem` / `ModifierApplySystem` destroyed entity 예외 0
- `GridMath.ChebyshevDistance(int2, int2)` Burst 오류 0. `HazardCastSystem` 은 동일 오류를 재발시키지 않는다.
- NativeQueue dispose/leak 경고 0
- Spine missing skin/animation warning 0

## 완료 기준

- 관련 EditMode 테스트 통과
- PlayMode smoke 에서 4종 caster defender 동작 확인
- Unity console error 0
- 구현 후 `5_handoff_summary.md` 작성
