using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CustomRP.Runtime {
    public class Lighting {
        const string BufferName = "Lighting";
        const int MaxDirLightCount = 4, MaxOtherLightCount = 64;
        readonly Shadows _shadows = new Shadows();

        static readonly int
            DirLightCountId = Shader.PropertyToID("_DirectionalLightCount"),
            DirLightColorsId = Shader.PropertyToID("_DirectionalLightColors"),
            DirLightDirectionsId = Shader.PropertyToID("_DirectionalLightDirections"),
            DirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");

        private static readonly int otherLightCountId = Shader.PropertyToID("_OtherLightCount"),
            otherLightColorsId = Shader.PropertyToID("_OtherLightColors"),
            otherLightPositionsID = Shader.PropertyToID("_OtherLightPositions");


        static readonly Vector4[]
            DirLightColors = new Vector4[MaxDirLightCount],
            DirLightDirections = new Vector4[MaxDirLightCount],
            DirLightShadowData = new Vector4[MaxDirLightCount];

        static readonly Vector4[] otherLightColors = new Vector4[MaxOtherLightCount],
            otherLightPositions = new Vector4[MaxOtherLightCount];

        readonly CommandBuffer _buffer = new CommandBuffer {
            name = BufferName
        };

        private CullingResults _cullingResults;

        public void Setup(ScriptableRenderContext context,
            CullingResults cullingResults, ShadowSettings shadowSettings) {
            this._cullingResults = cullingResults;
            _buffer.BeginSample(BufferName);
            //SetupDirectionalLight();
            _shadows.Setup(context, cullingResults, shadowSettings);
            SetupLights();
            _shadows.Render();
            _buffer.EndSample(BufferName);

            context.ExecuteCommandBuffer(_buffer);
            _buffer.Clear();
        }

        void SetupLights() {
            NativeArray<VisibleLight> visibleLights = _cullingResults.visibleLights;
            int dirLightCount = 0, otherLightCount = 0;
            foreach (var t in visibleLights) {
                VisibleLight visibleLight = t;

                switch (visibleLight.lightType) {
                    case LightType.Directional:
                        if (dirLightCount < MaxDirLightCount) {
                            SetupDirectionalLight(dirLightCount++, ref visibleLight);
                        }

                        break;
                    case LightType.Point:
                        if (otherLightCount < MaxOtherLightCount) {
                            SetupPointLight(otherLightCount++, ref visibleLight);
                        }
                        break;
                }
            }

            _buffer.SetGlobalInt(DirLightCountId,  dirLightCount);

            if (dirLightCount > 0) {
                _buffer.SetGlobalVectorArray(DirLightColorsId, DirLightColors);
                _buffer.SetGlobalVectorArray(DirLightDirectionsId, DirLightDirections);
                _buffer.SetGlobalVectorArray(DirLightShadowDataId, DirLightShadowData);
            }

            _buffer.SetGlobalInt(otherLightCountId, otherLightCount);
            if (otherLightCount > 0) {
                _buffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
                _buffer.SetGlobalVectorArray(otherLightPositionsID, otherLightPositions);
            }
        }


        /// <summary>
        /// only sun
        /// </summary>
        /// <param name="index">Index of visibleLight</param>
        /// <param name="visibleLight">VisibleLight object</param>
        void SetupDirectionalLight(int index, ref VisibleLight visibleLight) {
            DirLightColors[index] = visibleLight.finalColor;

            //local to world matrix XYZ为世界坐标下的local坐标系的XYZ轴（基）
            //  |Xx Yx Zx 0|
            //  |Xy Yy Zy 0|
            //  |Xz Yz Zz 0|
            //  | 0  0  0 1|
            DirLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            DirLightShadowData[index] = _shadows.ReserveDirectionalShadows(visibleLight.light, index);
        }


        void SetupPointLight(int index, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            otherLightPositions[index] = visibleLight.localToWorldMatrix.GetColumn(3);
        }

        public void Cleanup() {
            _shadows.Cleanup();
        }
    }
}