using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = UnityEngine.Random;


[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock _block;
    [SerializeField] Color baseColor = Color.white;

    private void Awake() {
        OnValidate();
    }

    private void OnValidate() {
        _block ??= new MaterialPropertyBlock();


        baseColor.r = Random.value;
        baseColor.g = Random.value;
        baseColor.b = Random.value;
        _block.SetColor(BaseColorId, baseColor);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }
}