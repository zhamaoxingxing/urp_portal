using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 传送门组件：负责传送门的放置、显示、以及物体穿越时的传送逻辑。
/// 视觉渲染（透过门看到的画面）由 PortalCamera.cs 负责，两者配合使用。
/// </summary>
[RequireComponent(typeof(BoxCollider))] // 强制要求挂载 BoxCollider，用作检测物体穿越的触发区域
public class Portal : MonoBehaviour
{
    // ==================== 序列化字段（可在 Inspector 中配置） ====================

    /// <summary>
    /// 配对的另一个传送门（入口对应出口，出口对应入口）。
    /// [field: SerializeField] 让属性背后的字段可以在 Inspector 中赋值，
    /// 同时对外保持 private set（外部只能读、不能改）。
    /// </summary>
    [field: SerializeField]
    public Portal OtherPortal { get; private set; }

    /// <summary>
    /// 传送门轮廓的渲染器（用于设置门框描边颜色）。
    /// </summary>
    [SerializeField]
    private Renderer outlineRenderer;

    /// <summary>
    /// 传送门颜色（会应用到轮廓材质的 _OutlineColour 上）。
    /// </summary>
    [field: SerializeField]
    public Color PortalColour { get; private set; }

    /// <summary>
    /// 放置检测使用的层遮罩：只有这些层的物体/墙面才被认为是"可放置的表面"。
    /// </summary>
    [SerializeField]
    private LayerMask placementMask;

    /// <summary>
    /// 用于"试放"的临时 Transform：放置时先在这里计算、修正位置，
    /// 全部检查通过后才真正应用到传送门自身。
    /// </summary>
    [SerializeField]
    private Transform testTransform;

    // ==================== 运行时私有字段 ====================

    /// <summary>
    /// 当前处于传送门触发区域内的可传送物体列表。
    /// 物体进入触发器时加入，离开时移除；越过门平面时执行传送。
    /// </summary>
    private List<PortalableObject> portalObjects = new List<PortalableObject>();

    /// <summary>
    /// 传送门是否已经被放置到墙上。PortalCamera 会用这个值判断是否渲染。
    /// </summary>
    public bool IsPlaced { get; private set; } = false;

    /// <summary>
    /// 传送门所贴附的那面墙的碰撞体（用于物体传送后做位置修正等）。
    /// </summary>
    private Collider wallCollider;

    // ==================== 组件缓存 ====================

    /// <summary>
    /// 传送门表面的渲染器。PortalCamera 会把渲染纹理赋给它的材质 mainTexture。
    /// </summary>
    public Renderer Renderer { get; private set; }

    /// <summary>
    /// 传送门的 BoxCollider，用作触发区域。
    /// 用 new 隐藏基类 MonoBehaviour 的 collider 字段（旧版 Unity 遗留）。
    /// </summary>
    private new BoxCollider collider;

    // ==================== 生命周期方法 ====================

    private void Awake()
    {
        // 缓存自身组件，避免频繁 GetComponent。
        collider = GetComponent<BoxCollider>();
        // Renderer = GetComponent<Renderer>();
        Renderer = GetComponentInChildren<Renderer>();
    }

    private void Start()
    {
        // 初始状态隐藏传送门，等待玩家放置到墙上。
        gameObject.SetActive(false);
    }

    private void Update()
    {
        // 只有配对的另一个门已经放置好，自己才显示（避免出现孤立的传送门）。
        Renderer.enabled = OtherPortal.IsPlaced;

        // 遍历当前在门内的所有物体，检测它们是否已经"穿过"门平面。
        for (int i = 0; i < portalObjects.Count; ++i)
        {
            // 把物体世界坐标转换到传送门的局部空间。
            Vector3 objPos = transform.InverseTransformPoint(portalObjects[i].transform.position);

            // 局部空间中 z > 0 表示物体已经越过门平面（门面朝 z 正方向）。
            if (objPos.z > 0.0f)
            {
                // 执行传送：把物体瞬移到另一侧。
                portalObjects[i].Warp();
            }
        }
    }

