# 1 · Spine 데이터 — `idleVariants` → `idleBreaks` · `idleBreakInterval`

## 목적

Spine 유닛 SO 가 스프라이트 세트와 **같은 모양·같은 뜻**의 대기 컷 데이터를 갖게 한다. 소비자(뷰)는 unit 2 — 이 단위는 저작 표면만.

## 변경 대상

- 수정 `Data/ISpineUnitVisualData.cs` — `SpineIdleVariants` → `SpineIdleBreaks` · `IdleBreakInterval` 추가 (주석 갱신)
- 수정 `Data/DefenderUnitData.cs` · `Data/AttackUnitData.cs` — 필드 `idleVariants` → `idleBreaks`(`[FormerlySerializedAs("idleVariants")]`) ·
  `idleBreakInterval`(Vector2, 기본 (1,3)) **맨 뒤** 추가 · getter
- 수정 `Presentation/SpineUnitView.cs:882` — getter 이름만(동작은 unit 2 에서)
- 수정 `Assets/_Project/Data/Defenders/Defender_Summoner.asset` — `[idle, idle2, idle3]` → `[idle2, idle3]` (기본 idle 은 풀에 없다)
- 수정 `docs/spec/summon-patrol-defender/10_unit_animation_structure.md` — 계약 5·7 상단에 「idle-break-shared 로 대체됨」 각주

## 구현

`[FormerlySerializedAs]` 로 기존 에셋 값을 보존한다 — 유닛 에셋 50여 개 중 값이 있는 건 소환사 하나. 직렬화 키 개명이므로
에셋 diff 는 저장하는 파일에서만 난다(소환사만 저장). `idleBreakInterval` 은 맨 뒤라 다른 에셋 diff 0.

`OnValidate`(DefenderUnitData 에 이미 있음): `idleBreakInterval.x < 0 || y < x` 면 LogError(스프라이트 세트와 같은 문구).

## 완료 기준

- 컴파일 0 에러 · `Defender_Summoner.SpineIdleBreaks == [idle2, idle3]` · `IdleBreakInterval == (1,3)`.
- 다른 유닛 에셋 diff 0(`git status` 로 확인).
- EditMode 전체 초록(`UnitAnimationChoiceTests` 등 기존 무변).
