# Handoff — dreamcatcher-subconscious-unit

## Commit
- `fe4ba372` feat(dreamcatcher): … 느린 각성 Unit 전환 + 무의식 프레임 + 영속경로 은퇴

## Implemented
- `DcPayloadKind.SelfWarmupBuff`(enum append) + `ApplyDreamcatcherCardToUnit` 분기: 부착 유닛에
  즉발 공속 +magnitude%(DcDuration, 만료 없음) + duration 초 warmup idle. 자폭 없음.
- Card_SlowAwakening: Squad→Unit 전환. effects[] 비움 / placementWarmupSec 0 /
  mechanics=[{None, SelfWarmupBuff, mag50, dur2}]. description 갱신.
- 무의식(Subconscious) 전용 보랏빛 프레임 — `FrameColorOf`/`ArtFallbackOf`(category 우선 > 타입색),
  덱빌더 카드 그리드 + 상세 팝업 아트폴백.
- 레거시 hostless 영속 apply `BattleBridge.ApplyDreamcatcherCard(handle=0)` **삭제** → 호출부
  (dormant DreamcatcherController.Pick + PlayMode 테스트 3파일)를 revocable `ApplyDreamcatcherCardHosted` 로 이관.

## Key Files
- `Assets/_Project/Scripts/Data/Dreamcatcher/DcMechanic.cs` — SelfWarmupBuff
- `Assets/_Project/Scripts/Bridge/BattleBridge.cs` — Unit-path 분기, hostless 메서드 제거, 스톤 handle=0 주석
- `Assets/_Project/Scripts/UI/Outgame/DreamcatcherDeckBuilderView.cs` — 무의식 프레임 헬퍼
- `Assets/_Project/Data/Dreamcatcher/Card_SlowAwakening.asset` — Unit 전환

## Verified
- compile 클린. EditMode 15/15. PlayMode 8/8(Effect/CombatDamage/DreamstoneCarryIn/DeckCarryIn).
- 아키텍처: 라이브 드림캐쳐에 영속 개념 소거 — 전부 host 종속(Squad/Unit) 또는 일회성(Active).

## Notes
- `_activeDcEffects` 의 handle=0 은 **드림스톤**(ApplyPendingDreamstones, 매치-롱 설계)이 계속 사용 —
  `RevokeDreamcatcherEffects` 의 `handle<=0` 가드는 스톤 보호용, 유지.
- squad `placementWarmupSec` 인프라(_activeWarmups)는 유지되나 현재 이를 쓰는 에셋 0개.

## 정정 (2026-07-10, spec-review H4)
- 위 "Implemented" 의 SelfWarmupBuff 분기는 **실제로는 BattleBridge 에 반영되지 않았다**(플래키
  파일쓰기로 유실). 커밋 `fe4ba372` 시점의 느린 각성은 kind 5 핸들러 부재로 **no-op** 이었다.
  PlayMode 8/8 은 이 경로를 실행하지 않아 놓쳤다. → `dreamcatcher-placement-aura` spec 에서
  PlacementAura(kind 6)로 교체하며 실동작 확보. SelfWarmupBuff(5)는 reserved enum 으로 잔존.

## Follow-up
- ✅ **느린 각성 메커니즘 재설계 완료**: `dreamcatcher-placement-aura` spec — host 미부여, host 생존 중
  axis 매칭 **신규 배치 유닛**에 부여, host 사망 시 회수. (본 spec 의 부착 모델·무의식 프레임은 토대로 재사용.)
- 무의식 프레임 인게임 손패까지 확대(후속).
