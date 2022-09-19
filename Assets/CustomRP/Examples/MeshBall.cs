using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshBall : MonoBehaviour
{
    
    static int _baseColorId = Shader.PropertyToID("_BaseColor");
    
    [SerializeField]
    Mesh mesh = default;
    
    [SerializeField]
    Material material = default;


    readonly Matrix4x4[] _matrices = new Matrix4x4[1023];
    readonly Vector4[] _baseColors = new Vector4[1023];
    
    MaterialPropertyBlock _block;
    
    void Awake () {
       var center= GetComponent<Transform>().transform.position;
        for (int i = 0; i <  _matrices.Length; i++) {
            _matrices[i] = Matrix4x4.TRS(
                Random.insideUnitSphere * 10f+center , Quaternion.identity, Vector3.one
            );
            _baseColors[i] =
                new Vector4(Random.value, Random.value, Random.value, 1f);
        }
    }
    
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        _block ??= new MaterialPropertyBlock();
        _block .SetVectorArray(_baseColorId,  _baseColors);
        Graphics.DrawMeshInstanced(mesh, 0, material, _matrices, 1023, _block);
    }
}
