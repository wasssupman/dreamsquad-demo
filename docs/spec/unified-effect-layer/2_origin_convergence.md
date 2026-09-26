# 2 — 원점 읽기 한 벌 (코어 + Skills · H1)

## 목적
concrete 가 원점을 `target.CellA` · `p.EventPosition`/`EventBodyRadius` · `ctx.Position(caster)` 셋으로 제각각 읽는 것을, 드레인이 채운 **원점 두 값**(계약 1: 발사 자리 · 효과 좌표 + 원점 몸)만 읽게 수렴한다. 원점 산출은 **새로 만들지 않는다** — 감지자와 드레인(`TriggerDispatcher` 의 `site = e.HasSite ? e.Site : e.SubjectPos` · `siteBody`)에 이미 있다. 흩어진 **읽기**를 모은다.

## 변경 대상 (unit 0 표 2 로 확정 — 원점을 읽는 concrete 21/29)
- `Scripts/Skills/` 입력 형 — `SkillParams`/`SkillTarget` 의 원점 필드를 한 묶음으로(이름·배치는 구현 판단). 액티브 커맨드의 지정 칸(`CellA`)도 드레인/커맨드 경로가 같은 필드에 채운다.
- `Scripts/BattleCore/Trigger/TriggerDispatcher.cs` · `CoreSkillContext.cs` · 커맨드 시전 경로 — 채우는 곳.
- 원점을 읽는 concrete 21개(`census.md` 표 2 ○). 그중 `CastHazardSkill` 은 레지스트리 미등록 죽은 파일 — 같이 고치거나 지운다(지우면 `SkillRoutingTests` 확인).
- 형을 쓰는 다른 어셈블리: `Wassup.Tests.EditMode`(`Tests/EditMode/TestSkillContext.cs` 가 `ISkillContext` 구현 · `MetaSkillsTests` · `StatAuraSkillTests` · `AllySpeedAuraSkillTests` 위치 인자 생성 · `ReachEntryPointGuardTests` 의 `EventBodyRadius`) · `Wassup.Runtime` `DcSkillRouting.cs`.

## 구현
- 원점 항은 **감지자가 이미 스냅샷한 값**을 그대로 쓴다(계약 2 — 몸형 = 원점 주인의 몸, 시체 폭발이면 죽은 적). `CasterRef.BodyRadius` 로 대신하지 않는다(`SkillParams` 의 기존 경고 유지).
- 후보 거리 계산(수면·실드·브레스·발사 명세 후보)의 위치 읽기는 원점 읽기가 아니다 — 건드리지 않는다.

## 완료 기준
- **Unity 컴파일 먼저**(`Wassup.Skills.dll` 갱신) → 헤드리스 클린 export(csproj 가 워크트리 dll 을 참조).
- EditMode 3 어셈블리 전부(선행 2 외 0) · 골든 11 · unit 0 표 1 전 행 무변 · `EffectWitness`.
- 소스 단언(`CoreArchitectureTests` 계열, 스캔 경로에 `Scripts/Skills/Concrete` 명시): concrete 가 `ctx.Position(caster.Unit)` 을 **효과 좌표로** 쓰지 않는다 · `EventPosition` 류 옛 필드 참조 0.
- `core-reviewer` APPROVE.

- 구현 2026-09-26 · `19db40d8d` — 헤드리스 926/3 skip · Unity EditMode 3 어셈블리 2532 중 실패 = 선행 2(bomb_man·boomerang)뿐 · 골든 11 일치. 리뷰 = 묶음 A(unit 2+3).

- 묶음 A(unit 2+3) core-reviewer APPROVE(발견 0) 2026-09-26.
