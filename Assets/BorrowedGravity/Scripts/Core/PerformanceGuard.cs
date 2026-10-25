using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowGravity
{
    /// <summary>How strong this phone is: picked once at start from memory, CPU cores and GPU shader level.</summary>
    public enum DeviceTier { Low, Mid, High }

    /// <summary>
    /// Phones: pick settings for the device's tier (low-end phones get a lower render resolution, no shadows or
    /// post-processing and 30 fps; strong phones keep it all at 60 fps), then keep the frame rate steady by lowering
    /// the 3D render resolution a step when frames run slow and raising it again when there is room. The UI is drawn
    /// on top at full resolution either way. Runtime only: the project's render pipeline asset is not changed on disk.
    /// </summary>
    public class PerformanceGuard : MonoBehaviour
    {
        const float Step = 0.1f;

        /// <summary>This device's tier (High in the editor and on PC).</summary>
        public static DeviceTier Tier { get; private set; } = DeviceTier.High;

        static PerformanceGuard instance;
        float minScale = 0.7f, maxScale = 1f, slowFrame, fastFrame;
        float sum, window, nextCameraCheck;
        int frames, fastWindows;

        /// <summary>Low: little memory, few cores or an old GPU. High: lots of memory and cores. Mid: the rest.</summary>
        public static DeviceTier Detect()
        {
            int ram = SystemInfo.systemMemorySize, cores = SystemInfo.processorCount, shader = SystemInfo.graphicsShaderLevel;
            if (ram <= 3072 || cores <= 4 || shader < 45) return DeviceTier.Low;
            if (ram >= 6000 && cores >= 8) return DeviceTier.High;
            return DeviceTier.Mid;
        }

        public static void Begin()
        {
            if (instance != null) return;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            Tier = Detect();
            var go = new GameObject("PerformanceGuard");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PerformanceGuard>();
            instance.Apply(pipeline);
        }

        void Apply(UniversalRenderPipelineAsset pipeline)
        {
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            switch (Tier)
            {
                case DeviceTier.Low:
                    Application.targetFrameRate = 30;
                    minScale = 0.55f; maxScale = 0.8f;
                    pipeline.renderScale = 0.7f;
                    pipeline.shadowDistance = 0f; // no real-time shadows
                    QualitySettings.lodBias = 0.6f;
                    break;
                case DeviceTier.Mid:
                    Application.targetFrameRate = 60;
                    minScale = 0.65f; maxScale = 0.95f;
                    pipeline.renderScale = 0.85f;
                    pipeline.shadowDistance = Mathf.Min(pipeline.shadowDistance, 22f);
                    pipeline.shadowCascadeCount = 1;
                    pipeline.mainLightShadowmapResolution = 1024;
                    QualitySettings.lodBias = 0.8f;
                    break;
                default:
                    Application.targetFrameRate = 60;
                    minScale = 0.75f; maxScale = 1f;
                    pipeline.renderScale = 1f;
                    pipeline.shadowDistance = Mathf.Min(pipeline.shadowDistance, 35f);
                    pipeline.shadowCascadeCount = Mathf.Min(pipeline.shadowCascadeCount, 2);
                    pipeline.mainLightShadowmapResolution = Mathf.Min(pipeline.mainLightShadowmapResolution, 2048);
                    break;
            }
            float target = 1f / Application.targetFrameRate;
            slowFrame = target * 1.2f;  // e.g. under 50 fps when aiming for 60
            fastFrame = target * 1.03f;
        }

        void Update()
        {
            // low-end phones: no post-processing on any camera (cameras come and go with each level)
            if (Tier == DeviceTier.Low && Time.unscaledTime >= nextCameraCheck)
            {
                nextCameraCheck = Time.unscaledTime + 1f;
                foreach (var cam in Camera.allCameras)
                {
                    var data = cam.GetComponent<UniversalAdditionalCameraData>();
                    if (data != null && data.renderPostProcessing) data.renderPostProcessing = false;
                }
            }

            // judge 3-second windows, ignoring hitches from loading a level
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.25f) return;
            sum += dt;
            frames++;
            window += dt;
            if (window < 3f) return;
            float avg = sum / frames;
            sum = window = 0f;
            frames = 0;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            if (avg > slowFrame && pipeline.renderScale > minScale + 0.01f)
            {
                pipeline.renderScale = Mathf.Max(minScale, pipeline.renderScale - Step);
                fastWindows = 0;
            }
            else if (avg < fastFrame && pipeline.renderScale < maxScale - 0.01f && ++fastWindows >= 3)
            {
                pipeline.renderScale = Mathf.Min(maxScale, pipeline.renderScale + Step);
                fastWindows = 0;
            }
        }
    }
}
