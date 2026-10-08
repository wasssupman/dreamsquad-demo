using System.Collections.Generic;

namespace Somnia.Battle.BattleCore
{
    // battle-core-rebuild unit 2 리뷰 F4 — 개체 **부분**의 풀.
    //
    // `Unit` 은 이미 풀에서 빌려 온다. 그런데 부분(이동·감지·어그로·순찰·점유·공격·진행형)은
    // 틱 중에 `new` 로 생겼다 — 어그로 획득은 도발 한 번마다, 스폰은 웨이브마다 돈다.
    // 3분 판에서 그게 수백 번이고, 모바일에서 그 정도 쓰레기는 프레임을 튀게 만든다.
    //
    // ⚠ **부분을 «항상 있는 것»으로 만들어 해결하지 않았다.** `Unit.Move == null` 은 단순한
    // 최적화가 아니라 **아키타입의 표현**이다(「감지 0 = 감지 상태 자체가 없다」 — unit 2 의
    // `UnitParts` 헤더가 그 규칙을 적어 뒀다). 전부 non-null 로 바꾸면 그 문장이 사라지고
    // 「값이 0 인가」라는 약한 술어로 대체된다. 그래서 **부재는 그대로 두고 객체만 돌려쓴다.**
    //
    // ⚠ 빌려줄 때 `Reset()` 된 상태를 보장한다 — 반납할 때 비우는 것이 아니라 **반납 시점에**
    // 비운다. 반납이 곧 마지막 사용 지점이라 거기서 비워야 「죽은 유닛의 값을 나중에 읽는」
    // 경로가 원천적으로 막힌다.
    //
    // ⚠ `ShieldSlots`·`Inbox` 와 unit 6a 의 효과 부분 넷(`ModifierSet`·`CcState`·`DotSet`·
    // `StackSet`)은 여기 없다. 그것들은 **모든 개체가 갖는 것**이라 `Unit` 이 자기 필드로
    // 한 개씩 들고 `Reset` 이 비우기만 한다(부재가 뜻을 갖지 않는다). 효과를 nullable 로
    // 두면 「걸 수 있나」가 부착 상태에 매여, 부여 지점마다 `if (u.Modifiers == null)` 가
    // 생기고 그중 하나가 언젠가 조용히 빠진다.
    public sealed class UnitPartPool
    {
        private readonly Stack<MoveState> _move = new Stack<MoveState>(32);
        private readonly Stack<Detection> _detection = new Stack<Detection>(16);
        private readonly Stack<Aggro> _aggro = new Stack<Aggro>(16);
        private readonly Stack<Patrol> _patrol = new Stack<Patrol>(8);
        private readonly Stack<Footprint> _footprint = new Stack<Footprint>(16);
        private readonly Stack<AttackState> _attack = new Stack<AttackState>(32);
        private readonly Stack<ProgressiveStates> _progressive = new Stack<ProgressiveStates>(8);
        private readonly Stack<Effects.ProjectileImbueSet> _imbue = new Stack<Effects.ProjectileImbueSet>(8);

        public MoveState RentMove() => _move.Count > 0 ? _move.Pop() : new MoveState();
        public Detection RentDetection() => _detection.Count > 0 ? _detection.Pop() : new Detection();
        public Aggro RentAggro() => _aggro.Count > 0 ? _aggro.Pop() : new Aggro();
        public Patrol RentPatrol() => _patrol.Count > 0 ? _patrol.Pop() : new Patrol();
        public Footprint RentFootprint() => _footprint.Count > 0 ? _footprint.Pop() : new Footprint();
        public AttackState RentAttack() => _attack.Count > 0 ? _attack.Pop() : new AttackState();
        public ProgressiveStates RentProgressive()
            => _progressive.Count > 0 ? _progressive.Pop() : new ProgressiveStates();

        /// <summary>unit 6a2 — 탄 부여 슬롯. 부여받은 개체만 갖는다(부재가 뜻을 갖는다).</summary>
        public Effects.ProjectileImbueSet RentImbue()
            => _imbue.Count > 0 ? _imbue.Pop() : new Effects.ProjectileImbueSet();

        /// <summary>
        /// 개체가 들고 있던 부분을 전부 회수하고 필드를 비운다. **`Unit.Reset` 의 본체**다 —
        /// 「부분이 늘면 `Unit.Reset` 도 같이 고친다」는 경고가 이제 이 함수를 가리킨다.
        /// </summary>
        public void Reclaim(Unit u)
        {
            if (u.Move != null) { u.Move.Reset(); _move.Push(u.Move); u.Move = null; }
            if (u.Detection != null) { u.Detection.Reset(); _detection.Push(u.Detection); u.Detection = null; }
            if (u.Aggro != null) { u.Aggro.Reset(); _aggro.Push(u.Aggro); u.Aggro = null; }
            if (u.Patrol != null) { u.Patrol.Reset(); _patrol.Push(u.Patrol); u.Patrol = null; }
            if (u.Footprint != null) { u.Footprint.Reset(); _footprint.Push(u.Footprint); u.Footprint = null; }
            if (u.Attack != null) { u.Attack.Reset(); _attack.Push(u.Attack); u.Attack = null; }
            if (u.Progressive != null)
            {
                u.Progressive.Reset();
                _progressive.Push(u.Progressive);
                u.Progressive = null;
            }
            if (u.Imbue != null) { u.Imbue.Reset(); _imbue.Push(u.Imbue); u.Imbue = null; }
        }
    }
}
