// Scripts/SdfUpdateJob.cs

using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

// BurstCompile 属性告诉Unity使用Burst编译器来优化这个Job
[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
public struct SdfUpdateJob : IJobParallelFor
{
    // --- 输入数据 ---
    
    // [ReadOnly] 属性告诉Job系统，这个数据在Job执行期间不会被修改，
    // 这允许系统进行更多优化，例如允许多个Job同时读取它。
    [ReadOnly] public NativeArray<ObstacleData> Obstacles;
    [ReadOnly] public int GridWidth;
    [ReadOnly] public int Resolution;

    // --- 输出数据 ---
    
    // [WriteOnly] 属性告诉Job系统，这个数据只会被写入。
    [WriteOnly] public NativeArray<float> Grid;

    // Execute方法是Job的核心。它会为Grid中的每一个元素并行地执行一次。
    // 'index' 参数就是当前正在处理的Grid数组的索引。
    public void Execute(int index)
    {
        // 1. 将一维索引转换为二维的网格坐标
        int gridX = index % GridWidth;
        int gridY = index / GridWidth;

        // 2. 将网格坐标转换为像素坐标
        float px = gridX * Resolution;
        float py = gridY * Resolution;
        float2 point = new float2(px, py);

        // 3. 计算该点到所有障碍物的最短距离
        float minDistance = float.PositiveInfinity;
        for (int i = 0; i < Obstacles.Length; i++)
        {
            ObstacleData obs = Obstacles[i];

            // 使用Unity.Mathematics库的函数，这些函数可以被Burst高效编译
            float2 d = math.max(obs.BottomLeftPos - point, 0f) + math.max(point - (obs.BottomLeftPos + obs.Size), 0f);
            float distance = math.length(d);
            
            minDistance = math.min(minDistance, distance);
        }

        // 4. 将计算结果写入输出数组
        Grid[index] = minDistance;
    }
}

// Job不能使用引用类型（如class），所以我们创建一个struct来传递障碍物数据。
// struct是值类型，可以被Job安全地使用。
public struct ObstacleData
{
    public float2 BottomLeftPos;
    public float2 Size;
}