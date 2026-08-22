# 5 — Handoff: M0 완료 (units 0~4)

> 이 문서는 **M0 구현 완료 시점**의 인계다. M1+ 작업 단위는 7번부터 이어 쓴다.
> 이전 판(구현 0줄 시점 인계)은 이 문서로 대체됐다.

## Commit

| unit | 커밋 | 제목 |
|---|---|---|
| 0 | `edfd5dc2` | 유효 시스템 총순서 캡처 + 미선언 순서 핀 |
| 1 | `57bf6518` | 동률·난수의 축을 `SimEntityId` 로 갈아끼운다 |
| 2 | `f682c5da` | 고정 스텝 하네스(`StepOneTick`) |
| 3 | `93b04b70` | 판 조건 물질화 + `configHash` |
| 4 | (이 커밋) | `LegacyTraceV0` 골든 하네스 + 코퍼스 7종 |

**푸시 안 됨** — 원격 반영은 사용자 승인제(CLAUDE.md).

## Implemented

- **시스템 순서 박제**: `BattleSimGroup` 48개의 실행 순서를 덤프하고 무순서 8 → 3 으로 핀.
  생성물 `order-capture.md`(자동). 순서를 **고치지 않고** 현행을 고정만 했다.
- **stable ID**: `SimEntityId`(매치 내 유일·비재사용, 스폰 순 발급). 타겟팅 동률 3종 ·
  발사 패턴 RNG seed · `HazardCastSystem`(tie-break 신설) · Bridge 최근접 픽이 모두 이 축.
- **고정 스텝 하네스**: `SimHarnessClock` + `BattleBridge.StepOneTick()`.
  「얼마나」와 「언제 한 번」을 둘 다 스텝이 준다 — 그래서 렌더 프레임과 완전히 분리된다.
- **조건 물질화**: `MatchConfigSnapshot`/`MatchConfigWriter` + `configHash`(SHA-256 16 hex).
  스탯 SO 는 리플렉션으로 통째로 접고 아트 참조는 제외. `LoginAutoImport` 하네스 차단.
- **골든**: `LegacyTraceV0`(줄 단위 텍스트, 축은 `SimEntityId` 하나) + 드레인 관측 탭 19개 +
  코퍼스 7종(`Assets/_Project/Tests/Golden/`) + 재생성/검증 메뉴.

## Key Files

- `Assets/_Project/Scripts/Battle/Units/SimEntityId.cs` — 축의 정의와 계약.
- `Assets/_Project/Scripts/Core/TimeControl/SimHarnessClock.cs` — 스텝 시계 + 핸드셰이크.
- `Assets/_Project/Scripts/Core/MatchConfigSnapshot.cs` — canonical 직렬화 + 해시.
- `Assets/_Project/Scripts/Core/Trace/LegacyTraceV0.cs` · `LegacyTraceRecorder.cs` — 골든 포맷·탭.
- `Assets/_Project/Editor/Battle/SimHarnessRunner.cs` — 시나리오·입력 스케줄·상태 지문(공용 몸통).
- `Assets/_Project/Editor/Battle/{SimOrderDumpMenu,SimHarnessRunMenu,SimGoldenMenu}.cs` — 메뉴 3종.
- `BattleBridge.cs` — `StepOneTick` · `TickBattleFrame` · `CollectMatchConfig` · 탭 19곳 · `SimIdOf`.

## Verified

- EditMode **2579건 중 실패 1건** = 사전 실패(`UnitKitCatalogTests` 말파이트 desc 30자 > 28).
  이 작업과 무관하며 M0 착수 전부터 빨갛다.
- 하네스 결정론: 2회 900틱 전량 일치, 시계 정확히 `15.0000s`.
  `editorFocused=False` 에서 **`unityFramesConsumed=0`** 으로 완주(프레임을 아예 안 쓴다).
- 골든: 코퍼스 7종 `Verify` 전건 ✓ (`golden-corpus.md`).
- 라이브 경로 무변: Play smoke 정상(357 프레임에 시계 7.24s, 적 생존, 코스트 재생),
  콘솔 에러 0, `Time.captureDeltaTime` 원복 확인.
- **CardBuffs PlayMode 사전 실패는 이미 수리돼 있었다** — 실측 통과(3.87s). 결정할 것 없음.

## Notes (되돌리면 안 되는 의도)

