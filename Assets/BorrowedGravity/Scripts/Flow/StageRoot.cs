using UnityEngine;
using UnityEngine.Rendering;

namespace BorrowGravity
{
    /// <summary>
    /// One level inside the single Game scene (intro, stage 1-3, outro). Everything of that level hangs under this
    /// object, which is switched off while another level plays. Keeps the level's sky, fog and ambient light, which
    /// are scene-wide settings, so they can be applied when the level is shown.
    /// </summary>
    public class StageRoot : MonoBehaviour
    {
        public int index;
        public Material skybox;
        public AmbientMode ambientMode = AmbientMode.Skybox;
        public float ambientIntensity = 1f;
        public Color ambientLight = Color.gray;
        public bool fog = true;
        public FogMode fogMode = FogMode.ExponentialSquared;
        public float fogDensity = 0.0032f;
        public Color fogColor = Color.gray;
        public Light sun;

        public void ApplyLighting()
        {
            RenderSettings.skybox = skybox;
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.fog = fog;
            RenderSettings.fogMode = fogMode;
            RenderSettings.fogDensity = fogDensity;
            RenderSettings.fogColor = fogColor;
            RenderSettings.sun = sun;
            DynamicGI.UpdateEnvironment();
        }
    }
}
