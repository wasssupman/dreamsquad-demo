# 3 — 레거시 `StunNearby` arm 철거

## 목적

unit 2 로 소비자가 0이 됐다. 죽은 분기를 남기면 다음 사람이 「배치 스턴은 두 곳에서 나온다」고
읽고, 그중 안 도는 쪽을 고친다. `on-place-skill-rework` 계약 2 만료 조건의 **첫 실제 수확**을
거둔다.

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `StunNearby` 분기 삭제
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — enum 멤버에 사장 표기
- `Assets/_Project/Scripts/Data/UnitKitSummary.cs` — `OnPlaceClause` 의 `StunNearby` 절 사장 표기
- `Assets/_Project/Tests/PlayMode/OnPlaceStunNearbyTest.cs` — 규칙 경로로 갱신
- `docs/spec/on-place-skill-rework/README.md` — 계약 2 만료 조건에 **부분 이행 한 줄** 추가
  (남은 arm 8개와 그때 쓸 어휘를 다음 사람이 바로 찾도록)

## 구현

### 지우는 것과 남기는 것

- **지운다**: `ApplyOnPlaceEffect` 의 `else if (onPlaceEffect == StunNearby) { ... }` 분기 전체
  (CC enqueue · `PlayKnockupHop` 직접 호출 포함).
- **남긴다**: `OnPlaceEffectType.StunNearby = 9` **멤버 자체.** 에셋이 int 로 직렬화하므로
  중간 값을 빼면 `DotNearby`(10)가 9로 밀려 **Busters 가 다른 스킬을 쓴다.** `SlowPulse` 가 이미
  같은 이유로 사장 멤버로 남아 있다 — 같은 표기(`// 사장 — {spec} 에서 규칙으로 이관`)를 쓴다.
- **남긴다**: `spineUnitPool.TryGet(...).PlayKnockupHop` 자체. 브리지의 넉업 드레인
  (`KnockupVisualEventsSingleton`)이 여전히 쓴다 — 평타 넉업과 unit 0 의 arm 이 둘 다 그 채널이다.

⚠ 전수 확인을 **삭제 직전에 다시 한다**: `Defender_*.asset` 에 `onPlaceEffect: 9` 가 0건.
다른 세션이나 시트 임포트가 값을 되살렸을 수 있다(시트는 `onPlace*` 를 안 실어 나르지만,
확인 비용이 0이다).

### 테스트 — 단언은 그대로, 경로만 바뀐다

`OnPlaceStunNearbyTest` 는 **삭제하지 않고 갱신**한다. 이 테스트의 단언은 「말파이트를 놓으면
반경 2 안 적이 3초 멈춘다」는 **증상**이고, 그 증상은 이관 전후로 같아야 한다. 이관이
성공했다는 증거가 바로 「경로를 바꿨는데 같은 단언이 초록」이다.

더할 것 하나: **피해 40 단언.** 배치 직후 반경 안 적의 체력이 40 줄고 반경 밖은 그대로다.
(테스트 더미 HP 가 100000 이라 그대로 쓰면 비율이 안 보인다 — 절대값으로 비교한다.)

## 완료 기준

- [ ] compile 0 error
- [ ] `grep -rn "StunNearby" Assets/_Project/Scripts` — 남은 것은 enum 멤버 · 사장 주석 ·
      문안 fallback 뿐(실행 분기 0)
- [ ] `grep -rn "onPlaceEffect: 9" Assets/_Project/Data/Defenders` — 0건
- [ ] PlayMode `OnPlaceStunNearbyTest` green — 반경 안 3초 정지 · 밖 무영향 · 해제 후 재이동
      **+ 피해 40**
- [ ] 기존 배치 스킬 PlayMode 3종(`DotNearby`·`ApplyStackNearby`·`ForwardProjectile`) 무회귀
- [ ] EditMode 전체 green (24초 lane)
