using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using Wassup.Bridge;
using Wassup.Data;
using Wassup.Data.MapGrid;
using Wassup.Logging;

namespace Wassup.Core
{
    // Owns one DraftSession at a time and bridges it to BattleBridge. It is a plain
    // MonoBehaviour placed in the scene — no static Instance, since GameManager is
    // the only allowed singleton (CLAUDE.md). UI layers call ToggleDiscard/TryConfirm;
    // BeginDraft is invoked by GameManager or ResultScreen-triggered code paths.
    public class DraftController : MonoBehaviour
    {
        private const int BasicSlotCount = 3;
        private const int MetaSlotCount = 2;
        private const int EgoSlotCount = 1;
        private const int CollectionSlotCount = 4;
        private const int DraftPoolSize = BasicSlotCount + MetaSlotCount + EgoSlotCount + CollectionSlotCount;

        [SerializeField] private DefenderUnitData[] basicDeck;
        [SerializeField] private DefenderUnitData[] metaDeck;
        [SerializeField] private DefenderUnitData egoUnit;
        [SerializeField] private DefenderUnitData[] collectionPool;

        // Legacy scene/test fallback. New content should use the slot fields above.
        [SerializeField, HideInInspector] private DefenderUnitData[] catalog;
        [SerializeField, HideInInspector] private int poolSize = DraftPoolSize;
        [SerializeField] private int discardCount = 3;
        [SerializeField] private BattleBridge battleBridge;
        [SerializeField] private SkillData[] defaultSkillLoadout;

        private readonly DraftSession _session = new();

        public DraftSession Session => _session;
        public int PoolSize => DraftPoolSize;
        public int DiscardCount => discardCount;
        public int PickCount => DraftPoolSize - discardCount;
        public IReadOnlyList<DefenderUnitData> CollectionPool => collectionPool;
        public IReadOnlyList<DefenderUnitData> Catalog => collectionPool;
        public MapGenerationOptions SelectedMapGenerationOptions { get; private set; } = MapGenerationOptions.Default;
        public MapPathShape SelectedMapPathShape => SelectedMapGenerationOptions.pathShape;
        public MapSource SelectedMapSource { get; private set; } = MapSource.Legacy;
        public bool BridgeGoalEdgeOnly => battleBridge != null && battleBridge.CurrentGoalEdgeOnly;
        public int2? SelectedMapGridGridSize { get; private set; }

        public event Action DraftStarted;
        public event Action DraftConfirmed;

        public void BeginDraft()
        {
            BeginDraft(GenerateSeed());
        }

        public void BeginDraft(int seed)
        {
            if (HasSlotConfiguration())
            {
                if (!ValidateSlots()) return;
                _session.Reset(
                    basicDeck,
                    metaDeck,
                    egoUnit,
                    collectionPool,
                    CollectionSlotCount,
                    discardCount,
                    seed);
            }
            else if (catalog != null && catalog.Length >= poolSize)
            {
                _session.Reset(catalog, poolSize, discardCount, seed);
            }
            else
            {
                Debug.LogError(
                    $"[DraftController] slot fields are not assigned and legacy catalog needs at least {poolSize} entries (has {(catalog?.Length ?? 0)}).",
                    this);
                return;
            }

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

        public bool ToggleDiscard(DefenderUnitData unit) => _session.ToggleDiscard(unit);

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
                foreach (var u in _session.Discarded) if (u != null) record.discarded.Add(u.displayName);
                logger.SetDraft(record);
            }

            if (battleBridge != null)
            {
                // draft-confirmed match carries no squad stones by construction
                // (스펙: 드래프트 폴백 경로 미적용).
                battleBridge.SetDreamstones(null);
                // dreamstone-loadout Unit 6 — same entry point resets the CostRate
                // multiplier (a drafted match carries no squad stone buffs at all,
                // entity or cost — REDRAFT-leak fix's symmetry, unit 3).
                GameManager.Instance?.CostRuntime?.SetRegenRateMultiplier(1f);
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
            // draft-stage-map-prebuild Unit 3 — propagate to bridge so the playfield
            // behind the card fan reflects the change immediately.
            if (battleBridge != null)
            {
                battleBridge.SetMapGenerationOptions(SelectedMapGenerationOptions);
                battleBridge.RebuildDraftMap();
            }
        }

