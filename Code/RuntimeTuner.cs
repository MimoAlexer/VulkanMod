using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Rendering;
using UnityEngine;

namespace VulkanMod.Code
{
    internal static class RuntimeTuner
    {
        private const float MinimapRedrawIntervalSeconds = 0.05f;

        private static bool _loggedThreadPlan;
        private static bool _loggedBatchPlan;
        private static bool _loggedAggressiveVisualPlan;
        private static bool _loggedMinimapPlan;
        private static bool _loggedNameplatePlan;
        private static bool _loggedScaleEffectPlan;
        private static bool _loggedQualityRollbackPlan;
        private static float _nextDenseWorldRetuneAt;
        private static int _lastActorDensityTier = -1;
        private static int _lastBuildingDensityTier = -1;

        internal static void Apply()
        {
            ForceWorldBoxParallelFlags();
            ApplyAggressiveVisualCuts();
            EnableThreadedTextureCreation();
            TuneFramePacing();
            TuneStreaming();
            ConfigureParallelOptions();
            ConfigureUnityJobWorkers();
            WarmThreadPool();
            TuneDenseWorldSimulation();
        }

        internal static void ApplyFrameCriticalSettings()
        {
            ForceWorldBoxParallelFlags();
            ApplyAggressiveVisualCuts();
            ConfigureParallelOptions();
            ConfigureUnityJobWorkers();
            TuneDenseWorldSimulation();
        }

        private static void ForceWorldBoxParallelFlags()
        {
            DebugConfig.setOption(DebugOption.ParallelJobsUpdater, true, true);
            DebugConfig.setOption(DebugOption.ParallelChunks, true, true);

            Config.parallel_jobs_updater = true;
            Config.parallel_chunk_manager = true;
        }

        private static void EnableThreadedTextureCreation()
        {
            if (!Texture.allowThreadedTextureCreation)
            {
                Texture.allowThreadedTextureCreation = true;
            }
        }

        private static void ApplyAggressiveVisualCuts()
        {
            Config.sprite_animations_on = false;
            Config.shadows_active = false;
            Bench.bench_enabled = false;
            Bench.bench_ai_enabled = false;

            DebugConfig.setOption(DebugOption.ScaleEffectEnabled, false, true);
            DebugConfig.setOption(DebugOption.LavaGlow, false, true);

            QualitySettings.antiAliasing = 0;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Disable;
            QualitySettings.masterTextureLimit = 0;
            QualitySettings.pixelLightCount = 0;
            QualitySettings.shadowDistance = 0f;

            RollBackForcedLowResolutionMode();

            if (_loggedAggressiveVisualPlan)
            {
                return;
            }

            _loggedAggressiveVisualPlan = true;
            VulkanMod.LogInfo(
                "Aggressive quality cuts enabled: shadows off, sprite animations off, city scale effects off, and reduced render quality."
            );
        }

        private static void RollBackForcedLowResolutionMode()
        {
            if (MapBox.instance == null || MapBox.instance.quality_changer == null)
            {
                return;
            }

            if (MapBox.instance.quality_changer.isLowRes())
            {
                MapBox.instance.quality_changer.setLowRes(false);
            }

            if (_loggedQualityRollbackPlan)
            {
                return;
            }

            _loggedQualityRollbackPlan = true;
            VulkanMod.LogInfo("Forced low-resolution zoom transitions disabled for stability.");
        }

        private static void TuneFramePacing()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Config.fps_lock_30 = false;
        }

        private static void TuneStreaming()
        {
            Application.backgroundLoadingPriority = UnityEngine.ThreadPriority.High;
            QualitySettings.asyncUploadPersistentBuffer = true;
            if (QualitySettings.asyncUploadTimeSlice < 8)
            {
                QualitySettings.asyncUploadTimeSlice = 8;
            }

            if (QualitySettings.asyncUploadBufferSize < 64)
            {
                QualitySettings.asyncUploadBufferSize = 64;
            }
        }

        private static void ConfigureParallelOptions()
        {
            if (MapBox.instance == null || MapBox.instance.parallel_options == null)
            {
                return;
            }

            int desiredWorkers = GetDesiredWorkerCount();
            if (MapBox.instance.parallel_options.MaxDegreeOfParallelism != desiredWorkers)
            {
                MapBox.instance.parallel_options.MaxDegreeOfParallelism = desiredWorkers;
            }

            if (_loggedThreadPlan)
            {
                return;
            }

            _loggedThreadPlan = true;
            VulkanMod.LogInfo(
                string.Format(
                    "Using up to {0} worker threads for WorldBox parallel loops.",
                    desiredWorkers
                )
            );
        }

