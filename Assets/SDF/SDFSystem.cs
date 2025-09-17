// Scripts/SDFSystem.cs

using UnityEngine;
using System.Collections.Generic;

// --- 引入Job System, Burst, 和 Mathematics 命名空间 ---
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

public class SDFSystem
{
    public float[] Grid { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int Resolution { get; private set; }

    private int screenWidth;
    private int screenHeight;
    
    // 用于存储从RectTransform转换来的障碍物数据
    private List<ObstacleData> obstacleDataCache = new List<ObstacleData>();

    public SDFSystem(int resolution = 4)
    {
        this.Resolution = Mathf.Max(1, resolution);
    }

    public void Init(int screenWidth, int screenHeight)
    {
        this.screenWidth = screenWidth;
        this.screenHeight = screenHeight;

        Width = Mathf.CeilToInt((float)screenWidth / Resolution);
        Height = Mathf.CeilToInt((float)screenHeight / Resolution);
        Grid = new float[Width * Height];
        Clear();
    }

    public void Clear()
    {
        for (int i = 0; i < Grid.Length; i++)
        {
            Grid[i] = float.PositiveInfinity;
        }
    }

    // [核心改造] Update方法现在负责调度Job，而不是自己计算
    public void Update(List<RectTransform> obstacles)
    {
        // 1. 准备Job所需的数据
        // 将List<RectTransform> (托管数据) 转换为 List<ObstacleData> (值类型)
        obstacleDataCache.Clear();
        Vector2 screenCenter = new Vector2(screenWidth / 2f, screenHeight / 2f);
        foreach (var obs in obstacles)
        {
            Vector2 obsSize = obs.sizeDelta;
            Vector2 obsCenterInScreenSpace = screenCenter + (Vector2)obs.anchoredPosition;
            Vector2 obsBottomLeftPos = obsCenterInScreenSpace - obsSize * 0.5f;

            obstacleDataCache.Add(new ObstacleData
            {
                BottomLeftPos = new float2(obsBottomLeftPos.x, obsBottomLeftPos.y),
                Size = new float2(obsSize.x, obsSize.y)
            });
        }

        // 2. 将数据从托管内存(List)复制到原生内存(NativeArray)
        // NativeArray是Job可以安全访问的特殊数组类型。
        // Allocator.TempJob表示这是一个短暂的Job，内存分配会很快。
        NativeArray<ObstacleData> obstaclesNative = new NativeArray<ObstacleData>(obstacleDataCache.ToArray(), Allocator.TempJob);
        NativeArray<float> gridNative = new NativeArray<float>(Grid.Length, Allocator.TempJob);

        // 3. 创建并设置Job
        var sdfJob = new SdfUpdateJob
        {
            Obstacles = obstaclesNative,
            GridWidth = this.Width,
            Resolution = this.Resolution,
            Grid = gridNative
        };

        // 4. 调度Job
        // Schedule(数组长度, 批次大小) -> 将任务分发给工作线程
        // 批次大小（例如64）表示每个线程一次性处理64个元素，这有助于平衡调度开销
        JobHandle jobHandle = sdfJob.Schedule(Grid.Length, 64);

        // 5. 等待Job完成
        // 对于SDF，我们通常需要立即得到结果，所以我们调用Complete()来阻塞主线程直到所有工作线程完成任务。
        jobHandle.Complete();

        // 6. 从原生内存复制回托管内存
        gridNative.CopyTo(Grid);

        // 7. [!!!] 必须释放NativeArray的内存，否则会导致内存泄漏 [!!!]
        obstaclesNative.Dispose();
        gridNative.Dispose();
    }
    
    // 查询部分无需改动，它依然在主线程上运行
    public float Query(float x, float y)
    {
        float nx = x / Resolution;
        float ny = y / Resolution;
        int x1 = Mathf.FloorToInt(nx);
        int y1 = Mathf.FloorToInt(ny);
        
        int x2 = x1 + 1;
        int y2 = y1 + 1;
        float dx = nx - x1;
        float dy = ny - y1;

        return Lerp(
            Lerp(GetValue(x1, y1), GetValue(x2, y1), dx),
            Lerp(GetValue(x1, y2), GetValue(x2, y2), dx),
            dy
        );
    }
    
    private float GetValue(int x, int y)
    {
        x = Mathf.Clamp(x, 0, Width - 1);
        y = Mathf.Clamp(y, 0, Height - 1);
        return Grid[y * Width + x];
    }

    private float Lerp(float a, float b, float t)
    {
        return a + (b - a) * Mathf.Clamp01(t);
    }
}