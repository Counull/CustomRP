using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class CameraRenderer : MonoBehaviour {
        private ScriptableRenderContext _context;
        private Camera _camera;
        private CullingResults _cullingResults;
        private const string BufferName = "Render Camera";

        private static ShaderTagId _unlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");

        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };

        public void Render(ScriptableRenderContext context, Camera renderingCamera) {
            this._context = context;
            this._camera = renderingCamera;
            if (!Cull()) {
                return;
            }

            Setup();
            DrawVisibleGeometry();
            Submit();
        }

        private void DrawVisibleGeometry() {
            var sortingSettings = new SortingSettings(_camera) {criteria = SortingCriteria.CommonOpaque};
            var drawingSettings = new DrawingSettings(_unlitShaderTagId, sortingSettings);
            var filteringSettings = new FilteringSettings(RenderQueueRange.all);
            _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
            _context.DrawSkybox(_camera);
        }

        private void Setup() {
            _context.SetupCameraProperties(_camera);
            _buffer.ClearRenderTarget(true, true, Color.clear);
            _buffer.BeginSample(BufferName);
            ExecuteBuffer();
        }

        private void Submit() {
            _buffer.EndSample(BufferName);
            ExecuteBuffer();
            _context.Submit();
        }

        void ExecuteBuffer() {
            _context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        bool Cull() {
            if (_camera.TryGetCullingParameters(out ScriptableCullingParameters p)) {
                _cullingResults = _context.Cull(ref p);
                return true;
            }

            return false;
        }
    }
}