using System;
using System.Threading;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Rendering;
using UnityEngine;

namespace VulkanMod.Code
{
    internal static class RuntimeTuner
    {
        private static bool _loggedThreadPlan;
        private static bool _loggedBatchPlan;

        internal static void Apply()
        {
            ForceWorldBoxParallelFlags();
            EnableThreadedTextureCreation();
            TuneFramePacing();
            TuneStreaming();
            ConfigureParallelOptions();
            ConfigureUnityJobWorkers();
            WarmThreadPool();
        }

        internal static void ApplyFrameCriticalSettings()
        {
            ForceWorldBoxParallelFlags();
            ConfigureParallelOptions();
            ConfigureUnityJobWorkers();
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

            if (workers >= 12)
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
    }
}
