# battle-audio — Handoff Summary

> 세션 인계 지도. 최신 계약은 `README.md` 우선. 구현 상세는 코드/커밋.

## Commit

- `6b4912bd` feat — 투사체 발사 SFX + BGM (SoundManager 확장, unit 0·2)
- `75a8afaf` fix — 투사체 발사음 디펜더 전용 (적 투사체 무음)
- `62dac1b4` / `be28670a` feat/fix — 배치 추임새 TTS (unit 3, 보이스 반복)
- `d550b0b7` feat — 캐릭터별 배치 추임새 16종 + 1분 BGM (정식 유료 플랜)
- `137cf209` fix — 배치 음성 성우급 재생성 (eleven_v3 + 성별 방향) ← **현재 baseline**

## Implemented

- `SoundManager`(`Wassup.Core`, BattleScene GameObject) 확장 — 라운드로빈 보이스 풀 + 전용 루프 BGM 소스. 인가된 싱글턴(CLAUDE.md §5).
- 투사체 발사 SFX: `BattleBridge` 의 `ProjectileSpawnRequest` 드레인 훅. **슈터가 `DefenderUnitTag` 일 때만** 재생(적 투사체 무음). `projectileFireMinInterval`(0.045s) 스로틀.
- BGM: ElevenLabs Music API(`music_v2`) 60초 tense-battle 루프. `GameManager.PhaseChanged` 지연 구독 → Battle 진입 재생/이탈 정지(`bgmOnlyInBattle`). import loadType=Streaming.
- 배치 추임새: `BattleBridge.TryBeginDefenderDeployment` 성공 직전 → `SoundManager.PlayDeployVoice(unitData.deployVoiceClip)`. 클립은 `DefenderUnitData.deployVoiceClip`(캐릭터별) — 16 디펜더 각각 다른 보이스.
- 튜닝값(볼륨·스로틀·`bgmOnlyInBattle`) 전부 `[SerializeField]`. 런타임 API 호출 0 — 전부 저작-시점 로컬 에셋.

## Key Files

- `Assets/_Project/Scripts/Audio/SoundManager.cs`
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — 투사체 드레인 훅(~2080), Deploy 훅(~2784)
- `Assets/_Project/Scripts/Data/DefenderUnitData.cs` — `deployVoiceClip` 필드
- `Assets/_Project/Audio/ScoreTick.mp3`, `ProjectileFire.mp3`, `BattleBgm.mp3`, `DeployVoice/Deploy_{name}.mp3` (16)
- `Assets/_Project/Data/Defenders/Defender_{name}.asset` (16) — 각 `deployVoiceClip` 배선

## Verified

- Compile 정상, MCP `refresh_unity` idle. 배선(BattleBridge 훅 · SO 필드 · SoundManager 씬 오브젝트) 코드/에셋 확인.
- **청각 품질 검증은 사용자 Play 몫** — 개별 사운드 퀄리티는 후속으로 이관(2026-07-08 사용자 결정).

## Notes

- **디펜더 전용 필터는 의도** — 되돌리지 말 것. 적 원거리 공격은 같은 `ProjectileSpawnRequest` 드레인을 공유하므로 `DefenderUnitTag` 체크로 분기. 스킬/메테오(`ApplyMeteor`→`SpawnProjectile` 직접)는 이 경로 밖이라 무음.
- **클립 덮어쓰기 = GUID 유지** — 같은 경로에 재생성하면 SO 배선 불변. 후속 재생성 시 경로 덮어쓰기로.
- 배치 음성 baseline 은 **한국어 16종**(`137cf209`). 사용자 청취상 어색 판정 → **일본 애니 성우 방향 전환 예정**(Archer 1종 검증 완료). README 후속 후보에 초안 라인·보이스 레시피 기록됨.
- ElevenLabs 키는 커밋 파일에 미포함(스크래치패드만). 작업 종료 후 사용자 키 로테이션 권장.

## Follow-up

- **배치 추임새 일본어 전면 재생성** — Archer 방향 검증 완료, 남은 15종 일괄 재생성(README 초안 라인 참조).
- 유닛/속성별 발사 SFX, BGM 이음새/스테이지별 변주, 적 처치·스킬·UI SFX, 오디오 믹싱/덕킹, 음소거/볼륨 UI — 전부 README 후속 후보.
- origin 미푸시 커밋(6b4912bd~137cf209 + 이 docs 커밋) push 여부 사용자 확인.
