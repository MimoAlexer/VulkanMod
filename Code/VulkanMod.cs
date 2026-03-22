using NeoModLoader.api;
using UnityEngine;
using UnityEngine.Rendering;

namespace VulkanMod.Code
{
    public class VulkanMod : BasicMod<VulkanMod>
    {
        private const float ReapplyIntervalSeconds = 2f;

        private float _nextReapplyAt;
        private bool _loggedGraphicsState;
        private bool _loggedStartupSummary;

        protected override void OnModLoad()
        {
            HarmonyBootstrap.Apply();
            RuntimeTuner.Apply();
            LogInfo("Performance hooks enabled. Watch the next startup/live status lines to verify the mod is active.");
        }

        public void Start()
        {
            RuntimeTuner.Apply();
            LogStartupSummary();
            LogGraphicsState();
        }

        public void Update()
        {
            if (!_loggedStartupSummary)
            {
                LogStartupSummary();
            }

            if (Time.unscaledTime < _nextReapplyAt)
            {
                return;
            }

            _nextReapplyAt = Time.unscaledTime + ReapplyIntervalSeconds;
            RuntimeTuner.Apply();
            LogGraphicsState();
        }

        private void LogStartupSummary()
        {
            if (_loggedStartupSummary)
            {
                return;
            }

            _loggedStartupSummary = true;
            RuntimeTuner.LogStartupSummary();
            RuntimeTuner.LogLiveStatusIfDue(true);
        }

        private void LogGraphicsState()
        {
            if (_loggedGraphicsState)
            {
                return;
            }

            _loggedGraphicsState = true;

            GraphicsDeviceType backend = SystemInfo.graphicsDeviceType;
            RenderingThreadingMode threadingMode = SystemInfo.renderingThreadingMode;
            bool graphicsMultiThreaded = SystemInfo.graphicsMultiThreaded;

            LogInfo(
                string.Format(
                    "Graphics backend: {0}; graphicsMultiThreaded={1}; renderingThreadingMode={2}.",
                    backend,
                    graphicsMultiThreaded,
                    threadingMode
                )
            );

            if (backend != GraphicsDeviceType.Vulkan)
            {
                LogWarning(
                    "WorldBox is not running on Vulkan. Unity/NML cannot swap graphics APIs after startup, so Vulkan must be requested before the game boots."
                );
            }
        }
    }
}
