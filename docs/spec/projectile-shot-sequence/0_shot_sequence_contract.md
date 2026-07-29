# unit 0 — shot sequence 계약

## 목적

공용 projectile pattern의 균일한 `shotCount × shotIntervalSec`를 개별 step 목록으로
교체한다. 한 번의 trigger가 만든 N발은 각자 spread 내 방향값과 직전 탄 이후 interval을
가지며, 순수 스케줄러가 프레임 드리프트 없이 이를 소비한다.

이 unit은 데이터·순수 로직과 기존 타깃형 패턴의 호환 이관까지만 담당한다. 무타겟 방향
발사, defender 공격 연결, 실제 샷건너 수치와 화면 발사점은 후속 unit 범위다.

## 변경 대상

- `Assets/_Project/Scripts/Data/ProjectilePatternData.cs`
- `Assets/_Project/Scripts/Data/PatternSpec.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/{EmitterTick,ShotOrder}.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/PatternDirection.cs`
- `Assets/_Project/Scripts/Battle/Combat/Projectile/Emission/ProjectileEmitterSystem.cs`
- `Assets/_Project/Scripts/Battle/Combat/BossPeriodicTriggerSystem.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs`
- `Assets/_Project/Data/Projectiles/Pattern_Nightmare{Barrage,Missile}.asset`
- 관련 EditMode 테스트

## 구현

- authoring step은 `directionT(0..1)`와 `intervalAfterPreviousSec`를 가진다.
  패턴은 `minAngleDeg`, `maxAngleDeg`, step 배열을 소유한다.
- unmanaged `PatternSpec`은 `FixedList128Bytes<PatternShotSpec>`로 step을 스냅샷한다.
  현재 8-byte step 기준 최대 15발이며 빈 목록, 초과 목록, `min > max`는 bake에서 거절한다.
- `directionT`와 interval은 변환 seam에서 각각 `[0,1]`, `0 이상`으로 정규화한다.
  첫 step은 trigger 프레임에 발사되므로 첫 interval 값은 사용하지 않는다.
- `EmitterTick`은 다음 step의 개별 interval을 더한다. 느린 프레임에는 여러 발을 내보내고
  음수 잔여 시간을 이월해 누적 드리프트를 만들지 않는다. 0 interval 연속 step은 같은
  프레임에 모두 소비한다.
- `PatternLogic.BuildOrder`가 현재 step의 `directionT`를 `ShotOrder`에 복사한 뒤
  `shotIndex`와 영속 선택용 `fireCount`를 한 발씩 전진시킨다.
- `PatternDirection`은 `lerp(min,max,directionT)` 각도로 base direction을 회전하는
  architecture-neutral 순수 함수다. ECS 방향 binding은 다음 unit에서 이 결과를 소비한다.
- 기존 나이트메어 2개 패턴은 각도 0, step 1개로 이관해 동작을 보존한다.

## 완료 기준

- Unity 컴파일 오류가 없다.
- 순수 EditMode 테스트가 방향 양 끝·중앙·clamp와 가변 interval, 0 interval, 느린 프레임,
  누적 드리프트 없음, 발수 보존을 검증한다.
- bake 테스트가 정상 step 스냅샷과 빈 목록·15발 초과·역전된 각도 거절을 검증한다.
- 기존 projectile emitter 통합 테스트가 1-step 및 다발 타깃형 패턴에 대해 통과한다.
- ECS 리뷰에서 맥락 소유권, unmanaged/Burst 호환, 구조 변경, lifecycle 회귀가 없다.

> 사용자 확인: 2026-07-30 · 구현 커밋 `37764dd8`
