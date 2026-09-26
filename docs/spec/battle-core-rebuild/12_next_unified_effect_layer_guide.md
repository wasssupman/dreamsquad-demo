# 12 — 후속 spec 가이드: 「통합 효과 층」 (다음 세션이 spec 을 쓰기 위한 맥락)

> 이 문서는 spec 이 아니다. `battle-core-rebuild` 가 끝난 자리에서 **다음 spec(가칭 `unified-effect-layer`)을 쓸 사람**이 대화 없이 출발할 수 있게, 논의의 결론·근거·코드 위치·완료 기준을 한곳에 모은 인계다. 계약의 정본은 README 「조각 E 사용자 결정」 ⑪ 이고, 증거는 탐침 테스트 두 파일이다.

## 1. 왜 이 spec 이 필요한가 (한 문단)

전환은 「옛 규칙을 그대로 옮긴다」였고 끝났다. 그 뒤 사용자가 낸 하드 케이스 둘(§3)을 코어에 실제로 세워 돌려 보니, **실행 층의 절반은 이미 하나**(바인딩 한 종류 · 디스패처 한 개 · 착탄 해석·도달 자 한 벌 · 출처는 꼬리표)인데, **「의도 → 발사 요청」 조립과 저작 검증이 트리거 종류별로 갈라져** 있어 「효과 하나를 다른 트리거·다른 원점에서 재사용」이 두 케이스 모두에서 막혔다. 이건 옛 게임에 없던 조합이라 전환 spec 범위 밖이고, 코어 규칙(원점)을 바꾸는 일이라 별도 spec 이다.

## 2. 사용자 결정 (되돌리지 말 것)

- **⑩ 프로젝트 방향**: 프로덕션 시작 단계. 추후 서버 권위 실시간 서버(매치 설정 서버 수신 · 핵심 로직 서버 · 클라 = 보여주기 + 행동 리포트). **커맨드 = 서버 로직의 어휘.** CI 시기상조. UI·에셋은 데모(정본 아님). 지금 목적 = 코드 레벨을 정식 설계에 맞추기. **서버 권위는 목표만, spec 은 열지 않는다.**
- **⑪ 통합 효과 층 계약** (사용자 원문: 「적의 위치 좌표로 통합 — 위치로 통일. 출처(skill·dreamcatcher)는 구분되되 각 출처의 트리거 검사와 효과 발동은 동일한 로직을 타야 한다」):
  - **효과 = f(원점 좌표 + 원점 항, 파라미터).** 효과는 출처를 모른다.
  - **트리거 = 사건 → 원점 좌표 산출기.** 커맨드 = 지정 칸 · 타격 = 맞은 적 위치 · 남의 배치 = 배치된 유닛 위치 · 자기 사건(사망·퇴근·주기) = 주체 위치. 호밍은 「좌표 + 선택적 대상 엔티티」이고 호밍 여부는 **탄의 궤적 속성**.
  - **출처(`BindingOrigin`) = 귀속·수명 꼬리표.** 트리거 검사·효과 발동은 한 경로.
  - 원점 항은 제약 13 그대로: 자리형 = 칸 반폭 0.5 · 몸형 = 발동 주체의 몸. 사건이 이미 `OriginBody` 를 값으로 나른다.
- 시트에 대한 사용자 의견(결정은 아님, 방향): **시트는 소유자별 탭(UnitSkill·Dreamcatcher·…), 코드는 한 레이어.** 조건 = 탭이 달라도 열 스키마 하나 · 효과는 Effects 탭에서 한 번 정의하고 소유자 탭이 id 로 참조 · 임포터는 탭 → 출처 꼬리표만 · 검증(`EffectWitness`)도 한 레이어로 전 출처.

## 3. 하드 케이스 둘과 실측 결과 (증거 = 테스트)

