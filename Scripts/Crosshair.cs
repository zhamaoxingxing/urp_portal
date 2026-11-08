using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Crosshair : MonoBehaviour
{
    [SerializeField]
    private PortalPair portalPair;

    [SerializeField]
    private Image inPortalImg;

    [SerializeField]
    private Image outPortalImg;

    private void Start()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas.overrideSorting = true;

        // 设置初始颜色
        if (portalPair != null && portalPair.Portals != null && portalPair.Portals.Length == 2)
        {
            inPortalImg.color = portalPair.Portals[0].PortalColour;
            outPortalImg.color = portalPair.Portals[1].PortalColour;
        }

        // 【强制显示】把之前的 SetActive(false) 删掉，让准星开局就亮着！
        inPortalImg.gameObject.SetActive(true);
        outPortalImg.gameObject.SetActive(true);
    }

    // 【补回缺失的方法】当 PortalPlacement 脚本调用这个方法时，准星不会报错
    public void SetPortalPlaced(int portalID, bool isPlaced)
    {
        // 这里不管 isPlaced 是 true 还是 false，我们都强制保持显示（不隐藏准星）
        if (portalID == 0)
        {
            inPortalImg.gameObject.SetActive(true);
        }
        else
        {
            outPortalImg.gameObject.SetActive(true);
        }
    }
}