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
        // 순번 갭은 즉시 거절이 아니라 보류 + 타임아웃 후 Session_SeqGap(청사진 ① §3 — jitter 와의
        // 충돌 회피). 전송 채널이 순서를 보장하지 않으면 세션이 재정렬 버퍼를 소유한다.
        CommandReceipt SendCommand(in MatchCommand command);

        // 이번 tick 에 발생한 이벤트. semantic 과 presentation 이 섞여 오고 소비자가
        // IsPresentation 으로 가른다. 내부 phase queue(9채널)는 여기 나타나지 않는다.
        IReadOnlyList<SessionEvent> DrainEvents();

        // 매치 종료 통지. outcome 4종. 구독자는 결과·집계 화면.
        event Action<MatchOutcome> MatchEnded;

        // 세션 파기(이탈 = 커맨드가 아니라 파기). restart 는 Dispose → Create(같은 config).
        void Dispose();
    }
}
