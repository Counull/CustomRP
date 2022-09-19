using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Random = System.Random;

[DisallowMultipleComponent]
public class PerObjectMaterialProperties : MonoBehaviour {
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static MaterialPropertyBlock _block;
    [SerializeField] Color baseColor = Color.white;

    private void Awake() {
        OnValidate ();  
    }

    private void OnValidate() {
        _block ??= new MaterialPropertyBlock();
        Random random = new Random();
     
        baseColor.r = random.Next()%255/255.0f;
        baseColor.g = random.Next()%255/255.0f;
        baseColor.b = random.Next()%255/255.0f;
        _block.SetColor(BaseColorId, baseColor);
        GetComponent<Renderer>().SetPropertyBlock(_block);
    }

    // Start is called before the first frame update
    void Start() { }

    // Update is called once per frame
    void Update() { }
}