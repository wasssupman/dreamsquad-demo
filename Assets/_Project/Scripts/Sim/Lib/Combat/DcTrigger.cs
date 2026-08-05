namespace Wassup.Sim.Combat
{
    /// <summary>
    /// battle-sim-extraction unit 18-G/2 — 드림캐쳐 발동 판정 순수 함수.
    /// 구 `Wassup.Battle.Combat.DcTrigger` 이식 — **본문은 한 글자도 바꾸지 않았다**
    /// (타입만 sim 어휘로). 이 조각은 원래부터 엔진을 몰랐다.
    ///
    /// 여섯 함수 전부 **함수 내부 가드**를 갖는다(kind 디스패치에 맡기지 않는다). 값 누락
    /// 카드(period 0 / periodSeconds 0 / fraction 0 / 미베이크 maxHpRef 0)가 매 틱 스핀-발동
    /// 하는 것을 막는 자리이고, 저작 검증이 이미 거절하더라도 **여기서 한 번 더** 막는다.
    ///
    /// ⚠ 카운터를 소유하지 않는다 — `ref` 로 받아 굴릴 뿐이고, 어느 시스템이 그 필드를 쓰는지는
    /// <see cref="DcTriggerSlot"/> 의 "쓰기 소유" 표가 정본이다.
    /// </summary>
    public static class DcTrigger
    {
        /// <summary>
        /// 공격 RESOLVE 한 번을 센다. N 번째에 true 를 돌려주며 카운터를 리셋한다.
        /// `period == 0` 은 발동하지 않는다(가드).
        /// </summary>
        public static bool Tick(ref ushort counter, ushort period)
        {
            if (period == 0) return false;
            counter++;
            if (counter < period) return false;
            counter = 0;
            return true;
        }

        /// <summary>
        /// 비변이 peek — **다음** <see cref="Tick"/> 이 발동하는가?
        /// 강공 pre-scan 이 카운터를 건드리기 전에 "이번 공격이 N 번째인가" 를 정하는 데 쓴다.
        ///
        /// ⚠ 발동 조건이 `Tick` 과 **정확히 일치**해야 한다(`period != 0 && counter + 1 >= period`).
        /// 어긋나면 pre-scan 예측과 실제 발화가 갈려 강공 배율이 엉뚱한 공격에 실린다 —
        /// 그 합성 불변식은 테스트가 period·게이트 조합 전수로 고정한다.
        /// </summary>
        public static bool WouldFire(ushort counter, ushort period)
            => period != 0 && counter + 1 >= period;

        /// <summary>
        /// 주기 트리거 누산기. 잔여를 이월해 drift 가 없다.
        ///
        /// ⚠ `periodSeconds &lt;= 0` 은 **발동하지도, 누적하지도** 않는다 — 누적만 시켜두면
        /// 나중에 값이 채워지는 순간 밀린 만큼이 한꺼번에 터진다.
        /// 틱당 최대 1발: 랙 스파이크로 여러 주기가 적립되면 다음 틱들에 한 발씩 흘린다.
        /// </summary>
        public static bool PeriodicTick(ref float elapsed, float dt, float periodSeconds)
        {
            if (periodSeconds <= 0f) return false;
            elapsed += dt;
            if (elapsed < periodSeconds) return false;
            elapsed -= periodSeconds;
            return true;
        }

        /// <summary>
        /// 체력 임계 — hp 가 다음 경계 `maxHpRef·(1 − k·fraction)` 아래로 내려가면 발동.
        /// k 는 베이크 시 1.
        ///
        /// 세 가지가 계약이다:
        /// <list type="bullet">
        /// <item><b>strict `&lt;`</b> — 경계 위에 정확히 앉아 있으면 발동하지 않는다.</item>
        /// <item><b>다중 관통 = 1발</b> — 큰 한 방이 여러 경계를 뚫으면 k 는 최심으로 가되
        ///       보고는 **한 번**이다(한 틱 다중 텔레포트 방지).</item>
        /// <item><b>단조 래치</b> — 힐로 경계 위로 올라가도 k 는 되감기지 않는다(핑퐁 익스플로잇 차단).</item>
        /// </list>
        /// 종료 보장: k++ 가 경계를 단조 하강시키고 hp ≥ 0 이므로 루프는 반드시 끝난다.
        /// </summary>
        public static bool HealthThresholdEval(float hp, float maxHpRef, float fraction, ref int nextBoundaryIndex)
        {
            if (fraction <= 0f || maxHpRef <= 0f) return false;
            bool fired = false;
            while (hp < maxHpRef * (1f - nextBoundaryIndex * fraction))
            {
                nextBoundaryIndex++;
                fired = true;
            }
            return fired;
        }

        /// <summary>
        /// 게이트 판정. 조립 계약은 `if (Pass) { if (Tick()) fire; }` — **게이트 실패 사건은
        /// counter 를 움직이지 않는다**(카운트 게이트).
        ///
        /// ⚠ `HpBelow` 는 `&lt;=` 다 — 정확히 경계값(30.0%)이면 통과.
        /// <see cref="HealthThresholdEval"/> 의 strict `&lt;` 와 **방향이 다르니 섞지 말 것**.
        /// 판정 기준도 다르다 — 여기는 **현재 max**, 저기는 스폰 스냅샷.
        ///
        /// 주어가 소멸했거나 죽었으면 caller 가 게이트 실패로 취급한다(이 함수는 모른다).
        /// </summary>
        public static bool GatePass(DcGateKind gate, float gateValue, float subjectHp, float subjectMaxHp)
        {
            switch (gate)
            {
                case DcGateKind.None: return true;
                case DcGateKind.HpBelow:
                    if (gateValue <= 0f || subjectMaxHp <= 0f) return false; // 무값 카드/미베이크 가드
                    return subjectHp <= subjectMaxHp * gateValue;
                default: return false;
            }
        }

        /// <summary>
        /// 게이트 배선 표의 **단일 source of truth**. v1 배선은 둘뿐 —
        /// ① `OnDamagedN × Self`(궁지폭발) ② `AttackN × EventTarget`(처형타).
        ///
        /// 그 외 `gate != None` 조합은 bake 가 이 함수를 보고 loud 거절한다. 이유는 둘 —
        /// **퇴화**(OnDeath×HpBelow 는 사망 시 항상 참) 와 **미배선**(주어 규칙 미정).
        /// 미래 gate enum 도 명시 배선 전까지 거절된다(`default` 없는 화이트리스트).
        /// 새 조합은 카드 + 배선 + 테스트 한 묶음으로만 연다.
        /// </summary>
        public static bool GateComboSupported(DcTriggerKind trigger, DcGateKind gate, DcGateSubject subject)
        {
            if (gate == DcGateKind.None) return true;
            if (gate != DcGateKind.HpBelow) return false;
            if (trigger == DcTriggerKind.OnDamagedN && subject == DcGateSubject.Self) return true;
            if (trigger == DcTriggerKind.AttackN && subject == DcGateSubject.EventTarget) return true;
            return false;
        }
    }
}
