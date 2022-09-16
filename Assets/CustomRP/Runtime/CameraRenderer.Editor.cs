using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public partial class CameraRenderer {
        partial void DrawUnsupportedShaders();
        partial void DrawGizmos();

        partial void PrepareForSceneWindow();
#if UNITY_EDITOR
        private static Material _errorMaterial;

        static readonly ShaderTagId[] LegacyShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM")
        };

        partial void DrawUnsupportedShaders() {
            if (_errorMaterial == null) {
                _errorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            var drawingSettings = new DrawingSettings(
                LegacyShaderTagIds[0], new SortingSettings(_camera)
            ) {overrideMaterial = _errorMaterial};

            for (int i = 1; i < LegacyShaderTagIds.Length; i++) {
                drawingSettings.SetShaderPassName(i, LegacyShaderTagIds[i]);
            }

            var filteringSettings = FilteringSettings.defaultValue;
            _context.DrawRenderers(_cullingResults, ref drawingSettings, ref filteringSettings);
        }

        /// <summary>
        /// show camera icon bla bla bla...
        /// </summary>
        partial void DrawGizmos() {
            if (Handles.ShouldRenderGizmos()) {
                _context.DrawGizmos(_camera, GizmoSubset.PreImageEffects);
                _context.DrawGizmos(_camera, GizmoSubset.PostImageEffects);
            }
        }

        partial void PrepareForSceneWindow() {
            if (_camera.cameraType == CameraType.SceneView) {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(_camera);
            }
        }

#endif
    }
}