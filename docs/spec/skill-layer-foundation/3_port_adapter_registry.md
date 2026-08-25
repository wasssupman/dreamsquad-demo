# 3 — 포트 · 어댑터 · 레지스트리

## 목적

도메인과 아키텍처 사이의 **프로토콜**을 실물로 만든다. 이 unit 이 끝나면 `ISkill` 을 쓸 수 있고,
`TestSkillContext` 덕에 스킬 하나를 **ECS 월드 없이** 단위 테스트할 수 있다.

## 변경 대상

- 신설 `Assets/_Project/Scripts/Skills/Wassup.Skills.asmdef` — **Entities·Collections 미참조**
- 신설 `Skills/ISkill.cs` · `ISkillContext.cs` · `SkillIntent.cs` · `SkillParams.cs` · `SkillRegistry.cs`
- 신설 `Assets/_Project/Scripts/Battle/Skills/EcsSkillContext.cs` (어댑터 — Runtime 쪽)
- 신설 `Assets/_Project/Tests/EditMode/TestSkillContext.cs`
- `Assets/_Project/Scripts/Wassup.Runtime.asmdef` — `Wassup.Skills` 참조 추가(단방향)

## 구현

1. **asmdef 로 계약 1 을 컴파일 게이트화한다.** 현재 런타임은 `Wassup.Runtime` 단일 어셈블리다.
   `Wassup.Skills` 가 Entities/Collections 를 참조하지 않으면 도메인의 ECS 참조가
   **컴파일 에러**가 된다. Runtime → Skills 단방향이라 순환은 없다.
   `Unity.Mathematics` 는 Entities 없이 단독 참조 가능하다(계약 1 의 허용 예외).
2. **`ISkillContext` 를 unit 0 의 표 그대로 구현한다.** 질의 12 · 의도 14.
   임의로 늘리지 않는다 — 늘려야 하면 unit 0 표를 먼저 고친다.
3. **`EcsSkillContext` 는 `SystemAPI` 를 쓰지 않는다.** `SystemAPI` 는 시스템 타입 안에서만
   동작하는 source-generated API 라 **독립 어댑터 클래스에서 호출할 수 없다.**
   → 호스트 시스템이 `EntityManager` 와 `ComponentLookup` 들을 **주입**한다.
4. **어댑터의 쓰기는 소유 맥락 채널 enqueue/append 만**(계약 3). component 직접 쓰기와
   구조 변경은 소유 맥락 시스템이 한다. unit 0 이 「예외」로 판정한 항목만 예외로 둔다.
5. **후보 풀은 프레임 공유 lazy 캐시로.** fire 당 재구축하지 않는다.
   선례: `Battle/Combat/BossPeriodicTriggerSystem.cs:114~125`.
6. **레지스트리는 fail-closed.** 미등록 `skillId` 는 조용한 no-op 이 아니라 loud 거절이다.
   선례: `Core/Dreamcatcher/DcApplicability.cs:226~231` 의 `default → Unclassified`.
7. **`TestSkillContext`** — 딕셔너리 유닛 저장소 + 기존 순수 코어 재사용.
   무거운 질의는 이미 순수 함수가 있다(`DefenderDensity`·`BlinkMath`·`AuraPulse`·
   `AoeTargetCap`·`TileAoe.IsInCone`·`OnPlaceFireAim` — 전부 배열을 인자로 받는 static).
   그래서 페이크가 **sim 재구현이 되지 않는다.**
8. **저작 계층을 함께 연다.** `SkillDescriptor`(SO): 타입 필드 · 발동 조건 · Validate ·
   문안 · 부착 자격 선언. 문안 포매터 `UI/Dreamcatcher/DreamcatcherCardText.cs`(20 case)의
   일은 타입 필드가 생기면 **필드명 열람**으로 대체된다. 도메인으로 옮기면 계약 1 위반이다.
   반면 `DcApplicability`(25 case)는 case 내용이 UI 지식이 아니라 **스킬의 자기 서술**
   (대상 확정 필요 · 적 조준 필요 · 데미지 출력 필요 · 배타 상태)이라 `ISkill` 의
   **요구 플래그 선언**으로 이관 가능하다 — 이 unit 은 선언 축만 열고 판정기는 잔존시킨다.

## 완료 기준

- [ ] `Wassup.Skills` asmdef 가 Entities/Collections 를 참조하지 않는다 (asmdef 파일로 확인)
- [ ] 도메인 파일 전체 grep 에 `Entity`·`EntityManager`·`SystemAPI`·`DynamicBuffer`·
      `NativeQueue`·`IComponentData` **0건**
- [ ] `EcsSkillContext` 가 `SystemAPI` 를 호출하지 않고 주입된 lookup 으로 동작한다
- [ ] `TestSkillContext` 로 도는 단위 테스트가 1개 이상 초록
- [ ] 미등록 skillId 가 loud 하게 거절된다 (테스트로 고정)
- [ ] 컴파일 초록 · EditMode 코어 lane 초록. **라이브 동작 무변경**(아직 아무도 이 경로를 안 탄다)
