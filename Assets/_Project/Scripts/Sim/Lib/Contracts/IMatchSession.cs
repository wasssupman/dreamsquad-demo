using System;
using System.Collections.Generic;

namespace Wassup.Core.Session
{
    // battle-sim-extraction unit 12 — 한 판의 세션 계약(청사진 ① 전체).
    //
    // 구현 4종이 이 표면 하나를 공유하는 것이 목적이다: Local(인프로세스) · Remote(M3) ·
    // Replay(M2, seek) · Ghost(필터드 프로젝션). 스왑 = 구현체 교체 1곳(ADR D4).
    //
    // **제약 8 예외 근거**: "인터페이스는 구현체 2개 이상일 때만" 이지만 unit 12 시점 구현체는
    // LegacyMatchSessionAdapter 1개다. 이 인터페이스는 "나중을 위한 추상"이 아니라
    // **CLAUDE.md 제약 1(BattleBridge 유일 창구)의 후계 불변식**이고(spec README 이행표),
    // 구현 4종이 설계 정본 §1 에 로드맵으로 확정돼 있다. 이행의 수단이므로 예외로 둔다.
    //
    // 서버는 아직 아무것도 정하지 않았다 — 이 계약은 "서버가 나중에 받아도 되는 모양"까지만이고
    // 전송·직렬화 포맷·서버 스택은 M3 의 결정이다(설계 정본 결정 #5).
    public interface IMatchSession
    {
        // 세션이 살아 있는가. Dispose 후 false.
        bool IsActive { get; }

        // 현재 tick 의 읽기 모델. 뷰는 매 프레임 이것만 본다.
        MatchReadModel ReadModel { get; }

        // 커맨드 수락/거절. 같은 ClientSeq 재전송은 재실행 없이 같은 receipt 를 돌려준다(멱등).
        // 순번 갭 처리는 **전송 채널의 순서 보장 여부가 정한다**: 인프로세스(순서 보장)에서 갭은
        // 곧 호출자 버그이므로 즉시 `Session_SeqGap` 거절이고 — `LegacyMatchSessionAdapter` 가
        // 그렇다 — 비순서 채널(M3)에서는 세션이 재정렬 버퍼를 소유해 보류 + 타임아웃 후 거절한다
        // (청사진 ① §3 의 jitter 충돌 회피). 구현체는 자기 채널에 맞는 쪽을 고르고 근거를 적는다.
        CommandReceipt SendCommand(in MatchCommand command);

        // 다음에 보낼 커맨드의 `ClientSeq`. **순번 소유가 세션에 있는 이유**(리뷰 #1): 호출자
        // 쪽에 카운터를 두면 두 값이 어긋날 수 있고, 어긋나면 갭 분기가 기대값을 전진시키지 않아
        // **재수렴이 불가능**하다 — 그 뒤 모든 커맨드가 거절되는데 receipt 를 보는 호출부가 없어
        // 콘솔이 깨끗한 채로 입력 전체가 죽는다. 세션이 자기 기대값을 내주면 그 상태가 생기지
        // 않는다. 호출자는 이 값을 받아 커맨드를 만들고 즉시 보낸다(받아두고 안 보내면 갭이다 —
        // 그래서 `MatchSession.Send` 가 두 동작을 한 곳에 묶는다).
        uint NextClientSeq();

        // 이번 tick 에 발생한 이벤트. semantic 과 presentation 이 섞여 오고 소비자가
        // IsPresentation 으로 가른다. 내부 phase queue(9채널)는 여기 나타나지 않는다.
        IReadOnlyList<SessionEvent> DrainEvents();

        // 레인별 첫 스폰 시각(초). 예보가 없으면 false — 전투 종료·재시작·남은 스폰 없음.
        //
        // **배열이 아니라 span 인 이유**(unit 13-A2): 구 `BattleBridge.TryGetSpawnAlertForecast` 는
        // 내부 캐시 배열 `_spawnAlertForecast` **참조를 그대로** 넘겨, 뷰가 그것을 통해 sim 상태를
        // 쓸 수 있었다. `ReadOnlySpan` 은 쓰기를 **컴파일러가** 막고 `float[]` 로 되돌리는 캐스팅
        // 우회도 불가하며 복사 할당도 없다(청사진 ① §6 이 지정한 "복사본/read-only" 중 후자).
        //
        // 유효 범위는 **호출한 프레임 안**이다 — 세션이 다음 tick 에 내용을 갱신할 수 있으므로
        // 필드에 저장하지 않는다. 값이 필요하면 그 프레임에 읽어 쓴다.
        bool TryGetSpawnAlertForecast(out ReadOnlySpan<float> laneFirstSpawnSec);

        // 유닛 배치 쿨타임. false = 쿨타임 아님(remaining <= 0). fraction 은 남은 비율(1→0).
        //
        // **키가 `unitDefId` 문자열인 이유**: 구 `PlacementCooldownRuntime` 은
        // `DefenderUnitData`(ScriptableObject) 인스턴스로 키잉한다. 계약에 엔진 타입을 넣지 않는
        // 원칙(`SimCell` 이 `int2` 를 대신하는 것과 같은 이유)이라 문자열 id 로 좁히고, id→정의
        // 해석은 구현체가 소유한다. 활성 쿨타임이 하나도 없는지는 `ReadModel.AnyPlacementCooldown`
        // 으로 먼저 걸러 전 슬롯 순회를 건너뛴다.
        bool TryGetPlacementCooldown(string unitDefId, out float remaining, out float fraction);

        // 매치 종료 통지. outcome 4종. 구독자는 결과·집계 화면.
        event Action<MatchOutcome> MatchEnded;

        // 세션 파기(이탈 = 커맨드가 아니라 파기). restart 는 Dispose → Create(같은 config).
        void Dispose();
    }
}