### 케이스 1 — `Tests/EditModeCore/HardCaseUnifiedSkillProbeTests.cs` (`06441f875`, 7 ○ · 2 Ignore)
- A 유닛 배치 스킬: 배치 시 N 안 **모든 적**에게 각 1발 호밍 투사체 100. **○** — `OnPlace` × 발사 명세 「전원 손잡이(FanOutToAllCandidates)」 + 호밍 탄. `ProjectileToTarget` 로는 안 됨(배치 사건에 대상이 없음).
- AA 드림캐쳐: 호스트 생존 동안 신규 배치 유닛마다 A 시전. **×** — 바인딩(`Subject.Any` + `Filter.PlacedDefender` + `Lifetime.Owner`)의 발화·해제는 되지만 **발사 명세 버스트 슬롯이 바인딩 소유자(호스트) 목록에 들어가 호스트 자리에서 쏜다**: `Scripts/BattleCore/Trigger/IntentApplier.cs:301-306`(슬롯 등록) · `Scripts/BattleCore/Phases/CombatPhase.cs:975-979`(소유 유닛으로 전진) · 원점/Owner `:1026·1040·1093·1098·1158·1163`. 귀속이 호스트인 것은 우연.
- 저작: 카드 빌더가 카드의 `OnPlace` 를 거절 `Scripts/BattleCoreUnity/CardDefinitionBuilder.cs:261`, 카드 발사 명세는 주기 트리거만 `:369`.
- 효과 정의: `BindingDef` 는 struct 라 A·AA 두 줄에 **값 복사**(탄·명세는 인덱스 공유). 패턴 경로 피해는 `PatternDef.Damage`(`IntentApplier.cs:311`), Magnitude 무관.

### 케이스 2 — `Tests/EditModeCore/HardCaseMeteorProbeTests.cs` (`fab244046`, 12 ○ · 3 Ignore)
- 액티브 운석(지정 칸 · N · 100) **○**. 타격 운석(맞은 적 위치 · N · 30) 코어 손조립 **○**(자리 = 맞은 적, 호스트 아님 · 3타 3개 · 귀속 = 부착 유닛).
- **하나인 것**: 같은 탄 줄 · SkyFall × TileAoe → `ResolveTileAoe` → `SkillMath.ReachFromImpact`(원점 항 = 칸 반폭) · 사건 사슬 동일 · 피해는 바인딩 Magnitude 라 탄 줄 하나로 100/30 · 착탄 VFX 키(`ProjectileHit.DefIndex`) 동일.
- **갈리는 것**: ① 대상 조준 갈래가 바인딩 TileRange 를 **재조준 반경**으로 쓰고 착탄 반경은 탄 정의값(`IntentApplier.cs:271` · `Phases/TickProjectilePhase.cs:459`) → 같은 탄 줄로 N 이 다른 운석 불가. ② 대상 갈래는 예고·비행시간을 안 채워 즉발(`IntentApplier.cs:258` vs 자리 갈래 `:276-286`) → 착탄 예고 없음 + **뷰가 낙하 그림을 못 그림**(사건은 틱 끝 배달 `Match/EventBus.cs:72-76`, 뷰는 그때 월드 탄을 찾음 `BattleCoreUnity/View/CoreProjectileViewPool.cs:95-97`; 비행 0 인 모든 대상 조준 SkyFall 탄 공통 결함). ③ 저작: 카드 빌더가 셀 바인딩 탄 거절 `CardDefinitionBuilder.cs:313`; 받는 `SkyFallOnTarget` 은 (`SkyFallOnEntity`, `SingleSplash`)로 번역(`CombatDefinitionBuilder.cs:417-418`) → 직격+비산이라 착탄 해석이 다르고, 비산 도달이 **인라인 자** `TickProjectilePhase.cs:874`(제약 13 확인 필요).

### 요약 판정
「트리거만 바꿔 같은 효과를 재사용」이 두 케이스 모두 **부분 커버**. 막힌 곳은 셋으로 수렴한다: (a) 원점이 「사건 주체 좌표」가 아니라 「바인딩 소유자」 또는 「갈래별 관례」에 매임 · (b) 발사 요청 조립이 갈래별(대상/자리)로 반경·예고·비행시간 출처가 다름 · (c) 카드 빌더가 출처별 블랙리스트로 저작을 막음.

## 4. 코드 지도 — 손댈 곳과 손대면 안 되는 곳