        private static void ConfigureUnityJobWorkers()
        {
            int desiredWorkers = Mathf.Clamp(GetDesiredWorkerCount(), 1, JobsUtility.JobWorkerMaximumCount);
            if (JobsUtility.JobWorkerCount != desiredWorkers)
            {
                JobsUtility.JobWorkerCount = desiredWorkers;
            }
        }

        private static void WarmThreadPool()
        {
            int desiredWorkers = GetDesiredWorkerCount();
            int workerThreads;
            int completionPortThreads;
            ThreadPool.GetMinThreads(out workerThreads, out completionPortThreads);

            if (workerThreads >= desiredWorkers)
            {
                return;
            }

            ThreadPool.SetMinThreads(desiredWorkers, completionPortThreads);
        }

        private static int GetDesiredWorkerCount()
        {
            return Math.Max(1, Environment.ProcessorCount - 1);
        }

        internal static int GetDynamicRenderBatchSize()
        {
            int workers = GetDesiredWorkerCount();
            int batchSize;

            if (workers >= 16)
            {
                batchSize = 48;
            }
            else if (workers >= 12)
            {
                batchSize = 64;
            }
            else if (workers >= 6)
            {
                batchSize = 96;
            }
            else if (workers >= 4)
            {
                batchSize = 128;
            }
            else
            {
                batchSize = 256;
            }

            if (!_loggedBatchPlan)
            {
                _loggedBatchPlan = true;
                VulkanMod.LogInfo(
                    string.Format(
                        "Using render prep batch size {0} on {1} logical processors.",
                        batchSize,
                        Environment.ProcessorCount
                    )
                );
            }

            return batchSize;
        }

        internal static float GetMinimapRedrawInterval()
        {
            if (!_loggedMinimapPlan)
            {
                _loggedMinimapPlan = true;
                VulkanMod.LogInfo(
                    string.Format(
                        "Minimap redraws throttled to once every {0:0.000}s.",
                        MinimapRedrawIntervalSeconds
                    )
                );
            }

            return MinimapRedrawIntervalSeconds;
        }

        private static void TuneDenseWorldSimulation()
        {
            if (Time.unscaledTime < _nextDenseWorldRetuneAt || MapBox.instance == null)
            {
                return;
            }

            _nextDenseWorldRetuneAt = Time.unscaledTime + 2f;

            ActorManager actorManager = MapBox.instance.units;
            BuildingManager buildingManager = MapBox.instance.buildings;

            if (actorManager == null || buildingManager == null)
            {
                return;
            }

            int actorCount = actorManager.units_only_alive != null ? actorManager.units_only_alive.Count : 0;
            int buildingCount = buildingManager.occupied_buildings != null ? buildingManager.occupied_buildings.Count : 0;

            int actorTier = GetActorDensityTier(actorCount);
            int buildingTier = GetBuildingDensityTier(buildingCount);

            ApplyActorJobSkipTuning(actorManager._job_manager, actorTier);
            ApplyBuildingJobSkipTuning(buildingManager._job_manager, buildingTier);

            if (actorTier == _lastActorDensityTier && buildingTier == _lastBuildingDensityTier)
            {
                return;
            }

            _lastActorDensityTier = actorTier;
            _lastBuildingDensityTier = buildingTier;
            VulkanMod.LogInfo(
                string.Format(
                    "Dense-world job tuning active: actors={0} (tier {1}), buildings={2} (tier {3}), actorAI_skip={4}, targetScan_skip={5}, buildingSpread_skip={6}.",
                    actorCount,
                    actorTier,
                    buildingCount,
                    buildingTier,
                    GetActorJobRandomSkips("b6_update_ai", actorTier),
                    GetActorJobRandomSkips("b3_findEnemyTarget", actorTier),
                    GetBuildingJobRandomSkips("update_spread_trees", buildingTier)
                )
            );
        }

        private static int GetActorDensityTier(int actorCount)
        {
            if (actorCount >= 5000)
            {
                return 3;
            }

            if (actorCount >= 3000)
            {
                return 2;
            }

            if (actorCount >= 1500)
            {
                return 1;
            }

            return 0;
        }

        private static int GetBuildingDensityTier(int buildingCount)
        {
            if (buildingCount >= 4000)
            {
                return 3;
            }

            if (buildingCount >= 2500)
            {
                return 2;
            }

            if (buildingCount >= 1200)
            {
                return 1;
            }

            return 0;
        }