        public void SetMapGenerationOptions(MapGenerationOptions options)
        {
            SelectedMapGenerationOptions = options.Normalized();
            // draft-stage-map-prebuild Unit 3 — propagate to bridge.
            if (battleBridge != null)
            {
                battleBridge.SetMapGenerationOptions(SelectedMapGenerationOptions);
                battleBridge.RebuildDraftMap();
            }
        }

        // 씬 BattleBridge authoring 값을 컨트롤러 상태로 흡수한다 (bridge 로의 push 없음 — 씬이 source of truth).
        // 패널 초기화가 호출. 이후 TryConfirm 등이 push 하는 값이 씬 값과 일치하게 된다.
        public void SyncMapStateFromBridge()
        {
            if (battleBridge == null) return;
            SelectedMapSource = battleBridge.CurrentMapSource;
            SelectedMapGridGridSize = battleBridge.CurrentMapGridGridSizeOverride;
            SelectedMapGenerationOptions = battleBridge.CurrentMapGenerationOptions.Normalized();
        }

        public void SetMapSource(MapSource src)
        {
            SelectedMapSource = src;
            if (battleBridge != null)
            {
                battleBridge.SetMapSource(src);
                battleBridge.RebuildDraftMap();
            }
        }

        public void SetMapGridGridSize(int2? gridSize)
        {
            SelectedMapGridGridSize = gridSize;
            if (battleBridge != null)
            {
                battleBridge.SetMapGridGridSizeOverride(gridSize);
                battleBridge.RebuildDraftMap();
            }
        }

        public void SetGoalEdgeOnly(bool value)
        {
            if (battleBridge != null)
            {
                battleBridge.SetGoalEdgeOnly(value);
                battleBridge.RebuildDraftMap();
            }
        }

        private static int GenerateSeed()
        {
            // Mix wall-clock with Unity's shared RNG so sequential sessions diverge
            // even when invoked on the same tick.
            return unchecked(Environment.TickCount
                ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue));
        }

        private bool HasSlotConfiguration() =>
            basicDeck != null || metaDeck != null || egoUnit != null || collectionPool != null;

        private bool ValidateSlots()
        {
            if (basicDeck == null || basicDeck.Length != BasicSlotCount)
            {
                Debug.LogError($"[DraftController] basicDeck must have {BasicSlotCount} entries.", this);
                return false;
            }

            if (metaDeck == null || metaDeck.Length != MetaSlotCount)
            {
                Debug.LogError($"[DraftController] metaDeck must have {MetaSlotCount} entries.", this);
                return false;
            }

            if (egoUnit == null)
            {
                Debug.LogError("[DraftController] egoUnit is not assigned.", this);
                return false;
            }

            if (collectionPool == null)
            {
                Debug.LogError("[DraftController] collectionPool is not assigned.", this);
                return false;
            }

            var fixedUnits = new HashSet<DefenderUnitData>();
            if (!AddFixedUnits(fixedUnits, basicDeck, "basicDeck")) return false;
            if (!AddFixedUnits(fixedUnits, metaDeck, "metaDeck")) return false;
            if (!AddFixedUnit(fixedUnits, egoUnit, "egoUnit")) return false;

            var collectionCandidates = new HashSet<DefenderUnitData>();
            foreach (var unit in collectionPool)
            {
                if (unit == null) continue;
                if (fixedUnits.Contains(unit)) continue;
                collectionCandidates.Add(unit);
            }

            if (collectionCandidates.Count < CollectionSlotCount)
            {
                Debug.LogError(
                    $"[DraftController] collectionPool needs at least {CollectionSlotCount} non-fixed unique candidates (has {collectionCandidates.Count}).",
                    this);
                return false;
            }

            return true;
        }

        private bool AddFixedUnits(HashSet<DefenderUnitData> fixedUnits, DefenderUnitData[] units, string fieldName)
        {
            for (int i = 0; i < units.Length; i++)
                if (!AddFixedUnit(fixedUnits, units[i], $"{fieldName}[{i}]")) return false;
            return true;
        }

        private bool AddFixedUnit(HashSet<DefenderUnitData> fixedUnits, DefenderUnitData unit, string fieldName)
        {
            if (unit == null)
            {
                Debug.LogError($"[DraftController] {fieldName} is null.", this);
                return false;
            }

            if (!fixedUnits.Add(unit))
            {
                string name = string.IsNullOrEmpty(unit.displayName) ? unit.name : unit.displayName;
                Debug.LogError($"[DraftController] duplicate fixed slot unit: {name}.", this);
                return false;
            }

            return true;
        }
    }
}
