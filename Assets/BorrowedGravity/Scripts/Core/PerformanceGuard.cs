using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BorrowGravity
{
    /// <summary>
    /// Phones: start sharp (full render resolution) and keep 60 fps by lowering the 3D render resolution a step
    /// when frames run slow, raising it again when there is room. The UI is drawn on top at full resolution either
    /// way. Also trims the costly extras phones don't need (HDR buffers, far shadows). Runtime only: the project's
    /// render pipeline asset is not changed on disk.
    /// </summary>
    public class PerformanceGuard : MonoBehaviour
    {
        const float MinScale = 0.7f, MaxScale = 1f, Step = 0.1f;
        const float SlowFrame = 1f / 50f, FastFrame = 1f / 58f;

        static PerformanceGuard instance;
        float sum, window;
        int frames, fastWindows;

        public static void Begin()
        {
            if (instance != null) return;
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) return;
            pipeline.renderScale = MaxScale;
            pipeline.supportsHDR = false;
            pipeline.shadowDistance = Mathf.Min(pipeline.shadowDistance, 35f);
            var go = new GameObject("PerformanceGuard");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PerformanceGuard>();
        }

        void Update()
        {
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
            if (avg > SlowFrame && pipeline.renderScale > MinScale + 0.01f)
            {
                pipeline.renderScale = Mathf.Max(MinScale, pipeline.renderScale - Step);
                fastWindows = 0;
            }
            else if (avg < FastFrame && pipeline.renderScale < MaxScale - 0.01f && ++fastWindows >= 3)
            {
                pipeline.renderScale = Mathf.Min(MaxScale, pipeline.renderScale + Step);
                fastWindows = 0;
            }
        }
    }
}
