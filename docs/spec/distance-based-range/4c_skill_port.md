# 4c — 스킬 포트와 parity 소멸

## 목적
스킬 레이어를 같은 자로 맞추고, **미러를 확장하는 대신 없앤다**(계약 8).

## 변경 대상
- `Skills/ISkillContext.cs:35` — `RangeMetric.Chebyshev` → **`BodyDistance` rename**(값 0 유지, 직렬화 호환)
- `Battle/Skills/EcsSkillContext.cs:437-447` — metric 분기 → **호출 1줄**
- `Tests/EditMode/TestSkillContext.cs:167` — **페이크도 같은 분기를 복제 중이다.** 안 고치면 EditMode 초록/라이브 오작동
- `Battle/Movement/GridMath.cs:42` — `ChebyshevDistance` 위임(`WorldToCell`/`FlowStep` 은 격자 소유라 유지)
- `Tests/EditMode/SkillMathParityTests.cs` — **축소**

## 구현
- 술어 본체는 4a 에서 `SkillMath` 에 섰다. 여기서는 **Runtime 쪽 사본을 위임으로 바꾼다.**
  `Wassup.Runtime.asmdef` references 첫 줄이 `Wassup.Skills` 라 방향이 이미 열려 있고,
  그 경로는 완주된 적이 있다(`TileAoe.IsInCone` → `SkillCone`).
- `RangeMetric` 은 **은퇴시키지 않는다.** 은퇴하면 남는 `Euclidean` arm 이 중심 대 중심 순수 원이라
  계약 1 과 다른 자가 되어 스킬 광역만 갈린다.
- `SkillMathParityTests` 는 **`SelectNearest` 항목만 남긴다.** 진짜로 영구인 이중화는 그것 하나뿐이다
  (`NativeArray` 시그니처 — 도메인이 Collections 를 못 참조).

## 완료 기준
- [ ] 명명 사본 **5벌 → 1 + 위임 3**, 테스트 페이크 1 → 0.
- [ ] `SkillMathParityTests` 가 3건 → **1건**으로 줄었다.
- [ ] 스킬 광역 10곳(`AreaCc`·`AreaDot`·`AreaStack`·`AreaTaunt`·`AreaSleep`·`ConeBreath`·
      `TileStatBurst`·`StatAura`×2·`GrantShield`)이 새 자로 같은 답을 낸다.