**손댈 곳(코어 규칙 변경 — 이 spec 의 본체)**
- `Scripts/BattleCore/Trigger/IntentApplier.cs` — `SpawnProjectile` 의 대상 갈래(:258)·자리 갈래(:276) → **좌표 한 갈래**. 입력 = 원점 좌표 + 원점 항 + 선택적 대상 엔티티(호밍) · 반경·예고·비행시간은 효과 정의에서. 발사 명세 슬롯 등록(:301-306)을 소유자가 아닌 원점 좌표 기준으로.
- `Scripts/BattleCore/Phases/CombatPhase.cs` — 버스트 슬롯 전진(:975-979)과 원점/Owner 읽기(:1026·1040·1093·1098·1158·1163)를 슬롯이 든 좌표로.
- `Scripts/BattleCore/Trigger/TriggerEvent.cs` · `TriggerDispatcher.cs` — 사건이 이미 대상·자리·`OriginBody` 를 값으로 나른다. 「원점 좌표 산출기」를 트리거 종류별로 한 곳에 모을 자리(새 파일 권장, 담당자 안).
- `Scripts/BattleCore/Phases/TickProjectilePhase.cs:874` — 비산 도달 인라인 자 → 정본 진입점(`AttackReach`/`SkillMath.ReachFrom*`)으로.
- 발사 명세 대상 선정: `Data/PatternSpec.cs` `PatternSelectionRule`(RoundRobin·DeterministicShuffle·None·Nearest) + 「전원 손잡이」 — 「범위 안 전부」는 손잡이로 가능. 필요하면 규칙 하나로 승격.

**손댈 곳(Unity 층)**
- `Scripts/BattleCoreUnity/CardDefinitionBuilder.cs:261·313·369` · `BindingDefinitionBuilder.cs` — 출처별 블랙리스트 → 「좌표를 못 뽑는 조합」만 거절. `CombatDefinitionBuilder.cs:417-418` 운석형 번역 재검토.
- `Scripts/BattleCoreUnity/View/CoreProjectileViewPool.cs:95-97` — 즉발 탄도 그릴 수 있게 사건에 뷰 키(def 인덱스·궤적)를 실어 월드 탄에 의존하지 않기.
- 저작 층(2단계): `Data/Dreamcatcher/DcMechanic.cs`(값 struct) → 효과 참조(`SkillEffectData` SO) + 트리거·주체·게이트·수명 + 오버라이드. 카드 SO 52장·`UnitSkillAbility`·`nightmareMechanics` 마이그레이션. enum 은 append-only(`DcTriggerKind` 10 · `DcPayloadKind` 33)라 번호 무변.
- 시트(3단계): `Editor/UnitStatImport/DcSheetExporter.cs:72-92`·`DcSheetApplier` — Effects 탭 신설 + 소유자 탭(UnitSkills·Dreamcatcher·Nightmares) 같은 열 스키마 · `EffectWitness`(Assets lane)를 전 출처로.

**손대면 안 되는 곳 / 지켜야 할 것**
- CLAUDE.md 「전투 코어 — 절대 제약」 6 · 제약 13(판정 자 하나 · 원점 항은 형이 정한다) · 코어 `UnityEngine`·리터럴 금지 · `SimEntityId` 순회 · 사건은 값 스냅샷.
- 골든 11 은 합성 고정구라 이 경로를 안 탄다 — **무변이 규칙 동일의 증거가 아니다.** 증거는 탐침 테스트(§5)와 `EffectWitness`.
- 결정 ⑦-2(부가 피해는 거점도 침) · ⑦-3(관통은 가까운 적부터) · ⑧(예보 경로 옛 방식 · 예고선 병합 새 방식) · ⑨ 플레이 4차 통과. 이 spec 이 건드릴 이유 없음.

## 5. 완료 기준 초안 (탐침이 이미 박아 둔 것)