    // ==================== 触发检测（物体进出传送门） ====================

    private void OnTriggerEnter(Collider other)
    {
        // 尝试获取可传送物体组件，非可传送物体直接忽略。
        var obj = other.GetComponent<PortalableObject>();
        if (obj != null)
        {
            // 登记物体，并通知它：当前在哪个门里、会从哪个门出来、对应的墙是哪个。
            portalObjects.Add(obj);
            obj.SetIsInPortal(this, OtherPortal, wallCollider);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var obj = other.GetComponent<PortalableObject>();

        // 如果离开的物体确实在列表中，则移除并通知它已离开传送门。
        if (portalObjects.Contains(obj))
        {
            portalObjects.Remove(obj);
            obj.ExitPortal(wallCollider);
        }
    }

    // ==================== 放置逻辑 ====================

    /// <summary>
    /// 尝试把传送门放置到指定墙面上。
    /// </summary>
    /// <param name="wallCollider">被放置的墙面碰撞体</param>
    /// <param name="pos">期望放置的位置</param>
    /// <param name="rot">期望放置的朝向</param>
    /// <returns>放置成功返回 true，失败返回 false</returns>
    public bool PlacePortal(Collider wallCollider, Vector3 pos, Quaternion rot)//四元数
    {
        // 先在 testTransform 上试放，避免直接修改自身导致失败后状态残留。
        testTransform.position = pos;
        testTransform.rotation = rot;

        // 稍微往墙里挪一点，避免门面与墙面 z-fighting（深度冲突闪烁）。
        testTransform.position -= testTransform.forward * 0.05f;

        // 修正：防止传送门超出墙面边缘。
        FixOverhangs();

        // 修正：防止传送门插进墙里。
        FixIntersects();

        // 最终检查：门不能与已有几何体重叠，且四角都要贴在墙面上。
        if (CheckOverlap())
        {
            // 记录所贴附的墙、应用修正后的位置和朝向。
            this.wallCollider = wallCollider;
            transform.position = testTransform.position;
            transform.rotation = testTransform.rotation;

            // 激活并标记为已放置。
            gameObject.SetActive(true);
            IsPlaced = true;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 确保传送门不会超出墙面边缘：
    /// 从门的上下左右四个方向做检测，若门边缘会悬空，则把门往墙内平移。
    /// </summary>
    private void FixOverhangs()
    {
        // 门边缘的四个测试点（局部坐标，相对门中心）。
        var testPoints = new List<Vector3>
        {
            new Vector3(-1.1f,  0.0f, 0.1f), // 左
            new Vector3( 1.1f,  0.0f, 0.1f), // 右
            new Vector3( 0.0f, -2.1f, 0.1f), // 下
            new Vector3( 0.0f,  2.1f, 0.1f)  // 上
        };

        // 对应每个测试点的检测方向（向门边缘外侧）。
        var testDirs = new List<Vector3>
        {
             Vector3.right,   // 从左侧往右检测
            -Vector3.right,   // 从右侧往左检测
             Vector3.up,      // 从下往上检测
            -Vector3.up       // 从上往下检测
        };

        for (int i = 0; i < 4; ++i)
        {
            RaycastHit hit;
            // 把测试点和方向转换到世界空间。
            Vector3 raycastPos = testTransform.TransformPoint(testPoints[i]);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            // 如果测试点附近已经有墙面（球体检测命中），说明门边缘被墙包住，无需修正。
            if (Physics.CheckSphere(raycastPos, 0.05f, placementMask))
            {
                break;
            }
            // 否则从测试点向外射线检测，如果 2.1 米内没打到墙，说明门悬空了，
            // 需要把门朝墙的方向平移，直到边缘贴住墙面。
            else if (Physics.Raycast(raycastPos, raycastDir, out hit, 2.1f, placementMask))
            {
                var offset = hit.point - raycastPos;
                testTransform.Translate(offset, Space.World);
            }
        }
    }

    /// <summary>
    /// 确保传送门不会与墙体内部交叉：
    /// 从门中心向四个方向发射线，若门嵌进墙里，则把门往反方向推出来。
    /// </summary>
    private void FixIntersects()
    {
        // 四个检测方向。
        var testDirs = new List<Vector3>
        {
             Vector3.right,
            -Vector3.right,
             Vector3.up,
            -Vector3.up
        };

        // 对应方向的最大检测距离（左右 1.1，上下 2.1，和门的尺寸匹配）。
        var testDists = new List<float> { 1.1f, 1.1f, 2.1f, 2.1f };

        for (int i = 0; i < 4; ++i)
        {
            RaycastHit hit;
            // 从门中心稍微往墙内偏移一点的位置开始射线。
            Vector3 raycastPos = testTransform.TransformPoint(0.0f, 0.0f, -0.1f);
            Vector3 raycastDir = testTransform.TransformDirection(testDirs[i]);

            // 如果射线在门尺寸范围内打到墙，说明门嵌进墙里了，需要往外推。
            if (Physics.Raycast(raycastPos, raycastDir, out hit, testDists[i], placementMask))
            {
                var offset = (hit.point - raycastPos);
                // 计算需要往反方向推出的距离。
                var newOffset = -raycastDir * (testDists[i] - offset.magnitude);
                testTransform.Translate(newOffset, Space.World);
            }
        }
    }

    /// <summary>
    /// 最终检查：传送门是否与其它几何体重叠，以及四角是否都贴在有效墙面上。
    /// </summary>
    /// <returns>通过检查返回 true，否则 false</returns>
    private bool CheckOverlap()
    {
        // 检测盒的半尺寸（略小于门本身，避免边缘误判）。
        var checkExtents = new Vector3(0.9f, 1.9f, 0.05f);

        // 检测用的一组位置：中心点、四个角点、以及一个"前方"参考点。
        var checkPositions = new Vector3[]
        {
            // 门中心（稍微往墙内偏移）。
            testTransform.position + testTransform.TransformVector(new Vector3( 0.0f,  0.0f, -0.1f)),

            // 四个角点。
            testTransform.position + testTransform.TransformVector(new Vector3(-1.0f, -2.0f, -0.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3(-1.0f,  2.0f, -0.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3( 1.0f, -2.0f, -0.1f)),
            testTransform.position + testTransform.TransformVector(new Vector3( 1.0f,  2.0f, -0.1f)),

            // 用于 Linecast 的偏移方向（从角点射向墙面）。
            testTransform.TransformVector(new Vector3(0.0f, 0.0f, 0.2f))
        };

        // 1) 检查门是否与已有物体/墙面重叠（用 OverlapBox）。
        var intersections = Physics.OverlapBox(checkPositions[0], checkExtents, testTransform.rotation, placementMask);

        if (intersections.Length > 1)
        {
            // 命中多个物体，说明门与其它东西重叠，放置失败。
            return false;
        }
        else if (intersections.Length == 1)
        {
            // 只命中一个物体：只有它是"旧传送门自身"时才允许（即移动同一个门）。
            if (intersections[0] != collider)
            {
                return false;
            }
        }

        // 2) 检查门的四个角是否都落在墙面上（用 Linecast）。
        bool isOverlapping = true;

        for (int i = 1; i < checkPositions.Length - 1; ++i)
        {
            // 每个角点朝墙方向做 Linecast，全部命中才算贴在墙上。
            isOverlapping &= Physics.Linecast(checkPositions[i],
                checkPositions[i] + checkPositions[checkPositions.Length - 1], placementMask);
        }

        return isOverlapping;
    }

    /// <summary>
    /// 移除传送门：隐藏并标记为未放置。
    /// 注意：这里没有清理 portalObjects 列表，若门内仍有物体，建议补充处理。
    /// </summary>
    public void RemovePortal()
    {
        gameObject.SetActive(false);
        IsPlaced = false;
    }
}