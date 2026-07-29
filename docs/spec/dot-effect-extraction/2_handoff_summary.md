# 2 — handoff summary

## Commit

- `4ba9a76e` feat(dot): 지속 피해를 CC 버퍼에서 떼어내 자기 파이프라인으로 (unit 0)
- `5a982da7` docs(dot-effect-extraction): unit 0 라이브 실측 결과 기재
- `(unit 1)` feat(dot): 상태 오라를 도트 자신이 구동하게
- `(rename)` fix(status-fx): 오라 종류 이름을 실제 의미에 맞게 정정 + 누락 meta 회수
- `(2축)` refactor(dot): flavor 한 축을 origin·element 두 축으로 분리

## Implemented

- `DotEffect` 버퍼 신설 — 지속 피해가 `CcEffect`(행동 제약)에서 완전히 분리됐다
- `DotOrigin`(Stack·Zone·OnPlace) × `DotElement`(Bleed·Fire·Ice·Poison) **2축**, 병합 키는 둘 다
- `DotApplyEventsSingleton` 신설(26번째 채널). producer 3곳 이관 — 스택 임계·해저드 장판·배치 스킬
- 감쇠를 `CcDecaySystem` 에서 `DotApplySystem` 안으로 가져옴(수명 관리가 자기 파이프라인에)
- 오라를 `DotEffect.element` 로 구동 → bridge 래치 4종 + 전용 쿼리 **삭제**
- `StatusFxKind.FireStack/IceStack/PoisonStack` → `Fire/Ice/Poison` (값 유지, 에셋 무영향)
- `Hazard_{Fire,Poison}_{1x1,3x3}` 4개에 `element` 저작

## Key Files

- `Assets/_Project/Scripts/Battle/Effects/DotEffect.cs` — 2축 enum + 슬롯 + `DotElementMap`
- `Assets/_Project/Scripts/Battle/Effects/DotEffectMerge.cs` — **통합 레이어**. 중첩 정책은 여기만
- `Assets/_Project/Scripts/Battle/Effects/DotApplySystem.cs` — 부여 드레인 + 틱 + 감쇠
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 채널 배선 · 오라 reconcile · `DotAuraKind`
- `Assets/_Project/Tests/EditMode/DotEffectMergeTests.cs` · `PlayMode/DotAuraFromElementTest.cs`

## Verified

- EditMode 1569 통과 / PlayMode 55 통과·13 실패 = **HEAD 베이스라인과 동일**(별도 baseline run 으로 확정)
- 리그 실측: 출혈 1회분 10틱·총 50 유지. 화염 장판 위에서 **출혈 scalar 5.00 유지**(이관 전이라면
  10 으로 덮임), 장판 이탈 시 화염 슬롯만 자기 지속으로 소멸 → 과피해 ~194 및 "장판 밖에서 장판
  요율로 타는" 증상 해소

## Notes (되돌리지 말 것)

- **축 2개를 하나로 겸직시키지 말 것.** origin=슬롯 분리 기준, element=그림. 합치면 장판 화염과
  중첩 폭발 화염이 한 슬롯에서 서로를 덮어 과피해가 재현된다. 가드 =
  `SameElement_DifferentOrigin_GetsSeparateSlot`
- **`Stun`·`Sleep`·`Impulse` 는 건드리지 말 것.** 덮어쓰기가 올바른 사양이고, wake-on-hit 를
  source 별로 좁히면 호접몽 파탄 판정이 무력화된다
- **새 채널 싱글턴은 `DestroyEntitiesByType` 목록에 넣을 것.** 빠뜨리면 재생성 시 인스턴스가 2개가
  되어 PlayMode 가 통째로 죽는다(이번에 실제로 밟았다)
- **지급은 정방향 순회, 만료 제거만 역순 별도 패스.** 역순 지급은 데미지 숫자 순서를 뒤집는다
- **오라 점등 조건에 스택 슬롯을 다시 넣지 말 것.** 슬롯이 도트보다 먼저 죽어 후반부에 꺼진다
- `DotApplySystem` 의 두 job 은 **일부러 안 합쳤다** — 이유는 코드 주석 참조
- `CcKind.DoT` 는 해저드 저작 토큰으로만 잔존(`CcKind.Slow` 와 같은 형태). 런타임 `CcEffect` 로는
  더 이상 만들어지지 않는다

## Follow-up

- **Play 확인** — 출혈 오라만 뜨는지 / 화염 장판 위 화염 오라 / 얼음 오라 없음
- **`DotCoexistenceTest` 미작성** — 공존은 리그 프로브 실측 + EditMode 병합 테스트로 덮었지만
  end-to-end 회귀 가드는 없다
- 나머지는 README 후속 후보 참조
