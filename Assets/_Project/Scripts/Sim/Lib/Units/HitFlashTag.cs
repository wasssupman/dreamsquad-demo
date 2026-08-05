namespace Wassup.Sim.Units
{
    /// <summary>
    /// battle-sim-extraction unit 18-H/3 — 피격 시 붙는 일시 표식. 구 `HitFlashTag` 이식.
    ///
    /// `remaining` 이 흐르는 동안 유닛을 잠깐 키웠다가 되돌린다. 틱과 스케일 복원은 #24 의
    /// 몫이고(18-J), 여기서는 **부여만** 한다.
    ///
    /// ⚠ 뷰성 상태지만 `SimTransform.Scale` 을 쓰기 때문에 **sim 안에 산다** — 구 sim 이
    /// 그랬고, 스케일이 상태 라인에 실리는 한 뷰로 밀 수 없다(살베지 판정 대상 — 18-K).
    ///
    /// ⚠ 연속 피격은 **타이머만 갱신**하고 `originalScale` 은 보존한다. 덮어쓰면 이미 부푼
    /// 스케일이 새 원본이 되어 유닛이 영구히 커진다.
    /// </summary>
    public struct HitFlashTag
    {
        public float remaining;
        public float duration;
        public float originalScale;
    }
}
