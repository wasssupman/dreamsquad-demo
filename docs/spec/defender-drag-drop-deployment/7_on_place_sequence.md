# On-place Sequence

**작업 구분**: Phase 7

## 목적

배치 순간 고유 스킬을 deployment sequence 안에 편입한다.

## 순서

```text
Drop success
  -> TryBeginDefenderDeployment
  -> PlayDeploymentPresentation
  -> wait deploymentDuration
  -> wait placementSkillDelay
  -> TriggerDeploymentOnPlaceSkill
  -> remove PendingDeployment
  -> RecomputeSynergy
```

## 규칙

- on-place skill 은 entity 당 1회만 발동한다.
- on-place skill 은 배치 sequence 에 포함된다.
- 일반 combat 참여는 `PendingDeployment` 제거 이후다.

## 완료 기준

- deployment sequence 완료 전 combat 참여가 없다.
- deployment sequence 완료 후 combat 참여가 정상이다.
- on-place skill 이 중복 발동하지 않는다.
