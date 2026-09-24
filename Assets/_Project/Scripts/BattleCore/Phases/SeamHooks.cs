using System;

namespace Wassup.BattleCore
{
    // battle-core-rebuild unit 3 — 트리거 레이어가 들어올 **자리**. unit 6b2 가 `Periodic`(4), unit 7a 가 `Immediate`(5) 를 append 했다.
    //
    // unit 3 은 훅을 **뚫기만** 했고(「seam 은 unit 7 이 채운다」), unit 7a 의 `TriggerDispatcher` 가
    // seam 마다 핸들러 하나를 등록한다(`BattleMatch` 조립 시점).
    //
    // ⚠ **왜 지금 만드나**(제약 8 「나중을 위한 추상 레이어 금지」와의 관계):
    // seam 은 추상 레이어가 아니라 **순서 계약**이다. 「피해 뒤·소멸 전」처럼 사건이 끼어들 수
    // 있는 구간은 옛 전투에서 `[UpdateAfter]`/`[UpdateBefore]` 네 건이 들고 있던 **게임 규칙**
    // 이고(C18), 그 구간을 나중에 «찾아서» 뚫으면 위치가 조용히 달라진다. 그래서 위치를 지금
    // 코드로 고정하고, **내용만** unit 7 이 채운다.
    // 반대로 **추상은 하나도 만들지 않았다** — 인터페이스도, 등록 DSL 도, 우선순위 정렬도 없다.
    // 핸들러는 `Action<TickContext>` 하나이고 배열은 조립 시점에 한 번 채워진다.
    //
    // ⚠ 등록은 **틱 밖**(조립 시점)에서만 한다. 틱 중에 등록하면 순회 중 배열이 바뀐다.
    public enum Seam : byte
    {
        /// <summary>공격이 성사된 직후. 공격 구동 효과(강공·부착 카드)가 여기 붙는다.</summary>
        Attack = 0,
        /// <summary>피해가 적용되고 사망이 표시된 직후. **시체는 아직 판 위에 있다.**</summary>
        Death = 1,
        /// <summary>소멸 직후. 자리는 소멸 사건이 값으로 나른다(개체는 이미 없다).</summary>
        Lifecycle = 2,
        /// <summary>체력 경계(임계)를 넘은 직후. 도약·순간이동이 이 뒤에 온다.</summary>
        Threshold = 3,
        /// <summary>
        /// unit 6b2 — **주기마다 무슨 일이 일어난다**의 자리. 장 준비(`FieldPrepPhase`) 끝에서 매 틱
        /// 돈다. unit 7 의 주기 바인딩(레드불 주기 · 온천 열기)과 배치 엣지(`OnPlace`)가 여기 붙는다.
        /// ⚠ **번호(4)는 틱 안의 실행 순서가 아니다** — 이 seam 은 `Attack`(0) 보다 **앞**에서 돈다.
        /// 번호는 append-only 의 몫이고 순서의 몫이 아니다(7a 의 `SeamTickOrder`).
        /// ⚠ 번아웃 피로는 여기서 스택을 **더하지 않는다** — `GimmickStacks.RequestFatigue`(스탯 적용
        /// 뒤 단계가 소비)로 요청만 넣는다. 여기서 곧바로 더하면 1틱 지연이 사라진다.
        /// </summary>
        Periodic = 4,
        /// <summary>
        /// unit 7a — **커맨드의 콜스택 안.** 부착·액티브·퇴근은 동기 트랜잭션이라 큐에 넣고 틱을 기다리면
        /// 소모(차감·쿨다운) 뒤에 실행이 도착한다. 그래서 이 seam 은 **자기 순서를 갖지 않고**
        /// `CommandPhase.Execute` 가 커맨드를 적용한 그 자리에서 돈다(틱 파이프라인의 0번 자리는 그 몫이다).
        /// </summary>
        Immediate = 5,

        /// <summary>종류 수. 종류가 아니다 — 배열 크기가 이 값에서 나온다.</summary>
        _Count,
    }

    public sealed class SeamHooks
    {
        private static readonly Action<TickContext>[] Empty = Array.Empty<Action<TickContext>>();

        private readonly Action<TickContext>[][] _handlers;

        public SeamHooks()
        {
            _handlers = new Action<TickContext>[(int)Seam._Count][];
            for (int i = 0; i < _handlers.Length; i++) _handlers[i] = Empty;
        }

        /// <summary>조립 시점 등록. 순서는 등록 순서다(구독 순서가 계약인 `EventBus` 와 같은 규율).</summary>
        public void Register(Seam seam, Action<TickContext> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            int i = (int)seam;
            var old = _handlers[i];
            var next = new Action<TickContext>[old.Length + 1];
            Array.Copy(old, next, old.Length);
            next[old.Length] = handler;
            _handlers[i] = next;
        }

        public int CountAt(Seam seam) => _handlers[(int)seam].Length;

        /// <summary>핸들러가 0 이면 아무 일도 안 한다 — 오늘의 전부다.</summary>
        public void Run(Seam seam, TickContext ctx)
        {
            var list = _handlers[(int)seam];
            for (int i = 0; i < list.Length; i++) list[i](ctx);
        }
    }
}
