# 첫 실행 튜토리얼 10웨이브 잡몹 케이던스

## 목적

첫 실행 전용 60초 경기를 10개 웨이브로 채워, 온보딩 안내가 끝난 뒤에도 남은 시간 동안
짧은 간격으로 전투가 이어지게 한다. 엘리트와 보스는 배제하고 기본 잡몹만 등장시킨다.

## 변경 대상

- `Assets/_Project/Scripts/Data/WavePlans/WavePlan_FirstRunTutorial.asset`
- `Assets/_Project/Tests/EditModeAssets/FirstRunTutorialWavePlanTests.cs`
- `docs/spec/first-run-tutorial/README.md`

## 구현

- 타이머는 기존과 동일하게 60초를 유지한다.
- 1웨이브는 온보딩 행동 예산과 산탄 연출을 위해 15초·Basic 10·lane 0 고정을 유지한다.
- 2웨이브는 첫 스폰 16초 계약을 유지한다.
- 남은 45초는 5초짜리 웨이브 9개로 나눈다.
- 모든 그룹은 `EnemyTier.Normal`인 `Enemy_Basic`만 사용한다.
- 마지막 웨이브의 마지막 적은 58.5초에 스폰되어 제한시간 안에 들어온다.

## 완료 기준

- 플랜의 제한시간이 60초이고 웨이브가 정확히 10개다.
- 웨이브 구간 합이 60초다.
- 모든 적 그룹이 `EnemyTier.Normal`이며 Basic만 참조한다.
- 모든 그룹의 마지막 스폰이 해당 웨이브 구간 안에 있다.
- 첫 웨이브의 Basic 10·lane 0 계약이 유지된다.
