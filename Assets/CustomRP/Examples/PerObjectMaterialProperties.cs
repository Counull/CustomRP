using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;


[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"),
        MetallicId = Shader.PropertyToID("_Metallic"),
        SmoothnessId = Shader.PropertyToID("_Smoothness"),
        CutoffId = Shader.PropertyToID("_Cutoff");

    static MaterialPropertyBlock _block;
    [SerializeField] Color baseColor = Color.white;

    [SerializeField, Range(0f, 1f)] float alphaCutoff = 0.5f;
    [SerializeField, Range(0f, 1f)] float metallic = 0f, smoothness = 0.5f;

    private void Awake() {
        OnValidate();
    }

    private void OnValidate() {
        _block ??= new MaterialPropertyBlock();
        _block.SetColor(BaseColorId, baseColor);
        _block.SetFloat(CutoffId, alphaCutoff);
        _block.SetFloat(MetallicId, metallic);
        _block.SetFloat(SmoothnessId, smoothness);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }
}