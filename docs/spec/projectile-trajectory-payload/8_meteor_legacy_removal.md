# 8 — Meteor 레거시 경로 삭제 + 채널 문서 갱신

## 목적

unit 7 로 미사용이 된 Meteor 전용 파이프라인을 삭제한다. NativeQueue 채널 1개가 은퇴하므로 **맥락 간 통신 계약 문서**(CLAUDE.md·TRD 의 채널 목록)도 함께 갱신한다 — 코드와 계약 문서의 원자적 동기화가 이 unit 의 핵심.

## 변경 대상

- 삭제: `Assets/_Project/Scripts/Battle/Effects/MeteorPending.cs` · `Battle/Combat/MeteorResolutionSystem.cs` · `Battle/Combat/MeteorBurstEvent.cs` · `Battle/Combat/MeteorBurstEventsSingleton.cs` · `EffectSpawner.SpawnMeteor` 메서드(unit 7 에서 미사용화됨)
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — `_meteorBurstQueue` lifecycle **5지점** 제거: 필드 선언(~188) · teardown(~412) · OnDestroy dispose(~437) · 생성(~930-933) · drain(~1744, ~1905-1913)
- `CLAUDE.md` — NativeQueue 채널 목록 15개 → 14개 (`MeteorBurstEventsSingleton` 제거). **TRD 는 무편집** — 열거식 채널 목록이 없음을 확인함(critic 검증), CLAUDE.md 가 유일한 열거 목록.

## 구현

1. 파일 4개 `git rm` (+`.meta`) + `SpawnMeteor` 메서드 삭제. 신규/삭제 .cs 반영은 `refresh_unity(scope=all)` (lessons/01).
2. BattleBridge 큐 lifecycle 5지점 제거. teardown/Dispose 대칭 확인 — 남은 큐들의 순서 불변.
3. 잔존 참조 정리. **주의**: `SpawnMeteorBurst`(VfxSpawner·bridge 호출부)·`SpawnMeteorWarningVisual`·`MeteorFall` 등 Presentation 계열은 unit 7 배선이 계속 사용 — 유지(unit 9 대상).
4. CLAUDE.md 채널 목록 갱신은 같은 커밋에 포함.

## 완료 기준

- `rg "MeteorPending|MeteorResolutionSystem|MeteorBurstEvent"` 코드 0 매칭 (`SpawnMeteorBurst` 는 유지라 패턴에서 제외 — critic 지적 반영).
- compile PASS + EditMode 전체 GREEN (알려진 무관 사전실패 ObstaclePlacer 제외).
- Play smoke: meteor 캐스트 → unit 7 과 동일 동작. StopBattle/재시작 시 teardown 에러·leak 경고 없음.
- CLAUDE.md·TRD 채널 개수/목록 일치.

확인 2026-07-06 — rg 잔존 0 · 리그 EditMode 506/509(동일 베이스라인) · MCP Play smoke: 캐스트→투사체 1→0 해결·콘솔 에러/경고 0·teardown 클린 · CLAUDE.md 채널 15→14 갱신(TRD 는 열거 목록 없음 확인, 무편집).