        private static void ApplyActorJobSkipTuning(JobManagerActors manager, int tier)
        {
            if (manager == null || manager._batches_active == null)
            {
                return;
            }

            foreach (BatchActors batch in manager._batches_active)
            {
                if (batch == null)
                {
                    continue;
                }

                ApplyActorJobSkipListTuning(batch.jobs_pre, tier);
                ApplyActorJobSkipListTuning(batch.jobs_parallel, tier);
                ApplyActorJobSkipListTuning(batch.jobs_post, tier);
            }
        }

        private static void ApplyActorJobSkipListTuning(List<Job<Actor>> jobs, int tier)
        {
            if (jobs == null)
            {
                return;
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                Job<Actor> job = jobs[i];
                if (job == null || string.IsNullOrEmpty(job.id))
                {
                    continue;
                }

                int targetSkips = GetActorJobRandomSkips(job.id, tier);
                if (job.random_tick_skips == targetSkips)
                {
                    continue;
                }

                job.random_tick_skips = targetSkips;
                if (job.current_skips > targetSkips)
                {
                    job.current_skips = targetSkips;
                }
            }
        }

        private static int GetActorJobRandomSkips(string jobId, int tier)
        {
            switch (jobId)
            {
                case "update_visibility":
                    return tier;
                case "update_stats":
                case "update_events_become_adult":
                case "update_events_hatched":
                case "u2_updateChildren":
                    return tier;
                case "update_hunger":
                    return tier >= 2 ? 1 : 0;
                case "u3_spriteAnimation":
                    return tier * 2;
                case "u7_checkAugmentationEffects":
                    return 20 + (tier * 10);
                case "b2_checkCurrentEnemyTarget":
                case "b4_checkTaskVerifier":
                case "b6_0_update_decision":
                case "b6_update_ai":
                case "u10_checkSmoothMovement":
                    return tier;
                case "b3_findEnemyTarget":
                    return 5 + (tier * 8);
                case "b5_checkPathMovement":
                    return tier >= 2 ? 1 : 0;
                case "b55_update_natural_death":
                    return 20 + (tier * 6);
                case "update_shake":
                case "update_hovering":
                case "update_pollinating":
                    return tier * 2;
                default:
                    return 0;
            }
        }

        private static void ApplyBuildingJobSkipTuning(JobManagerBuildings manager, int tier)
        {
            if (manager == null || manager._batches_active == null)
            {
                return;
            }

            foreach (BatchBuildings batch in manager._batches_active)
            {
                if (batch == null)
                {
                    continue;
                }

                ApplyBuildingJobSkipListTuning(batch.jobs_pre, tier);
                ApplyBuildingJobSkipListTuning(batch.jobs_parallel, tier);
                ApplyBuildingJobSkipListTuning(batch.jobs_post, tier);
            }
        }

        private static void ApplyBuildingJobSkipListTuning(List<Job<Building>> jobs, int tier)
        {
            if (jobs == null)
            {
                return;
            }

            for (int i = 0; i < jobs.Count; i++)
            {
                Job<Building> job = jobs[i];
                if (job == null || string.IsNullOrEmpty(job.id))
                {
                    continue;
                }

                int targetSkips = GetBuildingJobRandomSkips(job.id, tier);
                if (job.random_tick_skips == targetSkips)
                {
                    continue;
                }

                job.random_tick_skips = targetSkips;
                if (job.current_skips > targetSkips)
                {
                    job.current_skips = targetSkips;
                }
            }
        }

        private static int GetBuildingJobRandomSkips(string jobId, int tier)
        {
            switch (jobId)
            {
                case "update_scale":
                case "update_angle":
                case "update_visibility":
                case "update_dirty_stats":
                case "update_components":
                case "update_auto_remove":
                    return tier;
                case "update_resource_shaker":
                case "update_shake":
                    return tier * 2;
                case "update_spread_trees":
                case "update_spread_plants":
                case "update_spread_fungi":
                case "update_poop_turning_into_flora":
                    return tier * 2;
                default:
                    return 0;
            }
        }

        internal static void PrepareNameplatesForDisable(NameplateManager manager)
        {
            if (manager == null)
            {
                return;
            }

            manager.clearAll();

            if (_loggedNameplatePlan)
            {
                return;
            }

            _loggedNameplatePlan = true;
            VulkanMod.LogInfo("Nameplates disabled to remove per-frame UI traversal.");
        }

        internal static void NoteQuantumScaleEffectsDisabled()
        {
            if (_loggedScaleEffectPlan)
            {
                return;
            }

            _loggedScaleEffectPlan = true;
            VulkanMod.LogInfo("City and kingdom hover scale effects disabled.");
        }

    }
}
