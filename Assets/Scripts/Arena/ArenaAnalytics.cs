using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;
using UnityEngine.UnityConsent;
using WelpTimeToDie.Levels;

namespace WelpTimeToDie.RectanglePrototype
{
    internal sealed class ArenaAnalytics
    {
        private const int StartupEventLimit = 64;
        private readonly Queue<CustomEvent> startupEvents = new Queue<CustomEvent>();
        private string runId;
        private double startedAt;
        private bool[] visitedColumns;
        private int visitedCount, previousColumn;
        private float mapLeft, columnWidth;
        private bool reportedSendFailure;

        public void StartRun(RectangleCombat world, string reason)
        {
            runId = Guid.NewGuid().ToString("N");
            startedAt = Time.realtimeSinceStartupAsDouble;
            mapLeft = world.Terrain?.MinX ?? 0;
            float mapRight = world.Terrain?.MaxX ?? RectangleCombat.Width;
            columnWidth = world.Terrain?.BlockWidth ?? 1;
            visitedColumns = new bool[Mathf.Max(1, Mathf.RoundToInt((mapRight - mapLeft) / columnWidth))];
            visitedCount = 0;
            previousColumn = GetColumn(world.Player.X);
            VisitColumns(world.Player.X);

            var started = CreateEvent("arena_run_started");
            started.Add("start_reason", reason);
            Record(started);
        }

        public void EndRun(string outcome)
        {
            if (runId == null) return;
            var ended = CreateEvent("arena_run_ended");
            ended.Add("run_outcome", outcome);
            ended.Add("duration_seconds", Time.realtimeSinceStartupAsDouble - startedAt);
            ended.Add("map_explored_percent", 100.0 * visitedCount / visitedColumns.Length);
            runId = null;
            Record(ended);
        }

        public void RecordMove(RectangleFighter fighter, CombatMove move)
        {
            if (runId == null || move == CombatMove.None) return;
            var used = CreateEvent("arena_move_used");
            used.Add("move_name", RectangleCombat.Spec(fighter, move).Name);
            Record(used);
        }

        public void RecordFrame(RectangleCombat previous, RectangleCombat current)
        {
            if (runId == null) return;
            VisitColumns(current.Player.X);
            if (current.PlayerHitsThisFrame != null)
            {
                foreach (var hit in current.PlayerHitsThisFrame)
                {
                    var damage = CreateEvent("arena_move_damage_taken");
                    damage.Add("move_name", hit.Move);
                    damage.Add("damage_taken", hit.Damage);
                    Record(damage);
                }
            }

            if (current.RockSpikeRaisedThisFrame) RecordTrap("golem_rock_spike");
            var before = previous.Terrain?.State;
            var after = current.Terrain?.State;
            if (before != null && after != null && before.Revision != after.Revision)
            {
                for (int i = 0; i < after.Features.Count; i++)
                {
                    if (after.Features[i].Kind == TerrainFeatureKind.CollapsingFloor && after.IsOpen(i) && !before.IsOpen(i))
                        RecordTrap("collapsing_floor");
                }
            }

            if (current.Finished) EndRun(current.Lost ? "loss" : "win");
        }

        private void RecordTrap(string trapType)
        {
            var activated = CreateEvent("arena_trap_activated");
            activated.Add("trap_type", trapType);
            Record(activated);
        }

        private int GetColumn(float x) =>
            Mathf.Clamp(Mathf.FloorToInt((x - mapLeft) / columnWidth), 0, visitedColumns.Length - 1);

        private void VisitColumns(float playerX)
        {
            int column = GetColumn(playerX);
            for (int i = Mathf.Min(previousColumn, column); i <= Mathf.Max(previousColumn, column); i++)
            {
                if (visitedColumns[i]) continue;
                visitedColumns[i] = true;
                visitedCount++;
            }
            previousColumn = column;
        }

        private CustomEvent CreateEvent(string eventName)
        {
            var data = new CustomEvent(eventName);
            data.Add("run_id", runId);
            return data;
        }

        private void Record(CustomEvent data)
        {
            var consent = EndUserConsent.GetConsentState().AnalyticsIntent;
            if (consent == ConsentStatus.Denied)
            {
                startupEvents.Clear();
                return;
            }

            // The arena starts in OnEnable, before the existing UGS bootstrap runs in Start.
            if (UnityServices.State != ServicesInitializationState.Initialized ||
                (consent != ConsentStatus.Granted && startupEvents.Count > 0))
            {
                if (startupEvents.Count < StartupEventLimit) startupEvents.Enqueue(data);
                return;
            }

            if (consent != ConsentStatus.Granted) return;
            DrainStartupEvents();
            Send(data);
        }

        public void SendStartupEvents()
        {
            if (startupEvents.Count == 0) return;
            var consent = EndUserConsent.GetConsentState().AnalyticsIntent;
            if (consent == ConsentStatus.Denied)
            {
                startupEvents.Clear();
                return;
            }

            // Services can become ready before the bootstrap has applied the consent state.
            if (consent == ConsentStatus.Granted && UnityServices.State == ServicesInitializationState.Initialized)
                DrainStartupEvents();
        }

        private void DrainStartupEvents()
        {
            while (startupEvents.Count > 0)
                Send(startupEvents.Dequeue());
        }

        private void Send(CustomEvent data)
        {
            try
            {
                AnalyticsService.Instance.RecordEvent(data);
            }
            catch (Exception error)
            {
                // An unavailable or shutting-down SDK must never interrupt combat playback.
                if (reportedSendFailure) return;
                reportedSendFailure = true;
                Debug.LogWarning($"Arena Analytics could not record an event: {error.Message}");
            }
        }
    }
}
