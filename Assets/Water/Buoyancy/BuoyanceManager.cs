using UnityEngine;
using UnityEngine.Serialization;
using Unity.Collections;
using Unity.Mathematics;

public class BuoyanceManager : MonoBehaviour
{
    private Transform[] buoyantObjs;
    private NativeArray<float3> points; 
    private float3[] heights;
    private float3[] normals;

    public NativeArray<Wave> _waveData;
    public int _waveCount;

    private static BuoyanceManager _instance;
    public static BuoyanceManager Instance
    {
        get
        {
            if (_instance == null)
                _instance = (BuoyanceManager)FindObjectOfType(typeof(BuoyanceManager));
            return _instance;
        }
    }

    private int _guid;
    
    private void Start()
    {
        _guid = gameObject.GetInstanceID();

        // 자식으로 있는 부력 오브젝트 가져오기
        buoyantObjs = new Transform[transform.childCount];
        points = new NativeArray<float3>(buoyantObjs.Length, Allocator.Persistent);
        heights = new float3[buoyantObjs.Length];
        normals = new float3[buoyantObjs.Length];

        for (var i = 0; i < buoyantObjs.Length; i++)
        {
            buoyantObjs[i] = transform.GetChild(i);
            Debug.Log(buoyantObjs[i].name);
            points[i] = buoyantObjs[i].position;
        }

        // wave 정보 가져오기
        _waveCount = 4; 
        _waveData = new NativeArray<Wave>(_waveCount, Allocator.Persistent);
        
        Wave wave = new Wave();

        Material material = GetComponent<Renderer>().material;
        var time = material.GetVector("Vector4_2DF06505");

        wave.amplitude = material.GetFloat("Vector1_7EAA09FD");
        wave.time = time.x;
        wave.direction = new float3(material.GetVector("Vector3_BB5DA054").x, material.GetVector("Vector3_BB5DA054").y, material.GetVector("Vector3_BB5DA054").z); ;
        _waveData[0] = wave;
        //Debug.Log("wave.amplitude - " + Shader.GetGlobalFloat("Vector1_7EAA09FD"));

        wave.amplitude = material.GetFloat("Vector1_C9F53F9");
        wave.time = time.y;
        wave.direction = new float3(material.GetVector("Vector3_110DB033").x, material.GetVector("Vector3_110DB033").y, material.GetVector("Vector3_110DB033").z); ;
        _waveData[1] = wave;

        wave.amplitude = material.GetFloat("Vector1_F0AF1EEC");
        wave.time = time.z;
        wave.direction = new float3(material.GetVector("Vector3_8495EC90").x, material.GetVector("Vector3_8495EC90").y, material.GetVector("Vector3_8495EC90").z); ;
        _waveData[2] = wave;

        wave.amplitude = material.GetFloat("Vector1_76F34D1B");
        wave.time = time.w;
        wave.direction = new float3(material.GetVector("Vector3_13778AA8").x, material.GetVector("Vector3_13778AA8").y, material.GetVector("Vector3_13778AA8").z); ;
        _waveData[3] = wave;

        // job system init
        GerstnerWavesJobs.Init();
    }

    private void OnDisable()
    {
        points.Dispose();
    }

    private void Update()
    {
        GerstnerWavesJobs.UpdateSamplePoints(ref points, _guid);
        GerstnerWavesJobs.GetData(_guid, ref heights, ref normals);

        for (var i = 0; i < buoyantObjs.Length; i++)
        {
            var vec = buoyantObjs[i].position;
            vec.y = heights[i].y;
            buoyantObjs[i].position = vec;
            //Debug.Log(vec);
            //Debug.Log(normals[i]);

            buoyantObjs[i].up = Vector3.Slerp(buoyantObjs[i].up, normals[i], Time.deltaTime);
            //Debug.Log(buoyantObjs[i].up);
            Debug.DrawRay(buoyantObjs[i].position, buoyantObjs[i].up * 10);
        }
    }

    private void LateUpdate()
    {
        GerstnerWavesJobs.UpdateHeights();
    }
}
