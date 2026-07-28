# Unit 12 — 초반 웨이브 등장 게이트 (`minWaveNumber`)

## 목적

특정 적 유형이 너무 이른 웨이브에 나오지 않게 한다. 첫 요구는 **"첫 웨이브에 Runner 금지"** —
플레이어가 배치도 못 끝낸 시점에 고속 돌파형이 들어와 판이 시작부터 무너지는 것을 막는다.

규칙은 유닛의 성질이므로 **코드가 아니라 유닛 SO 가 소유**한다(제약 6: 하드코딩 금지).
"Runner" 라는 이름은 코드 어디에도 등장하지 않는다.

## 변경 대상

- `Assets/_Project/Scripts/Data/AttackUnitData.cs` — `minWaveNumber` 필드 신설
- `Assets/_Project/Scripts/Data/WavePatternGenerator.cs` — `ResolveWaveEligibleIndex` + 적용 2곳
- `Assets/_Project/Data/Enemies/Enemy_Runner.asset` — `minWaveNumber = 2`
- `Assets/_Project/Tests/EditMode/WaveEligibilityGateTests.cs` — 신규

## 구현

**데이터**: `AttackUnitData.minWaveNumber` (`[Min(1)]`, 기본 1 = 제한 없음). "이 유닛이 등장할 수
있는 가장 이른 웨이브(1부터)". Runner 만 2.

**적용**: `WavePatternGenerator.Generate` 가 웨이브 i 의 유형 2종을 뽑은 **뒤**, 인덱스를 순수 함수로
보정한다.

```
aIndex = ResolveWaveEligibleIndex(pool, aIndex, waveNumber);
bIndex = ResolveWaveEligibleIndex(pool, bIndex, waveNumber, excludeIndex: aIndex);
```

`ResolveWaveEligibleIndex(pool, startIndex, waveNumber, excludeIndex = -1)` 는 `pool[startIndex]` 가
그 웨이브에 등장 불가면 pool 순서로 **다음 허용 유닛까지 순환**해 인덱스를 돌려준다. 보스 웨이브의
호위 유형 선택에도 같은 함수를 통과시킨다.

## 계약

- **rng 소비 불변**: 게이트는 이미 뽑힌 인덱스를 사후 보정할 뿐 난수를 더 뽑지 않는다. 그래서
  게이트에 걸리지 않는 웨이브의 구성은 도입 전과 **byte-identical** 이다. 풀을 웨이브마다 필터링해
  `NextInt` 범위를 바꾸는 방식은 이 성질을 깨므로 금지.
- **"한 웨이브 = 2종" 유지**: 두 번째 그룹은 `excludeIndex` 로 첫 그룹을 건너뛴다.
- **fail-open**: 그 웨이브에 허용된 유닛이 하나도 없으면 원래 뽑힌 인덱스를 그대로 쓴다. 빈 웨이브나
  단일 그룹 웨이브를 만드는 것보다 게이트를 여는 쪽이 안전하다.
- **적용 범위는 seed 생성 경로뿐**. 작성 플랜(`WavePlanAsset` → `FromPlanAsset`)은 디자이너가 명시한
  배치이므로 게이트를 적용하지 않는다.
- 게이트는 **유닛당 1개 값**이다. "웨이브 N 이후 등장"만 표현하고 "웨이브 N 까지만"은 표현하지 않는다
  (상한이 필요해지면 그때 별도 필드).

## 완료 기준

- [x] EditMode 전체 통과 (1520 tests, 신규 6개 포함 — 게이트 준수 / 후반 등장 / 2종 유지 /
      미게이트 풀 결과 불변 / 순수 함수 경계 / fail-open)
- [x] 실제 덱 7종(Serpent·Coil·Twin·Spiral·Zig·Hook·Endless) 라이브 검증: 첫 웨이브 Runner 0,
      wave 2+ 구성은 도입 전과 100% 동일
- [x] 도입 전 첫 웨이브에 Runner 가 있던 덱 3종(Serpent·Coil·Twin)이 실제로 교체됨
- [ ] 사용자 Play 체감 확인 (첫 웨이브 난이도)
