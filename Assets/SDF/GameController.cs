// Scripts/GameController.cs

using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEditor;

public class GameController : MonoBehaviour
{
    [Header("Object References")]
    public RectTransform playerRect;
    public RectTransform obstacleContainer;
    public GameObject obstaclePrefab;
    public RawImage debugViewImage;

    [Header("Player Settings")]
    public float playerSpeed = 250f;
    public float playerRadius = 20f;

    [Header("SDF Settings")]
    [Range(1, 40)]
    public int sdfResolution = 4;
    
    private SDFSystem sdf;
    private List<RectTransform> obstacles = new List<RectTransform>();
    private Texture2D debugTexture;
    private Color32[] debugPixels;
    private bool isDebugViewActive = false;
    
    void Start()
    {
        // 确保Debug视图初始时是关闭的
        debugViewImage.gameObject.SetActive(false);

        // 初始化SDF系统
        sdf = new SDFSystem(sdfResolution);
        sdf.Init(1920, 1080);

        // 创建初始障碍物
        GenerateInitialObstacles();

        // 更新SDF
        sdf.Update(obstacles);
    }

    void Update()
    {
        HandlePlayerMovement();
        HandleInput();
    }

    void HandlePlayerMovement()
    {
        // 1. 获取输入向量
        Vector2 inputDir = Vector2.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) inputDir.y = 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) inputDir.y = -1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) inputDir.x = -1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) inputDir.x = 1;
        
        if (inputDir == Vector2.zero) return;

        inputDir.Normalize();

        // 2. 计算目标位置
        float speedPerDeltaTime = playerSpeed * Time.deltaTime;
        Vector2 currentPos = playerRect.anchoredPosition;
        Vector2 targetPos = currentPos + inputDir * speedPerDeltaTime;

        // 3. 安全移动
        Vector2 safePos = SafeMove(currentPos, targetPos, speedPerDeltaTime);
        playerRect.anchoredPosition = safePos;
    }

    Vector2 SafeMove(Vector2 from, Vector2 to, float speedPerDeltaTime)
    {
        // 分段步进检查
        int steps = Mathf.Max(sdf.Resolution, Mathf.CeilToInt(speedPerDeltaTime)) + 1;
        Vector2 current = from;

        for (int i = 0; i < steps; i++)
        {
            float t = (i + 1) / (float)steps;
            Vector2 testPos = Vector2.Lerp(from, to, t);

            // 将UI坐标(中心pivot, y向上)转换为SDF查询坐标(左下角原点)
            Vector2 queryPos = testPos + new Vector2(1920 / 2f, 1080 / 2f);
            float dist = sdf.Query(queryPos.x, queryPos.y);

            if (dist > playerRadius)
            {
                current = testPos;
            }
            else
            {
                // 发生碰撞，计算滑动向量
                Vector2 normal = GetCollisionNormal(queryPos.x, queryPos.y);
                Vector2 remainingMovement = (to - testPos);
                
                // 使用Vector2.Dot计算投影
                float dot = Vector2.Dot(remainingMovement, normal);
                Vector2 slideVector = remainingMovement - normal * dot;

                // 应用滑动并跳出循环
                current += slideVector * 0.5f; // 乘以一个系数防止过冲
                break;
            }
        }
        return current;
    }

    Vector2 GetCollisionNormal(float x, float y)
    {
        // 用中心差分法计算梯度
        const float delta = 0.1f;
        float dx = (sdf.Query(x + delta, y) - sdf.Query(x - delta, y)) / (2 * delta);
        float dy = (sdf.Query(x, y + delta) - sdf.Query(x, y - delta)) / (2 * delta);
        
        Vector2 gradient = new Vector2(dx, dy);
        gradient.Normalize();

        return gradient.magnitude > 0 ? gradient : Vector2.up;
    }

    void HandleInput()
    {
        // 点击添加障碍物
        if (Input.GetMouseButtonDown(0))
        {
            AddObstacleAtMousePosition();
        }

        // 空格切换调试视图
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isDebugViewActive = !isDebugViewActive;
            debugViewImage.gameObject.SetActive(isDebugViewActive);
            if (isDebugViewActive)
            {
                RenderDebugView();
            }
        }
    }

    void AddObstacleAtMousePosition()
    {
        Vector2 mousePos;
        // 将屏幕坐标转换为Canvas内的局部坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            obstacleContainer, Input.mousePosition, null, out mousePos
        );
        
        // 我们需要的是相对于Canvas左下角的坐标，但这里得到的是相对于中心的，直接用即可
        AddObstacle(mousePos, new Vector2(50, 50));

        // 更新SDF
        sdf.Update(obstacles);
        
        // 如果调试视图是开启的，则刷新
        if (isDebugViewActive)
        {
            RenderDebugView();
        }
    }
    
    void AddObstacle(Vector2 anchoredPosition, Vector2 size)
    {
        GameObject newObsObj = new GameObject("Obstacle", typeof(Image));
        newObsObj.transform.SetParent(obstacleContainer, false);
        newObsObj.GetComponent<Image>().color = new Color(0.52f, 0.85f, 0.98f); // #85DAF9

        RectTransform obsRect = newObsObj.GetComponent<RectTransform>();
        obsRect.anchoredPosition = anchoredPosition;
        obsRect.sizeDelta = size;
        
        obstacles.Add(obsRect);
    }
    
    void GenerateInitialObstacles()
    {
        // 模板数据，注意这里的坐标是相对于屏幕中心(0,0)的
        // 需要从左下角(0,0)坐标系转换
        var template = new[]
        {
            new { x = 300, y = 500, w = 200, h = 100 },
            new { x = 600, y = 300, w = 100, h = 300 },
            new { x = 800, y = 200, w = 100, h = 100 },
            new { x = 1000, y = 100, w = 100, h = 100 },
            new { x = 1100, y = 400, w = 150, h = 150 },
            new { x = 800, y = 600, w = 100, h = 100 },
            new { x = 1000, y = 700, w = 400, h = 100 },
        };

        foreach (var t in template)
        {
            // 将左下角坐标转换为UGUI中心点坐标
            float anchoredX = t.x + t.w / 2f - 1920 / 2f;
            float anchoredY = t.y + t.h / 2f - 1080 / 2f;
            AddObstacle(new Vector2(anchoredX, anchoredY), new Vector2(t.w, t.h));
        }
    }
    
    void RenderDebugView()
    {
        int width = 1920;
        int height = 1080;

        if (debugTexture == null)
        {
            debugTexture = new Texture2D(width, height, TextureFormat.RGB24, false);
            debugViewImage.texture = debugTexture;
            debugPixels = new Color32[width * height];
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dist = sdf.Query(x, y);
                byte c = (byte)Mathf.Clamp(Mathf.Abs(dist) * 3, 0, 255);
                debugPixels[y * width + x] = new Color32(c, c, c, 255);
            }
        }
        
        debugTexture.SetPixels32(debugPixels);
        debugTexture.Apply();
    }
    
    // ... 在脚本的其他代码下方 ...
    [Header("Debugging")]
    public bool showSdfGridGizmos = false; // <--- 添加这一行
    /// <summary>
    /// 使用Gizmos在Scene视图中绘制SDF距离，仅用于编辑器调试。
    /// </summary>
    void OnDrawGizmos()
    {
        // 确保只在游戏运行时并且必要组件都已初始化时才绘制
        if (sdf == null || playerRect == null || !Application.isPlaying)
        {
            return;
        }

        // --- 1. 计算SDF距离 ---
        // 获取玩家在SDF坐标系中的位置 (左下角为0,0)
        Vector2 playerSdfQueryPos = playerRect.anchoredPosition + new Vector2(1920 / 2f, 1080 / 2f);
        // 查询SDF获取到最近障碍物的距离 (这将作为圆的半径)
        float distance = sdf.Query(playerSdfQueryPos.x, playerSdfQueryPos.y);

        // --- 2. 准备绘制参数 ---
        // Gizmos绘制是在世界坐标系中进行的，我们需要获取Player UI元素的世界坐标
        Vector3 circleCenter = playerRect.position; 
        float circleRadius = distance;

        // --- 3. 开始绘制 ---
        // 设置Gizmo的颜色
        Gizmos.color = new Color(1f, 0.43f, 0.64f); // 粉红色

        // 绘制一个线框球体（在2D视图下看起来就是一个圆）
        Gizmos.DrawWireSphere(circleCenter, circleRadius);
        
        
        // 定义屏幕尺寸，用于坐标转换
        float screenWidth = 1920f;
        float screenHeight = 1080f;

        // --- a. 绘制网格线 ---
        // 设置网格线颜色（半透明的白色，不那么刺眼）
        Gizmos.color = new Color(1f, 1f, 1f, 0.15f);

        // 绘制所有水平线
        for (int y = 0; y <= sdf.Height; y++)
        {
            // 计算起点（最左边）和终点（最右边）的世界坐标
            Vector3 startPos = GetWorldPositionForGridPoint(0, y);
            Vector3 endPos = GetWorldPositionForGridPoint(sdf.Width, y);
            Gizmos.DrawLine(startPos, endPos);
        }

        // 绘制所有垂直线
        for (int x = 0; x <= sdf.Width; x++)
        {
            // 计算起点（最下边）和终点（最上边）的世界坐标
            Vector3 startPos = GetWorldPositionForGridPoint(x, 0);
            Vector3 endPos = GetWorldPositionForGridPoint(x, sdf.Height);
            Gizmos.DrawLine(startPos, endPos);
        }

        // --- b. 在网格点上标注SDF数值 ---
#if UNITY_EDITOR
        for (int y = 0; y < sdf.Height; y++)
        {
            for (int x = 0; x < sdf.Width; x++)
            {
                float sdfValue = sdf.Grid[y * sdf.Width + x];
                if (float.IsInfinity(sdfValue)) continue;

                // 获取网格点的世界坐标
                Vector3 worldPos = GetWorldPositionForGridPoint(x, y);

                // 根据SDF值设定文本颜色
                float normalizedValue = Mathf.Clamp01(sdfValue / 100f);
                Color labelColor = Color.Lerp(Color.red, Color.green, normalizedValue);

                // 绘制SDF数值文本
                string label = sdfValue.ToString("F1");
                GUIStyle style = new GUIStyle();
                style.normal.textColor = labelColor;
                style.fontSize = 8; // 调小字号，避免过于拥挤
                style.alignment = TextAnchor.MiddleCenter;
                Handles.Label(worldPos, label, style);
            }
        }
#endif
    }
    
    // 辅助方法：将网格坐标转换为世界坐标 (这是一个新方法，请添加到OnDrawGizmos下面)
    private Vector3 GetWorldPositionForGridPoint(int gridX, int gridY)
    {
        if (sdf == null) return Vector3.zero;

        float screenWidth = 1920f;
        float screenHeight = 1080f;
        // 对应的像素坐标(左下角为0,0)
        float pixelX = gridX * sdf.Resolution;
        float pixelY = gridY * sdf.Resolution;
        // 转换为以屏幕中心为(0,0)的世界坐标
        return new Vector3(
            pixelX ,
            pixelY ,
            0
        );
    }
}