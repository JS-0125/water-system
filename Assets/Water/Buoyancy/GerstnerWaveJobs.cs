using System.Collections.Generic;
using UnityEngine;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using Unity.Collections;
//using WaterSystem.Data;

public struct Wave
{
    public float amplitude;
    public float time;

    public float3 direction;

    public Wave(float amp, float t, float3 dir)
    {
        amplitude = amp;
        time = t;
        direction = dir;
        
    }
}


public static class GerstnerWavesJobs
{
    public static bool Initialized;
    private static bool _firstFrame = true;
    private static bool _processing;
    private static int _waveCount;
    private static NativeArray<Wave> _waveData;

    private static NativeArray<float3> _positions;
    private static int _positionCount;
    private static NativeArray<float3> _wavePos;
    private static NativeArray<float3> _waveNormal;
    private static JobHandle _waterHeightHandle;
    static readonly Dictionary<int, int2> Registry = new Dictionary<int, int2>();

    public static void Init()
    {
        _waveCount = BuoyanceManager.Instance._waveCount;
        _waveData = BuoyanceManager.Instance._waveData;

        _positions = new NativeArray<float3>(16900, Allocator.Persistent);
        _wavePos = new NativeArray<float3>(16900, Allocator.Persistent);
        _waveNormal = new NativeArray<float3>(16900, Allocator.Persistent);

        Initialized = true;
    }

    public static void Cleanup()
    {
        _waterHeightHandle.Complete();

        //Cleanup native arrays
        _waveData.Dispose();

        _positions.Dispose();
        _wavePos.Dispose();
        _waveNormal.Dispose();
    }


    public static void UpdateSamplePoints(ref NativeArray<float3> samplePoints, int guid)
    {
        // job이 돌아가고 있으면 complete
        CompleteJobs();

        // 값이 있으면 값 전달
        if (Registry.TryGetValue(guid, out var offsets))
        {
            for (var i = offsets.x; i < offsets.y; i++) _positions[i] = samplePoints[i - offsets.x];
        }
        else
        {
            // 정해놓은 vertex개수보다 많으면 return
            if (_positionCount/*position이 시작되는 index*/ + samplePoints.Length/*x,y,z,로 3*/ >= _positions.Length) return;

            offsets = new int2(_positionCount, _positionCount + samplePoints.Length);
            Registry.Add(guid, offsets);
            _positionCount += samplePoints.Length;
        }
    }

    public static void GetData(int guid, ref float3[] outPos, ref float3[] outNorm)
    {
        // 값이 없으면 return
        if (!Registry.TryGetValue(guid, out var offsets)) return;

        // x, y, z인 값 3개를 잘라서 copy
        _wavePos.Slice(offsets.x, offsets.y - offsets.x).CopyTo(outPos);
        if (outNorm != null)
            _waveNormal.Slice(offsets.x, offsets.y - offsets.x).CopyTo(outNorm);
    }

    // 다음 프레임 계산
    public static void UpdateHeights()
    {
        // 이미 프로세싱 중이면 return
        if (_processing) return;

        _processing = true;

        var waterHeight = new GerstnerWavesJobs.HeightJob()
        {
            WaveData = _waveData,
            Position = _positions,
            OffsetLength = new int2(0, _positions.Length),
            Time = Time.time,
            OutPosition = _wavePos,
            OutNormal = _waveNormal
        };

        _waterHeightHandle = waterHeight.Schedule(_positionCount, 32);
        JobHandle.ScheduleBatchedJobs();

        _firstFrame = false;
    }

    private static void CompleteJobs()
    {
        // 첫 프레임이거나 프로세싱중이 아니면 return
        if (_firstFrame || !_processing) return;

        // job complete
        _waterHeightHandle.Complete();

        // 프로세싱 끝
        _processing = false;
    }

    // C# Job
    private struct HeightJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Wave> WaveData; // wave data stroed in vec4's like the shader version but packed into one
        [ReadOnly]
        public NativeArray<float3> Position;

        [WriteOnly]
        public NativeArray<float3> OutPosition;
        [WriteOnly]
        public NativeArray<float3> OutNormal;

        [ReadOnly]
        public float Time;
        [ReadOnly]
        public int2 OffsetLength;

        // running on the job
        public void Execute(int i)
        {
            if (i < OffsetLength.x || i >= OffsetLength.y - OffsetLength.x) return;

            //var waveCountMulti = 1f / WaveData.Length;
            var wavePos = Position[i];
            var waveNorm1 = new float3(Position[i].x +1f, Position[i].y, Position[i].z);
            var waveNorm2 = new float3(Position[i].x, Position[i].y, Position[i].z + 1f);

            var pos = Position[i].xz;
            var normPos1 = new float2(Position[i].x + 1f, Position[i].z);
            var normPos2 = new float2(Position[i].x, Position[i].z + 1f);


            for (var wave = 0; wave < WaveData.Length; wave++) // for each wave
            {
                // wave
                calcGerstner(ref wavePos, wave, pos);

                // new normal
                calcGerstner(ref waveNorm1, wave, normPos1);
                calcGerstner(ref waveNorm2, wave, normPos2);
            }

            OutPosition[i] = wavePos;
            var normal = math.normalize(math.cross(math.normalize(waveNorm2 - wavePos), math.normalize(waveNorm1 - wavePos)));
            OutNormal[i] = normal;

            //Debug.Log("outNormal - " + math.normalize(math.cross(math.normalize(waveNorm2 - wavePos), math.normalize(waveNorm1 - wavePos))));
        }

        private void calcGerstner(ref float3 wavePos, int wave, float2 pos)
        {
            var amplitude = WaveData[wave].amplitude;
            var direction = WaveData[wave].direction;

            //Debug.Log("WaveData[wave].amplitude - " + WaveData[wave].amplitude);
            //Debug.Log("WaveData[wave].direction - " + WaveData[wave].direction);

            float depth = 10f;
            float length = math.length(direction);
            //Debug.Log("length - " + length);

            float tanh = math.tanh(length * depth);
            //Debug.Log("tanh - " + tanh);

            float frequency = math.trunc(math.sqrt(length * 9f * tanh) * 1000) / 1000; 
            //Debug.Log("frequency - " + frequency);
            float phase = 0;
            float theta = math.trunc(((direction.x * pos.x) + (direction.z * pos.y/*z*/) - (frequency * Time * WaveData[wave].time) - phase) * 1000) / 1000;

            wavePos.x += -((direction.x / length) * (amplitude / tanh) * math.sin(theta)) ; 
            wavePos.y += amplitude * math.cos(theta) ;
            wavePos.z += -((direction.z / length) * (amplitude / tanh) * math.sin(theta));
        }
    }
}
