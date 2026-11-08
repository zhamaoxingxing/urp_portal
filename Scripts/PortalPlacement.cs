using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 传送门放置逻辑。
/// 负责监听玩家开枪输入、发射射线、处理射线穿过传送门的递归，以及计算传送门贴合墙面的旋转。
/// </summary>
[RequireComponent(typeof(CameraMove))] // 强制要求同物体上挂载 CameraMove 脚本，因为计算门的旋转需要用到摄像机的朝向
public class PortalPlacement : MonoBehaviour
{
    [SerializeField]
    private PortalPair portals; // 包含两个传送门的引用对

    [SerializeField]
    private LayerMask layerMask; // 射线检测的层遮罩（只打墙壁和门，忽略玩家、空气等）

    [SerializeField]
    private Crosshair crosshair; // UI 准星，用于更新门是否放置成功的状态

    private CameraMove cameraMove; // 缓存摄像机移动脚本

    private void Awake()
    {
        cameraMove = GetComponent<CameraMove>();
    }

    private void Update()
    {
        // 左键发射 1 号门（蓝色）
        if (Input.GetButtonDown("Fire1"))
        {
            Debug.Log("【Debug】检测到左键点击！");
            // 参数：门的ID，射线起点（摄像机位置），射线方向（摄像机正前方），最大距离 250 米
            FirePortal(0, transform.position, transform.forward, 250.0f);
        }
        // 右键发射 2 号门（橙色）
        else if (Input.GetButtonDown("Fire2"))
        {
            Debug.Log("【Debug】检测到右键点击！");
            FirePortal(1, transform.position, transform.forward, 250.0f);
        }
    }

    /// <summary>
    /// 核心射击逻辑（支持递归穿透）
    /// </summary>
    private void FirePortal(int portalID, Vector3 pos, Vector3 dir, float distance)
    {
        RaycastHit hit;
        // 发射射线
        Physics.Raycast(pos, dir, out hit, distance, layerMask);

        if (hit.collider != null)
        {
            Debug.Log("【Debug】射线打中物体了: " + hit.collider.name + "，所在层: " + LayerMask.LayerToName(hit.collider.gameObject.layer));

            // ==================== 情况一：射线打中了传送门 ====================
            if (hit.collider.tag == "Portal")
            {
                var inPortal = hit.collider.GetComponent<Portal>();

                if (inPortal == null)
                {
                    Debug.LogWarning("【Debug】打中Portal标签，但没找到Portal组件！");
                    return;
                }

                var outPortal = inPortal.OtherPortal;

                // 计算射线从出口门射出的新起点（pos）
                Vector3 relativePos = inPortal.transform.InverseTransformPoint(hit.point + dir);
                relativePos = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativePos;
                pos = outPortal.transform.TransformPoint(relativePos);

                // 计算射线从出口门射出的新方向（dir）
                Vector3 relativeDir = inPortal.transform.InverseTransformDirection(dir);
                relativeDir = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativeDir;
                dir = outPortal.transform.TransformDirection(relativeDir);

                // 扣除距离
                distance -= Vector3.Distance(pos, hit.point);

                // 递归
                FirePortal(portalID, pos, dir, distance);
                return;
            }

            // ==================== 情况二：射线打中了实体墙面 ====================

            // 获取摄像机当前的旋转
            var cameraRotation = cameraMove.TargetRotation;
            var portalRight = cameraRotation * Vector3.right;

            // 对齐 X/Z 轴
            if (Mathf.Abs(portalRight.x) >= Mathf.Abs(portalRight.z))
            {
                portalRight = (portalRight.x >= 0) ? Vector3.right : -Vector3.right;
            }
            else
            {
                portalRight = (portalRight.z >= 0) ? Vector3.forward : -Vector3.forward;
            }

            // 根据法线计算朝向
            var portalForward = -hit.normal;
            var portalUp = -Vector3.Cross(portalRight, portalForward);
            var portalRotation = Quaternion.LookRotation(portalForward, portalUp);

            Debug.Log("【Debug】准备放置传送门，当前选的ID是: " + portalID);

            // 尝试把传送门放置到墙上
            bool wasPlaced = portals.Portals[portalID].PlacePortal(hit.collider, hit.point, portalRotation);

            if (wasPlaced)
            {
                Debug.Log("【Debug】传送门放置成功！");
                // 更新 UI 准星状态
                crosshair.SetPortalPlaced(portalID, true);
            }
            else
            {
                Debug.LogError("【Debug】传送门放置失败！可能是因为空间不够、重叠、或者墙壁太小。");
            }
        }
        else
        {
            Debug.Log("【Debug】什么都没打中！（检查 LayerMask 是否勾选了墙壁的层，或者墙壁没有 Collider）");
        }
    }
}