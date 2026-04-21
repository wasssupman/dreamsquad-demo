using System;
using System.Collections.Generic;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Logging;

namespace Wassup.Core
{
    // Owns one DraftSession at a time and bridges it to BattleBridge. It is a plain
    // MonoBehaviour placed in the scene — no static Instance, since GameManager is
    // the only allowed singleton (CLAUDE.md). UI layers call TogglePick/TryConfirm;
    // BeginDraft is invoked by GameManager or ResultScreen-triggered code paths.
    public class DraftController : MonoBehaviour
    {
        [SerializeField] private DefenderUnitData[] catalog;
        [SerializeField] private int poolSize = 10;
        [SerializeField] private int pickCount = 7;
        [SerializeField] private BattleBridge battleBridge;
        [SerializeField] private SkillData[] defaultSkillLoadout;

        private readonly DraftSession _session = new();

        public DraftSession Session => _session;
        public int PoolSize => poolSize;
        public int PickCount => pickCount;
        public IReadOnlyList<DefenderUnitData> Catalog => catalog;
        public MapGenerationOptions SelectedMapGenerationOptions { get; private set; } = MapGenerationOptions.Default;
        public MapPathShape SelectedMapPathShape => SelectedMapGenerationOptions.pathShape;

        public event Action DraftStarted;
        public event Action DraftConfirmed;

        public void BeginDraft()
        {
            BeginDraft(GenerateSeed());
        }

        public void BeginDraft(int seed)
        {
            if (catalog == null || catalog.Length < poolSize)
            {
                Debug.LogError(
                    $"[DraftController] catalog needs at least {poolSize} entries (has {(catalog?.Length ?? 0)}).",
                    this);
                return;
            }
            _session.Reset(catalog, poolSize, pickCount, seed);

            // Phase 7: BeginDraft is the "new draft" entry point (initial start
            // and Redraft). Roll a fresh skill loadout here so the DraftView can
            // display the 2 skills alongside the unit pool. Restart skips this
            // path and therefore keeps its prior loadout.
            var loadout = GameManager.Instance?.SkillLoadout;
            if (loadout != null)
            {
                if (loadout.Pool.Count == 0 && defaultSkillLoadout != null && defaultSkillLoadout.Length > 0)
                    loadout.Configure(defaultSkillLoadout);
                loadout.Roll();
                Debug.Log($"[DraftController] Roll 완료: Picked={loadout.Picked.Count}", this);
            }

            DraftStarted?.Invoke();
        }

        public bool TogglePick(DefenderUnitData unit) => _session.TogglePick(unit);

        // Finalises the current session. If picks are incomplete this is a no-op.
        // On success pushes the 7-pick array into BattleBridge, triggers StartBattle,
        // then emits DraftConfirmed for any UI layer listening for the hide signal.
        public bool TryConfirm()
        {
            if (!_session.IsFull) return false;

            // Record the full pool + picks + seed before StartBattle so the log
            // entry produced by BattleLogger.EndSession captures the draft even
            // when the session is short-circuited by an immediate defeat.
            var logger = GameManager.Instance?.Logger;
            if (logger != null)
            {
                var record = new DraftRecord { seed = _session.Seed };
                foreach (var u in _session.Pool) if (u != null) record.pool.Add(u.displayName);
                foreach (var u in _session.Picked) if (u != null) record.picked.Add(u.displayName);
                logger.SetDraft(record);
            }

            if (battleBridge != null)
            {
                battleBridge.SetDefenderPool(_session.PickedArray());
                battleBridge.SetMapGenerationOptions(SelectedMapGenerationOptions);

                // Phase 7: prefer the SkillLoadoutController roll; fall back to
                // the legacy Inspector array only when no loadout controller is
                // wired up (keeps tooling/editor scenes working without setup).
                var loadoutCtl = GameManager.Instance?.SkillLoadout;
                SkillData[] loadout;
                if (loadoutCtl != null && loadoutCtl.Picked.Count > 0)
                {
                    loadout = new SkillData[loadoutCtl.Picked.Count];
                    for (int i = 0; i < loadout.Length; i++) loadout[i] = loadoutCtl.Picked[i];
                }
                else
                {
                    loadout = defaultSkillLoadout;
                }

                if (loadout != null && loadout.Length > 0)
                {
                    battleBridge.SetSkillLoadout(loadout);
                    if (logger != null)
                    {
                        var ids = new System.Collections.Generic.List<string>();
                        foreach (var s in loadout) if (s != null) ids.Add(s.id);
                        logger.SetSkillLoadout(ids);

                        if (loadoutCtl != null)
                        {
                            var poolIds = new System.Collections.Generic.List<string>();
                            foreach (var s in loadoutCtl.Pool) if (s != null) poolIds.Add(s.id);
                            logger.SetSkillPool(poolIds, loadoutCtl.Seed);
                        }
                    }
                }
                // Phase 6: do NOT start battle immediately. PlacementPhaseView
                // subscribes to DraftConfirmed and runs the placement countdown,
                // then calls StartBattle itself.
            }
            DraftConfirmed?.Invoke();
            return true;
        }

        public void SetMapPathShape(MapPathShape shape)
        {
            var options = SelectedMapGenerationOptions.Normalized();
            options.pathShape = shape;
            SelectedMapGenerationOptions = options;
        }

        public void SetMapGenerationOptions(MapGenerationOptions options)
        {
            SelectedMapGenerationOptions = options.Normalized();
        }

        private static int GenerateSeed()
        {
            // Mix wall-clock with Unity's shared RNG so sequential sessions diverge
            // even when invoked on the same tick.
            return unchecked(Environment.TickCount
                ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }
    }
}
