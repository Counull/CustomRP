using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class Lighting {
        const string BufferName = "Lighting";
        const int MaxDirLightCount = 4;

        static int
            //dirLightColorId = Shader.PropertyToID("_DirectionalLightColor"),
            //dirLightDirectionId = Shader.PropertyToID("_DirectionalLightDirection");
            _dirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            _dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            _dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections");


        static Vector4[]
            _dirLightColors = new Vector4[MaxDirLightCount],
            _dirLightDirections = new Vector4[MaxDirLightCount];

        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };

        private CullingResults _cullingResults;

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults) {
            this._cullingResults = cullingResults;
            _buffer.BeginSample(BufferName);
            //SetupDirectionalLight();
            SetupLights();
            _buffer.EndSample(BufferName);

            context.ExecuteCommandBuffer( _buffer);
            _buffer.Clear();
        }

        void SetupLights() {
            NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;
            int dirLightCount = 0;
            for (int i = 0; i < visibleLights.Length; i++) {
                VisibleLight visibleLight = visibleLights[i];
                if (visibleLight.lightType == LightType.Directional) {
                    SetupDirectionalLight(dirLightCount, ref visibleLight);
                    dirLightCount++;
                    if (dirLightCount >= MaxDirLightCount) {
                        break;
                    }
                }
            }

            _buffer.SetGlobalInt(_dirLightCountId, visibleLights.Length);
            _buffer.SetGlobalVectorArray(_dirLightColorsId, _dirLightColors);
            _buffer.SetGlobalVectorArray(_dirLightDirectionsId, _dirLightDirections);
        }


        /// <summary>
        /// only sun
        /// </summary>
        /// <param name="index">Index of visibleLight</param>
        /// <param name="visibleLight">VisibleLight object</param>
        void SetupDirectionalLight(int index, ref VisibleLight visibleLight) {
            Light light = RenderSettings.sun;
            _dirLightColors[index] = visibleLight.finalColor;

            //local to world matrix XYZ为世界坐标下的local坐标系的XYZ轴（基）
            //  |Xx Yx Zx 0|
            //  |Xy Yy Zy 0|
            //  |Xz Yz Zz 0|
            //  | 0  0  0 1|
            _dirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
        }
    }
}