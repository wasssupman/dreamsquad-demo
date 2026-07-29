# 10 — Squad 클래스 버프 부착 앵커 제한

## 목적

`ranger_atk`·`ranger_as` 같은 `CardType.Squad` 클래스 전군 버프가
`attachType=Class, attachValue=Ranger`를 선언했을 때 Ranger 유닛에만 부착되게 한다.
수혜 범위는 `axis=ClassRanger`대로 현재·미래 모든 Ranger이며, 부착 유닛은 수명 앵커다.

검증 질문: *Ranger 제한 Squad 카드는 Fighter에서 invalid/무차감 거절되고 Ranger에서는
부착되어 모든 Ranger에게 적용되는가?*

## 변경 대상

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs`
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
- `Assets/_Project/Editor/UnitStatImport/DcAttachRequirementValidator.cs`
- `Assets/_Project/Tests/EditMode/UnitStatImport/DcAttachRequirementValidatorTests.cs`
- `Assets/_Project/Tests/PlayMode/DreamcatcherAttachRequirementE2ETest.cs`
- `docs/spec/dreamcatcher-attach-requirement/README.md`

## 구현

1. `WouldDreamcatcherCardApply`의 defender-hosted 범위를 Unit/Squad로 넓힌다. 두 타입 모두
   live defender 확인 후 `PassesAttachRequirement`를 호출한다. Squad의 기여도 판정은 기존
   `DreamcatcherAttachEval.WouldApply`(`true`)를 유지한다.
2. 실제 커밋은 공용 `ApplyDreamcatcherCard(host, card)`에서 Squad 제한을 첫 쓰기 전에
   검사한다. 통과 후 기존 `ApplyDreamcatcherCardHosted(card)` 효과 머신을 호출하므로
   `_activeDcEffects`·`axis`·미래 배치 상속·host 사망 회수 계약은 바꾸지 않는다.
3. Unit/Squad 거절 로그를 한 helper로 모아 무효 값·host 조회 실패·클래스/id 불일치 문구를
   동일하게 유지한다. 거절은 `-1`이라 `CommitAttach`의 spend 전 반환을 그대로 탄다.
4. validator는 제한 있는 Squad를 정상으로 인정한다. Active와 BountyMark의 조용한 무효
   경고는 유지한다.

## 완료 기준

- [x] Unity compile 에러 0.
- [x] EditMode: 제한 있는 Squad는 validator 경고 0, Active/BountyMark 경고 유지.
- [x] PlayMode: `Squad + ClassRanger + attach Class/Ranger`가 Ranger에서
      `WouldDreamcatcherCardApply=true` 및 커밋 성공.
- [x] PlayMode: 같은 카드가 Fighter/비Ranger에서 UI invalid, 커밋 `-1`, 각성 무차감,
      카드 손패 잔류.
- [x] PlayMode: 성공 시 효과는 부착 host 한 기가 아니라 `axis=ClassRanger`에 따라
      현재 배치된 모든 Ranger에게 적용.
- [x] 기존 제한 없는 Squad와 Unit 제한 회귀 통과.
- [x] 사용자 Play: Ranger 카드 D&D/탭 시 Fighter 리티클 invalid·움찔, Ranger 부착 성공.

자동 검증 2026-07-30 — compile error 0 · 관련 EditMode 6/6 · 관련 PlayMode 2/2.
전체 EditMode 1574건 중 unrelated 기존 map dirty 실패 1건, 전체 PlayMode 70건 중
관련 2건 통과·기존 서버/상태 오염 실패 12건.

> 완료 확인 2026-07-30 — 사용자 Play 통과.
