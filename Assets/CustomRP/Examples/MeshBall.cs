using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MeshBall : MonoBehaviour {
    [SerializeField] private bool defaultDraw = true;

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"),
        MetallicId = Shader.PropertyToID("_Metallic"),
        SmoothnessId = Shader.PropertyToID("_Smoothness");

    [SerializeField] Mesh mesh = default;

    [SerializeField] Material material = default;
    [SerializeField] LightProbeProxyVolume lightProbeVolume = null;

    readonly Matrix4x4[] _matrices = new Matrix4x4[1023];
    readonly Vector4[] _baseColors = new Vector4[1023];
    readonly float[] _metallic = new float[1023];
    readonly float[] _smoothness = new float[1023];

    MaterialPropertyBlock _block;

    void Awake() {
        var center = GetComponent<Transform>().transform.position;
        for (int i = 0; i < _matrices.Length; i++) {
            _matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f + center,
                Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                Vector3.one * Random.Range(0.5f, 1.5f)
            );
            _baseColors[i] =
                new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
            _metallic[i] = Random.value < 0.25f ? 1f : 0f;
            _smoothness[i] = Random.Range(0.05f, 0.95f);
        }
    }

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() {
        if (_block == null) {
            _block ??= new MaterialPropertyBlock();
            _block.SetVectorArray(BaseColorId, _baseColors);
            _block.SetFloatArray(MetallicId, _metallic);
            _block.SetFloatArray(SmoothnessId, _smoothness);

            var positions = new Vector3[1023];

            for (int i = 0; i < _matrices.Length; i++) {
                positions[i] = _matrices[i].GetColumn(3); //获取球体的位置
            }

            var lightProbes = new SphericalHarmonicsL2[1023];
            var occlusionProbes = new Vector4[1023];
            LightProbes.CalculateInterpolatedLightAndOcclusionProbes(
                positions, lightProbes, occlusionProbes
            );
            _block.CopySHCoefficientArraysFrom(lightProbes);
        }

        if (defaultDraw) {
            Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023, _block);
            // Debug.Log("Mesh Ball Default Draw");
        }
        else {
            bool lightProbeVolumeEnable = lightProbeVolume && lightProbeVolume.enabled;
            Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023, _block
                , ShadowCastingMode.On, true, 0, null,
                lightProbeVolumeEnable ? LightProbeUsage.UseProxyVolume : LightProbeUsage.CustomProvided,
                lightProbeVolume
            );
            /*if (lightProbeVolumeEnable) {
                Debug.Log("Mesh Ball  LPPV");
            }
            else {
                Debug.Log("Mesh BallLight Probes");
            }*/
        }
    }
}