using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class CustomRenderPipeline : RenderPipeline {
        private readonly CameraRenderer _renderer = new();

        public CustomRenderPipeline() {
           GraphicsSettings.useScriptableRenderPipelineBatching = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            foreach (Camera camera in cameras) {
                _renderer.Render(context, camera);
            }
        }
    }
}