using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using RenderPipeline = UnityEngine.Rendering.RenderPipelineManager;

public class PortalCamera : MonoBehaviour
{
    [Header("门配置")]
    [SerializeField] private Portal[] portals = new Portal[2];
    [SerializeField] private Camera portalCamera;

    private RenderTexture tempTexture1;
    private RenderTexture tempTexture2;
    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = GetComponent<Camera>();

        // 保持你原本的 512x1024 分辨率，兼顾性能
        tempTexture1 = new RenderTexture(512, 1024, 24, RenderTextureFormat.ARGB32);
        tempTexture2 = new RenderTexture(512, 1024, 24, RenderTextureFormat.ARGB32);
    }

    private void Start()
    {
        // =========================================================
        // 【保留你的 HDR 注入】：这一块绝不删减
        // =========================================================
        float hdrIntensity = 5.0f;

        // ---------------- 处理门 1 ----------------
        MaterialPropertyBlock block1 = new MaterialPropertyBlock();
        portals[0].Renderer.GetPropertyBlock(block1);
        block1.SetTexture("_MainTex", tempTexture1);
        Vector4 color1 = portals[0].PortalColour * (hdrIntensity+1);
        block1.SetVector("_FresnelColor", color1);
        portals[0].Renderer.SetPropertyBlock(block1);

        Renderer frame1 = portals[0].transform.Find("PortalFrame").GetComponent<Renderer>();
        if (frame1 != null)
        {
            MaterialPropertyBlock frameBlock1 = new MaterialPropertyBlock();
            frame1.GetPropertyBlock(frameBlock1);
            frameBlock1.SetVector("_OutlineColour", color1);
            frame1.SetPropertyBlock(frameBlock1);
        }

        // ---------------- 处理门 2 ----------------
        MaterialPropertyBlock block2 = new MaterialPropertyBlock();
        portals[1].Renderer.GetPropertyBlock(block2);
        block2.SetTexture("_MainTex", tempTexture2);
        Vector4 color2 = portals[1].PortalColour * hdrIntensity;
        block2.SetVector("_FresnelColor", color2);
        portals[1].Renderer.SetPropertyBlock(block2);

        Renderer frame2 = portals[1].transform.Find("PortalFrame").GetComponent<Renderer>();
        if (frame2 != null)
        {
            MaterialPropertyBlock frameBlock2 = new MaterialPropertyBlock();
            frame2.GetPropertyBlock(frameBlock2);
            frameBlock2.SetVector("_OutlineColour", color2);
            frame2.SetPropertyBlock(frameBlock2);
        }
    }

    private void OnEnable()
    {
        RenderPipeline.beginCameraRendering += UpdateCamera;
    }

    private void OnDisable()
    {
        RenderPipeline.beginCameraRendering -= UpdateCamera;
    }

    private void OnDestroy()
    {
        if (tempTexture1 != null) tempTexture1.Release();
        if (tempTexture2 != null) tempTexture2.Release();
    }

    void UpdateCamera(ScriptableRenderContext SRC, Camera camera)
    {
        if (!portals[0].IsPlaced || !portals[1].IsPlaced) return;

        // --- 渲染门 1 ---
        if (portals[0].Renderer.isVisible)
        {
            portalCamera.targetTexture = tempTexture1;

            // 这里就是套娃层数：硬写 3 层，保证画面出来
            int iterations = 3;

            // 必须倒序，从最深层开始算
            for (int i = iterations - 1; i >= 0; --i)
            {
                RenderCamera(portals[0], portals[1], i, SRC);
            }
        }

        // --- 渲染门 2 ---
        if (portals[1].Renderer.isVisible)
        {
            portalCamera.targetTexture = tempTexture2;

            int iterations = 3;

            for (int i = iterations - 1; i >= 0; --i)
            {
                // 参数顺序绝对不能错
                RenderCamera(portals[1], portals[0], i, SRC);
            }
        }
    }

    private void RenderCamera(Portal inPortal, Portal outPortal, int iterationID, ScriptableRenderContext SRC)
    {
        Transform inTransform = inPortal.transform;
        Transform outTransform = outPortal.transform;

        Transform cameraTransform = portalCamera.transform;
        cameraTransform.position = transform.position;
        cameraTransform.rotation = transform.rotation;

        for (int i = 0; i <= iterationID; ++i)
        {
            Vector3 relativePos = inTransform.InverseTransformPoint(cameraTransform.position);
            relativePos = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativePos;
            cameraTransform.position = outTransform.TransformPoint(relativePos);

            Quaternion relativeRot = Quaternion.Inverse(inTransform.rotation) * cameraTransform.rotation;
            relativeRot = Quaternion.Euler(0.0f, 180.0f, 0.0f) * relativeRot;
            cameraTransform.rotation = outTransform.rotation * relativeRot;
        }

        // 斜截视锥体（完美切除门板）
        Plane p = new Plane(-outTransform.forward, outTransform.position);
        Vector4 clipPlaneWorldSpace = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
        Vector4 clipPlaneCameraSpace =
            Matrix4x4.Transpose(Matrix4x4.Inverse(portalCamera.worldToCameraMatrix)) * clipPlaneWorldSpace;

        var newMatrix = mainCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
        portalCamera.projectionMatrix = newMatrix;

        // 【绝杀】：不要隐藏门！让 CalculateObliqueMatrix 去切！
        // 如果你在这里隐藏门，套娃就会断。

        portalCamera.GetUniversalAdditionalCameraData().renderPostProcessing = false;

        UniversalRenderPipeline.RenderSingleCamera(SRC, portalCamera);
    }
}