1. **스텝 순서는 `Bridge → ECS` 다.** 라이브 플레이어 루프가 `Update → SimulationSystemGroup`
   순이기 때문이다. 뒤집으면 ECS 가 만든 캐리어를 같은 스텝에 드레인해 **한 틱 빠른 세상**이
   되고, 그 위의 골든은 라이브가 낸 적 없는 궤적을 정본이라 우긴다. (스펙 스케치가 반대로
   적혀 있었고 실측이 이겼다.)
2. **스텝에 태울 런타임은 셋이다** — `SkillRuntime` · `CostRuntime` · `PlacementCooldownRuntime`.
   전부 배틀 델타로 self-tick 하고 전부 **입력 통과를 게이트**한다. 하나라도 빠지면 같은 틱의
   같은 입력이 두 판에서 다른 판정을 받는다(실제로 배치가 전부 거부됐다).
   → **갱신 트리거**: 자기 `Update` 에서 `TimeManager.DeltaTime` 을 쓰는 배틀 런타임을 새로
   만들면 `StepOneTick` 에 추가하고 그 `Update` 를 하네스에서 막아라.
3. **`configHash` 는 아트를 담지 않는다.** 담으면 스킨 교체가 「조건이 바뀌었다」로 읽혀
   판독 장치가 거짓말을 한다. 아트 판정은 **null 검사보다 앞**이어야 한다(비대칭 함정).
4. **골든은 `Entity` 를 싣지 않는다.** 축은 `SimEntityId` 하나. 직렬화 왕복 게이트를 통과한
   것만 저장한다 — 실패하면 저장하지 않고 에러를 남긴다.
5. **`SimEntityId` 발급은 `AttachSimEntityId` 한 곳뿐.** 사후 부여·재사용 금지. 부착 대상은
   「타겟 후보가 될 수 있는 것」(`FactionTag`+`Health`+`LocalTransform`) 전부 + 투사체.
   → **갱신 트리거**: `FactionTag` 를 붙이는 스폰 경로를 새로 만들면 여기도 붙여라.
6. **`ThreatTable.Leader` 는 런타임 소비자가 0 이다**(누적만 돈다). 동률을 표 순서로 가르게
   해 뒀다. 되살릴 계획이 없으면 은퇴 후보.

## 레포 고유 함정 (그대로 유효)

- `LoginAutoImport` 가 로그인 시 SO 스탯을 시트값으로 덮는다 — 하네스에서는 차단되지만
  **라이브 세션에서는 그대로**다. 골든 diff 는 `configHash` 로 「드리프트 vs 회귀」부터 가른다.
- Bash 샌드박스에서 `git add/commit` 이 무산될 수 있다(exit 0 인데 index 롤백) — 샌드박스 비활성으로.
- `dotnet build` "오류 0" 은 거짓일 수 있다(신규 `.cs` 는 csproj 에 없어 건너뛴다).
  컴파일 검증은 Unity 리임포트/UnityMCP 기준.
- 원 워크트리에 병행 세션이 있다 — 스테이징은 **경로 명시**로만.

## Follow-up

- **코퍼스가 못 담은 축 3개**(unit 4 문서에 이유와 함께): 멀티골 맵(현재 `mapPool` 이 1장,
  goal 1개) · 드림캐쳐 다용 판(카드 사용이 UI 를 지난다) · 동시 사망 유발(전용 픽스처 맵 필요).
- **관측 탭이 프레젠테이션 배선 뒤에 있다** — 스포너가 null 이면 큐를 `Clear()` 하고 빠지는
  드레인이 있다. M1 에서 drain 소유권이 `LegacyMatchSessionAdapter` 로 옮겨갈 때 끊는다.
- **하네스 진입이 라이브 진입과 한 군데 다르다** — 코스트 재생 스위치를 `PlacementPhaseView`
  가 갖고 있어 하네스 드라이버가 대신 켠다. 라이브 진입 경로의 재현은 M1 의 세션 계약에서.
- `EffectTickSystem` 주석이 실측과 반대다(unit 0 기록). 모디파이어 생산자 11개 중 8개가
  소비자보다 뒤라 **1프레임 지연**이다 — 재배치 판단은 M1.
- M1 units(7~)를 설계 정본의 M1 절 기준으로 분해해 이어 쓴다.
