# DoT Tick Cadence — 이산 주기 DoT

> 상태: 완료 2026-07-18 (커밋 aedcb66f) · Play 로그 확인 완료

## 검증 질문

> "화염/독이 매 프레임 소수 데미지를 흩뿌리는 대신, **정해진 주기마다 정수 청크 데미지 1회**를 주고, 데미지 폰트도 그 주기마다 한 번만 뜨는가?"

## 배경 (왜)

현재 DoT(`CcKind.DoT`)는 `DotApplySystem`에서 `amount = scalar × deltaTime`으로 **매 프레임 연속** 적용된다. param1=20·60fps면 프레임당 ≈0.33 데미지 → `DamageApplicationSystem`이 버퍼 엔트리당 폰트 1개를 띄우고(`:131-142`), `DamageNumberSpawner`가 `Max(1, RoundToInt(0.33))=1`로 바닥 처리(`:86`) → **초당 ~60개의 "1" 폰트 스팸**. 실제 데미지 총량(20 DPS)은 정상이나 표시가 판독 불가.

해결: DoT에 **tick 간격** 개념을 도입해, 주기마다 `scalar`(=tick당 데미지) 청크를 1회 지급한다. 폰트는 자연히 tick당 1개(예: 0.5초마다 "10")로 정리된다.

## 목표 수치 (캐스터, 1x1)

| 존 | tick당 데미지(param1) | tickInterval | 실효 DPS |
|---|---|---|---|
| Fire (`Hazard_Fire_1x1`) | 10 | 0.5s | 20 |
| Poison (`Hazard_Poison_1x1`) | 20 | 1.0s | 20 |

## 작업 단위

| # | 구분 | 문서 | 목적 |
|---|---|---|---|
| 0 | data | `0_data_model.md` | `HazardEffect.tickInterval` SO 필드 + `CcEffect.tickInterval`/`tickTimer` 필드 + ZoneApply 전달 + CcApply 병합 보존 (plumbing, 무행동변화) |
| 1 | logic | `1_tick_application.md` | `DotApplySystem` 이산 tick 지급 + EditMode 테스트 |
| 2 | data | `2_asset_values.md` | Fire/Poison 1x1 에셋 값 확정 + Play 검증 |

## Feature-wide 계약

1. **`tickInterval <= 0` = 레거시 연속 DoT** (`scalar × dt`). 기존 DoT 소스(`StackModifierTickSystem` 스택 DoT, 3x3 존)는 tickInterval 미설정 → **행동 불변**. 하위호환 필수.
2. **`tickInterval > 0` = 이산 tick.** 이때 `scalar`(param1)의 의미가 "DPS" → **"tick당 데미지"** 로 바뀐다. 주기마다 `scalar` 청크를 `IncomingDamage`에 1개 추가.
3. **첫 tick은 즉발.** 적이 존에 진입/DoT 최초 적용 시 `tickTimer = tickInterval`로 시작 → 첫 프레임에 즉시 1회 지급(진입 반응성).
4. **tickTimer는 소유 슬롯의 지속 상태.** `CcApplySystem` 병합(매 프레임 존 refresh) 시 **기존 tickTimer를 보존**한다(초기화 금지). 안 그러면 누적기가 리셋돼 영원히 tick 못 함.
5. **DoT는 여전히 kind별 단일 슬롯**(비스택). Fire+Poison 동시 노출 시 `scalar`/`tickInterval`은 마지막 처리분으로 덮임 — 기존 비스택 성질 유지(본 spec은 스택 도입 아님).
6. **폰트/스포너는 무수정.** tick 청크가 정수(10/20)라 `Max(1,RoundToInt)` 바닥 문제 자연 해소. `DamageNumberSpawner` 손대지 않음(스코프 밖).
7. **3x3 DoT 존은 이번 스코프 밖** — tickInterval=0 유지(연속). 필요 시 후속.
8. **병합 정책 단일 소스 = `CcEffectMerge.Apply`** (`CcApplySystem`·`EffectSpawner` 공유). 정책 갈라짐 방지 — 어느 경로로 들어와도 동일 규칙.
9. **interval 변경 시 tickTimer 진행률 환산.** 서로 다른 주기의 DoT(Fire 0.5↔Poison 1.0)가 같은 슬롯에서 교체될 때 `tickTimer = tickTimer/oldInterval*newInterval` 로 "다음 tick 까지 %" 를 보존 → 조기/이중 tick 방지. 동일 interval refresh(흔한 케이스)는 환산 없이 그대로.

## 변경 대상 파일

- `Assets/_Project/Scripts/Battle/Effects/HazardEffect.cs` (SO 필드)
- `Assets/_Project/Scripts/Battle/Effects/CcEffect.cs` (런타임 필드)
- `Assets/_Project/Scripts/Battle/Effects/ZoneApplySystem.cs` (전달)
- `Assets/_Project/Scripts/Battle/Effects/CcApplySystem.cs` (병합 보존)
- `Assets/_Project/Scripts/Battle/Effects/DotApplySystem.cs` (tick 지급)
- `Assets/_Project/Data/Hazards/Hazard_Fire_1x1.asset`, `Hazard_Poison_1x1.asset`
- `Assets/_Project/Tests/EditMode/DotApplySystemTests.cs` (확장)

## 후속 후보

- 3x3 화염/독 존도 이산 tick 통일
- `StackModifierTickSystem` 스택 DoT를 이산 tick으로 승격(현재 연속 유지)
- tick 지급 순간 임팩트 VFX/사운드(현재는 데미지만)
- **Fire+Poison 같은 셀 겹침의 근본 정리**: 현재는 비스택 단일 DoT 슬롯이라 겹치면 `scalar`가 last-writer 로 10/20 교대(사용자 승인한 비스택의 결과, timer 는 환산으로 매끈). 완전 분리(원소별 독립 DoT = 각자 tick·데미지 합산)를 원하면 merge key 를 `(kind, source)` 로 확장 — 별도 spec(사용자가 "유지" 결정해 현재 스코프 밖)
