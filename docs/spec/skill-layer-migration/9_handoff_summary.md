# 9 — 인계 요약

> 이 문서는 **지도**다. 최신 계약은 `README.md` 와 번호 문서가, 구현 상세는 코드와
> 커밋 히스토리가 소유한다.

## Commit

`d0e7caa7`(spec 작성) ~ `d9d10065`(정합성 리뷰 반영). 약 80커밋, **전부 로컬 · 미푸시**.
분기점만 꼽으면:

| 커밋 | 무엇 |
|---|---|
| `4947bbdf` | 토대 착수 직전 — **PlayMode 기준선을 잡으려면 여기다** |
| `af2ffa3f` | 골든 코퍼스 의존 제거(스킬 발화 기록이 0회임이 실증됨) |
| `01e86f07` | 진영 화이트리스트 2술어 은퇴 → `HasDetector` |
| `bce07b2d` | 「방어유닛 전용」 하드코딩 3곳 제거(사용자 결정) |
| `a0576e6f` | **PlayMode 가 잡은 무음 사망** → 부류 차단 3종 |
| `3050b0a3` | 리뷰 HIGH 4건 — 걷은 게이트를 대신 지킬 그물이 안 지켰다 |
| `d9d10065` | 정합성 리뷰 — 문서 과소 신고 + 그물 공백 2 |

## Implemented

- 스킬 **34종**이 `Wassup.Skills` concrete 로. 감지자는 **발화만 알리고** 실행은 concrete.
- 도메인의 ECS 무참조를 **asmdef 가 컴파일로 강제**(참조 = `Unity.Mathematics` 하나).
- seam **7개**. 이벤트가 자기 seam 을 말하고 남의 것은 큐 꼬리로 되돌린다.
- 어휘 3종 중 `OnPlaceEffectType` 타입째 소멸, `SkillEffectType` 은 저작 enum 으로 잔존,
  `DcPayloadKind` 는 **비스킬 7종만** 남고 이유가 각각 다르게 기재됨.
- 진영 화이트리스트 → 「감지자가 있나」 하나로. 적에게 `OnShieldBreak`·`OnKill`·
  `OnDamagedN`·`OnDeath` 개방.
- 「방어유닛 전용」 하드코딩 3곳 제거 → 그 과정에서 **적 전용 투사체 풀이 소비처 0 으로 은퇴**.
- 침묵 차단 3종: `SkillPayloadPolicy`(단일 정본) · bake 게이트 · 전수 그물(같은 술어 공유).

## Key Files

| 무엇 | 어디 |
|---|---|
| 도메인 포트·계약 | `Assets/_Project/Scripts/Skills/ISkill.cs`·`ISkillContext.cs` |
| concrete 전량 | `Assets/_Project/Scripts/Skills/Concrete/` |
| 어댑터(ECS 를 아는 유일한 곳) | `Assets/_Project/Scripts/Battle/Skills/EcsSkillContext.cs` |
| 드레인 · seam 정본 | `.../Battle/Skills/SkillDispatchSystem.cs`·`SkillDispatchSeams.cs` |
| **「스킬인가」 정본** | `Assets/_Project/Scripts/Data/Dreamcatcher/SkillPayloadPolicy.cs` |
| 라우팅 · 등록 | `Assets/_Project/Scripts/Bridge/BattleBridge.cs` (`SkillIdForMechanic`·`InstallSkillLayer`) |
| 트리거 개방 판정 | `Assets/_Project/Scripts/Battle/Combat/DcTrigger.cs` (`HasDetector`) |

## Verified

- EditMode **2745건** — 빨강은 선행 실패 2건(`boomerang`·`bomb_man` 문안, 시트 건)뿐.
- PlayMode **216건 중 14 빨강**. 전건 판정됨(README 하단 「PlayMode 판정」).
  리뷰 수정 뒤 재완주에서 **신규 빨강 0**.
- 투트랙 리뷰 2회 + 정합성 리뷰 3트랙. 지적은 전부 반영되거나 후속으로 이관.
- ⚠ **Play 육안 미실시.**

## Notes — 되돌리면 안 되는 것

1. **`skillId == 0`(`NotRouted`)은 「숙제」가 아니라 「스킬이 아님」이다.** 이 값의 뜻이
   이전 도중 조용히 뒤집혀 `OnPlace × 충전` 이 무음으로 죽었고, 첫 PlayMode 가 70여 커밋
   뒤여서야 발견됐다. 이름·정책·게이트·그물 네 겹이 그 재발을 막는다 — 한 겹도 빼지 말 것.
2. **계약에 개수를 적지 않는다.** 계약 10개 중 갈린 둘이 **둘 다 개수**였다(드레인 3, 예외 3건).
   원칙형 8개는 전부 살아남았다. 개수는 코드가 소유한다.
3. **어휘 밖 2종의 이유가 서로 다르다.** `PlacementAura` = 시제(발동 규칙),
   `HeavyStrike` = 자기참조(그 공격의 성질). 뭉뚱그리면 다음 후보를 오분류한다.
4. **`SkillSeam.None = 0`** 은 「생산자가 안 채웠다」 판별용이다. 0 에 진짜 seam 을 두면
   안 채운 이벤트가 조용히 그리로 흘러간다.
5. **페이크가 질의를 막으면 그물을 칠 수 없다.** `TestSkillContext.TryDensestOpponentCluster`
   가 `false` 스텁이던 동안 토대 검증 질문에 증인이 없었다.

## Follow-up

- **푸시 미승인** — 약 80커밋이 로컬에만 있다.
- **시트 묶음**: `boomerang`·`bomb_man` 문안 + 슬라임 「작은 = 중간의 50%」가 데이터상 60%.
- **PlayMode 14건**은 원래부터 빨갰다 — 별건으로 손볼 것.
  특히 `DragPlacementReach` 는 리플렉션 시그니처 예외라 테스트 자체가 깨져 있다.
- **검증 질문 미달**: switch 2곳 → 1곳(`DcApplicability` 단순화).
- **후속 후보**: 출처 사망 시 모디파이어 회수(생기면 `PlacementAura` 영수증이 불필요해진다) ·
  `SplitOnDeath` 형태 점검(시제상 스킬인데 배선만 다른 길) · `EmitProjectilePattern` splash 저작 검증.
- **진짜 기준선**: `wassup-testrig` 를 `4947bbdf` 에 세우면 PlayMode 14건의 선행 여부가
  실측된다. 오늘은 그 워크트리가 812커밋 뒤 + 미커밋 10여 파일이라 보류했다.