- [ ] `HardCaseUnifiedSkillProbeTests` 의 `[Ignore]` 2건 해제 후 초록(AA 의 버스트가 배치된 유닛 자리에서 · A == AA 대상 집합 동치). `현행_` 박제 테스트는 빨개져야 하므로 **삭제 또는 목표형으로 뒤집기**.
- [ ] `HardCaseMeteorProbeTests` 의 `[Ignore]` 3건 해제 후 초록(같은 탄 줄로 반경 다른 운석 · 타격 운석 착탄 예고 · 낙하 그림 뷰 키).
- [ ] 카드 저작으로 AA·타격 운석이 굽힌다(빌더 검증 통과 + `EffectWitness` 관측).
- [ ] 코어 diff 는 §4 목록 안 · 골든 11 Verify(무변이 예상이나 바뀌면 트레이스로 원인) · 헤드리스 918 기준 + 신설 · EditMode 3 어셈블리 선행 2 외 0 · PlayMode 코어.
- [ ] `core-reviewer` APPROVE. 리뷰어 정의(`.claude/agents/core-reviewer.md`)의 제약 13 줄이 인라인 자를 HIGH 로 본다.

## 6. 권장 작업 단위 (작은 것부터, 값 무변 → 규칙 → 저작)

1. **0 — 계약**: README(⑪ 인용) · 이 문서의 §4 를 변경 대상 표로 · 완료 기준 §5.
2. **1 — 좌표 갈래 통일**(코어): `SpawnProjectile` 한 갈래 + 원점 산출기. 케이스 2 ①②(코어 부분) 해소.
3. **2 — 버스트 원점**(코어): 슬롯을 원점 좌표에. 케이스 1 AA 해소.
4. **3 — 뷰 키**: 즉발 탄 낙하 그림. 케이스 2 ② 뷰 부분.
5. **4 — 빌더 검증 완화**: 출처 블랙리스트 → 좌표 산출 가능성. 케이스 1·2 저작 해소. 비산 인라인 자 정본화.
6. **5 — 효과 자산화**: `DcMechanic` 값 → 효과 SO 참조. 카드 52장·유닛 능력·악몽 마이그레이션.
7. **6 — 시트 Effects 탭** + 소유자 탭 스키마 통일 + `EffectWitness` 전 출처 + 죽은 컬럼 4·거짓 문안 2 정리(`docs/spec/README.md` 「시트 ↔ 저작 ↔ 코어 정합 감사」 참조).

## 7. 운용 함정 (이 세션에서 실제로 겪은 것 — 상세는 `handoff_session_2026-09-24.md` §3·§8·§9, `docs/reference/lessons/05-agent-operations.md`)

- 헤드리스는 **클린 export**(`git archive` → `dotnet test`)에서만 믿는다. 워크트리에서 바로 돌리면 stale dll·`bin/obj` 캐시로 `Faction` CS0246 거짓 빨강.
- Unity 는 한 번에 한 주체만. 에이전트 「반납」 보고는 `isPlaying`·러너 상태로 확인. 사용자 Play 중이면 어떤 Unity 부작용도 금지.
- PlayMode 코어 씬 픽스처는 `dev_forceMapIndex`(PlayerPrefs)를 상속한다 — 사용자 플레이가 맵을 바꾸면 지형 의존 테스트가 빨개진다(0 으로 단독 재실행해 판별 · 복원).
- 코어 lane 직후 옛 lane 잔류(도메인 리로드까지) — 이제 옛 lane 은 없지만 `RequestScriptReload` 판별법은 유효.
- 리드가 「사용자 결정 필요」를 스스로 닫지 말 것(정합성 감사가 3건 잡았다). 플레이어 규칙은 묻는다.
- 「동률 결정론」 선례를 「기하 순서 규칙」에 적용하지 말 것. 옛 주석이 규칙을 말하면 그것이 spec.

## 8. 남은 사용자 몫 (이 spec 밖)

GitHub 푸시 승인(main 미푸시) · 시트 문안 2건(bomb_man·boomerang) · Android QA 빌드(Entities 없이 첫 빌드) · 로그인 계정 실제 랭킹 · 실서버 쓰레기 계정(`AuthE2ETest`) 정리 · 워크트리 `wassup-core` 정리 전 헤드리스 csproj 4개의 dll 경로를 main 워크트리로.
