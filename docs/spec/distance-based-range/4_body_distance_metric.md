# 4 — 자 교체: 몸과 몸 사이 간격

## 목적

사거리를 **중심점이 아니라 몸 가장자리**에서 잰다. 이 spec 의 본체다.

```
gap = length(max(|대상중심 − 내중심| − 내반폭, 0)) − 대상몸반경
사거리 안  ⟺  gap ≤ 사거리
```

분기 없는 5연산이고 Burst 친화적이다. **원은 반폭 0 인 특수해**이므로 별도 축이 생기지 않는다.

## 변경 대상

| 파일 | 무엇 |
|---|---|
| `Combat/AttackReach.cs` | 술어 본체. `CellSlackTiles` 은퇴 |
| `Combat/TileAoe.cs` | 광역 멤버십 |
| `Skills/SkillMath.cs` + `Battle/Skills/EcsSkillContext.cs:437` | 도메인 사본 + metric 분기 |
| `Tests/EditMode/TestSkillContext.cs:167` | **페이크도 같은 분기를 복제 중** — 안 고치면 EditMode 초록/라이브 오작동 |
| `Combat/TargetPersistence.cs` | 획득/유지 임계 분리(히스테리시스) |
| `Bridge/BattleBridge.cs` | `CollectAlliesInRange` · `InTileRange` · `CollectShieldBreakTargets` |
| `Bridge/BattleBridge.cs:3303` | `sceneKnobs` 등재 |

## 구현

- **이관 공식을 먼저 돌린다** — README 「사거리 데이터 이관」 표. `N′ = N + 0.5 − 내반폭 − 대상몸반경`.
  흔한 짝은 `N′ = N`(무회귀), 저작된 몸을 가진 쪽만 자기 반경만큼 줄어든다.
  **이관 커밋과 튜닝 커밋을 분리한다.**
- **히스테리시스가 진동을 단독으로 진다.** 반폭은 경계의 *위치*를 옮길 뿐 *무디게* 하지 않는다.
  획득 `gap ≤ N`, 유지 `gap ≤ N + h`. 락·장판·오라 세 축 모두.
- **어설션**: `프레임당 최대 변위 ≤ 히스테리시스 폭`. 적 속도 상한이나 `tileSize` 가 바뀌면
  조용히 깨지는 독립 조건이라 기하에 맡기지 않는다.
- **`sceneKnobs` 등재 필수.** 안 하면 `configHash` 가 안 움직여 드리프트 판독기가 「조건 무변화」로
  거짓말한다(보너스 포탈에서 같은 사고).
- `RangeMetric.Chebyshev` arm 의 소비처가 0 이 되면 **같은 unit 에서 은퇴**(제약 8).
- `AttackReach` 주석의 「월드에서도 체비셰프로 잰다 — 유클리드면 대각이 조용히 좁아진다」를
  이 spec 의 판단으로 **교체**한다(대각 도달 N+0.707 로 계약 유지).

## 완료 기준

- [ ] `AttackReachTests.WorldGate_IsChebyshevToo_...` 를 포함한 4건이 **뒤집힌다 — 정상이다.**
      테스트 이름과 주석을 새 계약으로 갱신한다.
- [ ] `SkillMathParityTests` 를 **멤버십 술어와 metric 분기까지** 덮도록 확장. 지금은 반올림·거리값·
      동률 3개만 덮어 이 변경을 못 잡는다.
- [ ] 골든 7건 red — **정직한 red** 다(`configHash` 에 술어 코드가 없어 드리프트로 위장되지 않는다).
      재생성은 unit 6.
- [ ] unit 0 안전망·카나리아 초록. 교착 0.
- [ ] 고정 스텝 하네스 2회 실행 일치(`TileAoe` 가 정수 산술에서 float 비교로 내려오므로 확인).
