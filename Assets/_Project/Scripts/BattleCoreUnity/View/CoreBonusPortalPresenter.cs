using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Somnia.Battle.BattleCore;
using Somnia.Battle.BattleCore.Wave;

namespace Somnia.Battle.BattleCoreUnity.View
{
    // battle-core-rebuild unit 8a — **보너스 포탈**. 옛 `Bridge/BattleBridge.BonusWave.cs:161~167·234~258`
    // (`ForceBonusWave` 의 열림·닫힘 시각 + `OpenBonusPortals`·`ClearBonusPortalViews`)의 뷰 몫이다.
    // 장부 bridge-fields 1 `bonusPortalPrefab` 의 새 주인.
    //
    //   열림 = 당김 + `portalAppearDelaySec`
    //   닫힘 = 마지막 스폰 + `portalLingerSec`
    //
    // ⚠ **시각은 판의 시계다**(`MatchClock.BattleTime` — 틱 × 1/60). `Time` 이면 슬로모·정지에서
    // 포탈과 스폰이 갈린다(옛 `BonusWave.cs:164` 주석). 코어는 틱 발행률로만 느려지므로 이 뷰도
    // 그 시계를 읽으면 저절로 같이 느려진다.
    //
    // ⚠ **마지막 스폰 시각을 다시 계산하지 않는다.** 코어가 스폰 예약에 쓴 순수 함수
    // (`BonusWaveSchedule.Build`)를 **같은 입력으로 호출만** 한다 — 자를 새로 만들면 두 곳이 갈린다.
    // `portalLingerSec` 는 코어 정의표에 없다(뷰 타이밍이 `configHash` 에 들어가면 안 된다) —
    // `BonusWaveData` 에서 이 뷰가 직접 읽는다.
    //
    // 풀이 아니라 **보너스 웨이브 수명**이다(옛 것과 같다 — 거점 뷰 선례). 판 경계에서 회수한다.
    [DisallowMultipleComponent]
    public sealed class CoreBonusPortalPresenter : MonoBehaviour
    {
        [SerializeField] private BattleDriver _driver;

        [Tooltip("보너스 포탈 프리팹. 옛 브리지 필드 `bonusPortalPrefab`(bridge-fields 1)의 새 주인. 비우면 포탈 없이 스폰만 일어난다.")]
        [SerializeField] private GameObject _portalPrefab;

        private readonly List<GameObject> _views = new List<GameObject>();
        private bool _active;
        private bool _opened;
        private float _openAt;
        private float _closeAt;

        /// <summary>지금 열려 있는 포탈 수. 「열림/닫힘 틱」 증언 창.</summary>
        public int OpenCount => _views.Count;

        /// <summary>이번 보너스 웨이브의 열림·닫힘 시각(판의 시계, 초). 진행 중이 아니면 둘 다 0.</summary>
        public float OpenAtSec => _active ? _openAt : 0f;
        public float CloseAtSec => _active ? _closeAt : 0f;

        private void OnEnable()
        {
            if (_driver != null) _driver.Subscribe(ViewOrder.Board, OnCoreEvent);
        }

        private void OnDisable()
        {
            if (_driver != null) _driver.Unsubscribe(OnCoreEvent);
            Clear();
        }

        private void OnCoreEvent(CoreEvent e)
        {
            switch (e.Kind)
            {
                case CoreEventKind.MatchStarted: Clear(); break;
                case CoreEventKind.BonusPulled: Begin(); break;
            }
        }

        // 당김 사건은 커맨드 적용 **그 자리**에서 배달된다(`BattleDriver.Apply` → 드레인). 그래서
        // 지금 판의 시계가 곧 코어가 스폰 예약에 쓴 `now` 다.
        private void Begin()
        {
            Clear();
            var match = _driver.Match;
            ref var bonus = ref match.Definition.Bonus;
            var portals = match.Map.Snapshot.BonusSpawns;
            var entries = BonusWaveSchedule.Build(
                portals.Length, bonus.EnemyCount, bonus.FirstSpawnAtSec, bonus.SpawnIntervalSec);
            if (entries.Length == 0) return;

            var data = _driver.BonusAuthoring;
            float now = match.Clock.BattleTime;
            _openAt = now + bonus.PortalAppearDelaySec;
            _closeAt = now + entries[entries.Length - 1].SpawnAtSec + (data != null ? data.portalLingerSec : 0f);
            _active = true;
            _opened = false;
        }

        private void Update()
        {
            if (!_active || _driver == null || !_driver.Running) return;
            float t = _driver.Match.Clock.BattleTime;

            if (!_opened && t >= _openAt)
            {
                _opened = true;
                Open();
            }
            // 마지막 스폰 + linger → 포탈을 닫는다. 적이 아직 살아 있어도 «웨이브» 는 끝난 것이다.
            if (t >= _closeAt) Clear();
        }

        private void Open()
        {
            DestroyViews();
            if (_portalPrefab == null) return;
            var portals = _driver.Match.Map.Snapshot.BonusSpawns;
            float tile = _driver.TileSize;
            for (int i = 0; i < portals.Length; i++)
            {
                int2 cell = portals[i];
                // sim 셀 중심 → **뷰** 월드. 평면 보드라 sim 좌표를 그대로 쓰면 어긋난다.
                var world = (Vector3)Somnia.Battle.Core.BoardSpace.ToView(new float3(cell.x * tile, 0f, cell.y * tile));
                var go = Instantiate(_portalPrefab, world, Quaternion.identity, transform);
                go.name = $"BonusPortal_{cell.x}_{cell.y}";
                _views.Add(go);
            }
        }

        private void Clear()
        {
            _active = false;
            _opened = false;
            _openAt = 0f;
            _closeAt = 0f;
            DestroyViews();
        }

        private void DestroyViews()
        {
            for (int i = 0; i < _views.Count; i++)
                if (_views[i] != null) Destroy(_views[i]);
            _views.Clear();
        }
    }
}
