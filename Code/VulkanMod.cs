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

        protected override void OnModLoad()
        {
            HarmonyBootstrap.Apply();
            RuntimeTuner.Apply();
            LogInfo("Performance hooks enabled.");
        }

        public void Start()
        {
            RuntimeTuner.Apply();
            LogGraphicsState();
        }

        public void Update()
        {
            if (Time.unscaledTime < _nextReapplyAt)
            {
                return;
            }

            _nextReapplyAt = Time.unscaledTime + ReapplyIntervalSeconds;
            RuntimeTuner.Apply();
            LogGraphicsState();
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
