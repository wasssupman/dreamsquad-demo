using UnityEngine;
using Wassup.Battle.Units;
using Wassup.BattleCore;
using Wassup.Core;
using Wassup.Data;

namespace Wassup.BattleCoreUnity
{
    // battle-core-rebuild unit 5c — **전투가 내는 소리 셋.**
    //
    // 옛 전투에서 이 셋은 브리지 드레인 안에 인라인으로 박혀 있었다(`:4840` 공격 ·
    // `:5453` 발사 · `:7797` 배치). 여기로 옮기면서 바뀐 것은 **「누가 부르나」뿐**이다 —
    // 클립도, 볼륨도, 스로틀도 전부 있던 자리에 그대로 있다.
    //
    // ⚠ **뷰가 클립을 고르지 않는다.** 유닛마다 다른 소리는 그 유닛의 저작
    // (`DefenderUnitData.attackSfxClip`·`deployVoiceClip`)에서 오고, 판 전체가 공유하는
    // 소리(발사·배치 폴백)는 `SoundManager` 의 저작에서 온다. 코어 정의표는 `AudioClip` 을
    // 들 수 없으므로(계약 4) **줄 번호로 되찾는다** — 5a 가 스켈레톤·시트를 되찾는 것과
    // 같은 길이고, 그 번호를 매긴 쪽과 목록을 내놓는 쪽이 같아야 한다는 규칙도 같다.
    //
    // ⚠ **`SoundManager` 는 의도된 매니저 예외**다(제약 5 · 전역 SFX). 새 층이 매니저를
    // 두지 않는다는 절대 제약과 충돌하지 않는 이유: 이 컴포넌트는 규칙도 상태도 안 들고
    // 사건을 소리로 번역만 한다. 판정이 여기 들어오면 그것이 새 브리지의 첫 줄이다.
    [DisallowMultipleComponent]
    public sealed class CoreBattleAudio : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        // 「사건당 한 번」을 증언하는 계수기. 소리는 단언할 수 없지만 **호출은 셀 수 있다**.
        public int AttackCues { get; private set; }
        public int FireCues { get; private set; }
        public int PlaceCues { get; private set; }

        private void OnEnable()
        {
            AttackCues = 0;
            FireCues = 0;
            PlaceCues = 0;
            if (_driver != null) _driver.Subscribe(ViewOrder.Audio, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.AttackResolved:
                    // 공격 실행음은 **방어유닛 저작에만** 있다(적은 클립이 없어 옛 전투도 무음이었다).
                    // 진영으로 거르는 이유는 소리가 아니라 **표**다 — `DefIndex` 가 가리키는 표가
                    // 진영마다 다르므로(적이면 적 표), 안 거르면 엉뚱한 유닛의 클립을 집는다.
                    if ((e.Faction & Faction.DefenderUnit) == 0) return;
                    AttackCues++;
                    // 투사체 유닛은 클립 미할당 → `PlayAttack` 이 무음으로 흘린다(옛 거동 그대로).
                    SoundManager.Instance?.PlayAttack(DefenderOf(e.DefIndex)?.attackSfxClip);
                    return;

                case CoreEventKind.ProjectileSpawned:
                    // 발사음도 방어유닛 탄만. 적 원거리 공격이 같은 사건을 쓰므로(옛 드레인도
                    // 공유했다) 쏜 쪽의 진영으로 거른다.
                    if ((e.Faction & Faction.DefenderUnit) == 0) return;
                    FireCues++;
                    SoundManager.Instance?.PlayProjectileFire();
                    return;

                case CoreEventKind.Placed:
                    // `Arg` = 정의표 줄 번호. 유닛별 배치 보이스가 없으면 `PlayDeployPlace` 가
                    // 통합 폴백(`deployPlaceClip`)을 쓴다 — 그 폴백 규칙은 사운드의 것이다.
                    PlaceCues++;
                    SoundManager.Instance?.PlayDeployPlace(DefenderOf(e.Arg)?.deployVoiceClip);
                    return;
            }
        }

        private DefenderUnitData DefenderOf(int defIndex)
        {
            if (_driver == null || defIndex < 0) return null;
            var units = _driver.DefenderAssets;
            return defIndex < units.Count ? units[defIndex] : null;
        }
    }
}
