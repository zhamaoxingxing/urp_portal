using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class CameraMove : MonoBehaviour
{
    private const float moveSpeed = 7.5f;
    private const float cameraSpeed = 3.0f;

    public Quaternion TargetRotation { private set; get; }

    private Vector3 moveVector = Vector3.zero;

    private new Rigidbody rigidbody;

    private void Awake()
    {
        rigidbody = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
        TargetRotation = transform.rotation;
    }

    private void Update()
    {
        // 1. 鼠标控制视角（上下左右看）
        var rotation = new Vector2(-Input.GetAxis("Mouse Y"), Input.GetAxis("Mouse X"));
        var targetEuler = TargetRotation.eulerAngles + (Vector3)rotation * cameraSpeed;

        if (targetEuler.x > 180.0f) targetEuler.x -= 360.0f;
        targetEuler.x = Mathf.Clamp(targetEuler.x, -85.0f, 85.0f); // 允许看到几乎垂直的天空
        TargetRotation = Quaternion.Euler(targetEuler);

        // 直接对齐，防止穿门抖动
        transform.rotation = TargetRotation;

        // 2. 获取 WASD 输入
        float x = Input.GetAxis("Horizontal"); // A/D
        float z = Input.GetAxis("Vertical");   // W/S

        // 【核心修改】Minecraft 飞行逻辑
        // 前/后（W/S）沿着摄像机的“正前方（包含上下俯仰角）”飞
        // 左/右（A/D）沿着摄像机的“正右方”飞
        // 注意：这里把 Y 轴的移动也用 transform.forward 包含了，所以不需要额外的升降键
        moveVector = (transform.forward * z + transform.right * x).normalized * moveSpeed;
    }

    private void FixedUpdate()
    {
        // 直接将计算好的方向赋予刚体速度。
        // 因为不需要重力，所以直接等于 moveVector 即可。
        rigidbody.velocity = moveVector;
    }

    public void ResetTargetRotation()
    {
        TargetRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
    }
}