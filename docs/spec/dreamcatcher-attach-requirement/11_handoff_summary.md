# 11 — Handoff Summary

## Commit

- `33be2453` — `fix(dreamcatcher): unit 10 — Squad 부착 앵커 제한`
- 기존 토대: unit 7 `12f5b644` · unit 8 `874a54ad`

## Implemented

- `attachType/attachValue` 적용 범위를 defender-hosted Unit과 Squad로 확장했다.
- `Squad + ClassRanger + Class/Ranger`는 Ranger에게만 부착된다.
- Squad의 실제 버프 수혜 집합은 계속 `axis`가 결정한다.
- 제한 Squad를 Fighter에 부착하면 UI preflight와 커밋이 모두 거절한다.
- 거절은 효과 등록과 spend 전에 `-1`을 반환해 각성치와 손패를 보존한다.
- 제한이 없는 Squad는 기존처럼 어떤 defender도 수명 앵커로 사용할 수 있다.
- Unit/Squad 커밋 거절 로그가 같은 원인 분류와 문구를 사용한다.
- 에디터 validator가 제한 있는 Squad를 정상 설정으로 인정한다.

## Key Files

- `Assets/_Project/Scripts/Bridge/BattleBridge.Dreamcatcher.cs`
  - 공용 dispatcher의 Squad host 검증과 attach requirement preflight
  - UI 판정 `WouldDreamcatcherCardApply`
- `Assets/_Project/Scripts/Core/Dreamcatcher/DreamcatcherAttachEval.cs`
  - 클래스·유닛 id 제한의 순수 판정
- `Assets/_Project/Editor/UnitStatImport/DcAttachRequirementValidator.cs`
  - Unit/Squad 저작 데이터 검증
- `Assets/_Project/Tests/PlayMode/DreamcatcherAttachRequirementE2ETest.cs`
  - UI 판정·커밋·무차감·전군 Ranger 수혜 회귀
- `10_squad_attach_requirement.md`
  - unit 10 계약과 완료 기준

## Verified

- Unity 6000.4.3f1 / Entities 6.4.0 compile error 0.
- 관련 EditMode 테스트 6/6 통과.
- 관련 PlayMode 테스트 2/2 통과.
- Fighter 거절 시 각성 무차감·카드 손패 잔류를 컨트롤러 경로로 확인했다.
- Ranger host 성공 시 현재 배치된 Ranger 2기 모두 공속 버프를 받음을 확인했다.
- 사용자 Play에서 탭/D&D 비대상 invalid와 Ranger 부착 성공을 확인했다.
- 전체 EditMode 1574건 중 공유 map dirty 영향 1건이 실패했다.
- 전체 PlayMode 70건 중 서버/상태 오염 12건이 실패했으며 관련 2건은 통과했다.

## Notes

- `attachType/attachValue`는 부착 앵커 제한이고 `axis`는 버프 수혜 범위다.
- Squad 효과 등록·미래 배치 상속·host 사망 회수는 기존 hosted 효과 머신을 유지한다.
- 프로덕션 손패 커밋은 반드시 `ApplyDreamcatcherCard(host, card)`를 사용한다.
- `ApplyDreamcatcherCardHosted(card)`는 host가 없는 저수준 효과 머신이라 제한을 판정하지 않는다.
- ECS 접근은 기존대로 `BattleBridge`에만 있으며 새 시스템·컴포넌트·이벤트 채널은 없다.
- 플레이 오브젝트 파이프라인 구조 변경은 없어 object pipeline map 갱신은 N/A다.

## Follow-up

- 복수 클래스 제한은 실제 카드 기획이 생길 때 별도 작업 단위로 다룬다.
- 제한 카드의 미래 Ranger 상속·host 사망 회수 전용 테스트는 필요 시 공통 lifecycle 테스트를 보강한다.
