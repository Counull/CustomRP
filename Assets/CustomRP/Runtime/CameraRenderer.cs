using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class CameraRenderer : MonoBehaviour {
        private ScriptableRenderContext _context;
        private Camera _camera;


        public void Render(ScriptableRenderContext context, Camera renderingCamera) {
            this._context = context;
            this._camera = renderingCamera;
            DrawVisibleGeometry();
            Submit();
        }

        private void DrawVisibleGeometry() {
            _context.DrawSkybox(_camera);
        }

        private void Submit() {
            _context.Submit();
        }
    }
}