# 0 — AwakeningConfig SO + 유닛별 각성 보상 필드

## 목적

각성 시스템의 모든 튜너블 수치를 담는 config SO 와, 유닛(아군/적)별 사망 보상 필드를 만든다. 런타임 소비자는 아직 없다 — 컴파일 + 에셋만.

## 변경 대상

- `Assets/_Project/Scripts/Data/Dreamcatcher/AwakeningConfig.cs` (신규)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` (필드 append)
- `Assets/_Project/Scripts/Data/AttackUnitData.cs` (필드 append)
- `Assets/_Project/Data/.../AwakeningConfig.asset` (신규 — `DeckRuleConfig.asset` 과 동일 폴더 관례)
- 기존 Defender/AttackUnit SO 에셋 백필

## 구현

1. **`AwakeningConfig : ScriptableObject`** (`CreateAssetMenu "Wassup/AwakeningConfig"`, DeckRuleConfig 스타일):
   - `int gaugeMax = 100` / `int gaugeStart = 0` — 각성수치 상한·매치 시작값.
   - `int costUnit = 15` / `int costSquad = 30` / `int costActive = 20` — 타입별 사용 비용. `CostFor(CardType)` 헬퍼는 **enum 케이스 switch 로 매핑**(위치/int 기반 금지 — enum 순서는 `{Squad=0, Unit=1, Active=2}` 인데 문서 서술 순서와 다르다, critic L2).
   - `int handSize = 5` — 손패 크기(큐 front N).
   - `float slomoTimeScale = 0.3f` — 손패 열림 중 Battle 도메인 감속 배율.
   - ~~`confirmDelaySec`~~ — **rev 4 (2026-07-10) 제거**: 오부착 방어 확정 지연 폐기, touchup 즉시 커밋.
   - `int maxAttachPerUnit = 3` — 유닛당 Unit 카드 부착 상한.
2. **`DefenderUnitData.awakeningReward`** (int, 기본 4) — 이 유닛 사망 시 부여량. 직렬화 끝 append + 이유 주석(프로젝트 관례).
3. **`AttackUnitData.awakeningReward`** (int, 기본 1) — 악몽 사망 시 부여량.
4. **백필**: defender 전체 = 4, 악몽은 클래스별 1~3 배정(일반/소형 1, 중형 2, 대형/특수 3 — `EnemyClass` 기준 배정표를 커밋 메시지에 남긴다). **int 기본값은 zero-init 이 아님에 주의** — 백필 수단은 **일회용 에디터 MenuItem 스크립트로 일괄 세팅 후 스크립트 삭제**(critic L3; unityMCP execute_code 불가 우회 관례)로 지정한다.

## 완료 기준

- [ ] 컴파일 클린 (`read_console` 에러 0).
- [ ] `AwakeningConfig.asset` 생성, 인스펙터에서 필드 9종 확인.
- [ ] defender/악몽 SO 각 1개 샘플의 `awakeningReward` 인스펙터 확인 (defender=4, 악몽=클래스별 값).
- [ ] 기존 에셋 직렬화 값 변동 없음 (git diff 가 append 필드만).
