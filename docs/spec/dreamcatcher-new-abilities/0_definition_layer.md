# 0 — 정의 계층 확장

## 목적

신규 3종이 쓸 enum/필드를 **선언만** 한다(무동작·컴파일 통과). 실행/번역/집계는 unit 1·2. critic BLOCKER(선택자 필드 부재) 해소가 핵심.

## 경계 결정

정의 계층 `Wassup.Data` 는 Battle 타입 참조 금지(DcMechanic.cs 최상단 계약). 따라서 critic 이 지목한 `CcKind`/`StackKind` 선택자는 **데이터 미러 enum**(`DcCcKind`/`DcStackKind`)으로 두고 bridge 가 Battle enum 으로 번역한다(기존 `CardBuffKind→StatKind` 패턴). baked `DcTriggerSlot`(Battle 측)은 번역된 `Battle.Effects.CcKind`/`StackKind` 를 저장 — hot path 무번역.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs`
  - `DcPayloadKind` append: `ApplyCcToTarget = 10, ApplyStackToTarget = 11`.
  - 신규 enum: `DcCcKind { Stun, Impulse }`(공격 온-히트 CC 선택자 — Slow 는 이 엔진에서 CcEffect 가 아니라 MoveSpeedMul StatModifier 라 제외, unit 1 발견), `DcStackKind { Fire, Ice, Bleed, Poison }`(Battle `StackKind` 비-None 미러).
  - `DcPayloadSpec` append 필드: `public DcCcKind ccKind;`(ApplyCcToTarget), `public DcStackKind stackKind;`(ApplyStackToTarget). 주석: magnitude=Slow%/potency·duration=초(ApplyCcToTarget), magnitude=스택 수(ApplyStackToTarget). "kind별 struct 분리 YAGNI" 주석에 payload 다형성이 필드 다중화를 강제함을 적시.
- `Assets/_Project/Scripts/Data/Dreamcatcher/DreamcatcherCard.cs`
  - `CardBuffKind` append: `DamageVsCc`. (매핑은 unit 2.)
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierTypes.cs`
  - `StatKind` append: `DamageVsCcMul`.
- `Assets/_Project/Scripts/Battle/Effects/Modifiers/ModifierStats.cs`
  - append `public float damageVsCcMul;` (주석: 디폴트 1.0 — base 1 init·소비는 unit 2. unit 0 단계엔 reader 없음).
- `Assets/_Project/Scripts/Battle/Combat/DcTriggerSlot.cs`
  - append `public Wassup.Battle.Effects.CcKind ccKind;`, `public Wassup.Battle.Effects.StackKind stackKind;` (bake 시 번역 저장; 소비는 unit 1).

## 구현

선언만. 어떤 시스템도 신규 필드를 읽지 않는다(unit 0 종료 시 완전 inert). append-only 로 기존 카드/슬롯 직렬화·런타임 안정.

## 완료 기준

- [x] 4개 어셈블리 `dotnet build` 오류 0개 (Layout stale-csproj 파일 주입 후 깨끗한 신호).
- [x] 신규 enum/필드가 전부 append(중간 삽입 0) — 기존 DreamcatcherCard 에셋 역직렬화 불변.
- [x] 신규 필드를 읽는 코드 0(grep 확인) — 순수 선언. (`.stackKind` 매치는 기존 AttackOutput 의 별개 필드.)

확인: 2026-07-13 — dotnet build 컴파일 검증 (Unity 테스트 실행은 미실시).
발견: sim 에 기존 `AttackOutputKind.ApplyStack` 경로 존재 → ember_bite(unit 1) 재사용.
