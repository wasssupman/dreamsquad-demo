namespace Somnia.Battle.BattleCore.Trigger
{
    /// <summary>규칙 하나가 「왜 안 터졌나」 — tools.md 「트리거 강제 발화」의 네 원인 + 터지고 있음.</summary>
    public enum BindingStatus : byte
    {
        /// <summary>이 판에 한 번 이상 터졌고 아직 더 터질 수 있다.</summary>
        Firing = 0,
        /// <summary>그 규칙이 듣는 사실을 이 판의 감지자가 한 번도 올리지 않았다(부착 즉시 규칙은 부착 문이 안 열렸다).</summary>
        NoDetector = 1,
        /// <summary>사실은 올라왔는데 조건(카운터 N · 게이트 · 주기 · 경계 · 주어 필터)이 아직 안 찼다.</summary>
        ConditionNotMet = 2,
        /// <summary>발동 상한(`FireCap`)을 다 썼다.</summary>
        FireCapSpent = 3,
        /// <summary>떨어졌다(수명 만료 · 주인 소멸 · 회수). 사유는 `BindingDetached` 사건이 나른다.</summary>
        Detached = 4,
    }

    // battle-core-rebuild unit 7d — **「왜 안 터졌나」의 판정은 코어가 한다.** 도구(`CoreTriggerDebugMenu`)는 이 답을
    // 찍기만 한다 — 도구가 원인을 재구성하면 규칙이 바뀔 때 도구만 옛 답을 낸다.
    //
    // 순서가 뜻이다: 떨어진 규칙은 상한·조건을 물을 필요가 없고, 상한을 다 쓴 규칙은 사실이 와도 안 터진다.
    // 감지자 유무는 **관측**(`TriggerDispatcher.SensedCount`)이다 — 「이 종류를 올리는 감지자가 코드에 있나」를
    // 목록으로 적어 두면 감지자가 늘 때 이 표만 낡는다.
    public static class BindingDiagnosis
    {
        public static BindingStatus Diagnose(Binding b, TriggerDispatcher dispatcher)
        {
            if (b.Detached) return BindingStatus.Detached;
            ref var d = ref b.Def;
            if (d.FireCap > 0 && b.FireCount >= d.FireCap) return BindingStatus.FireCapSpent;
            if (b.FireCount > 0) return BindingStatus.Firing;
            // 부착 즉시(`None`)는 감지자가 아니라 부착 지점이 발화시킨다 — 한 번도 안 났다면 그 문이 안 열렸다.
            if (d.Trigger == TriggerKind.None) return BindingStatus.NoDetector;
            if (!TriggerDispatcher.IsPolled(d.Trigger) && dispatcher.SensedCount(d.Trigger) == 0)
                return BindingStatus.NoDetector;
            return BindingStatus.ConditionNotMet;
        }
    }
}
