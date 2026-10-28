using System;
using UnityEngine;

// 与 Go 后端一致的 JSON 字段。字段名必须与后端保持一致（小驼峰）。
[Serializable]
public class PositionDto
{
    public float x;
    public float y;
    public float z;

    public static PositionDto From(Vector3 v)
    {
        return new PositionDto { x = v.x, y = v.y, z = v.z };
    }
}

[Serializable]
public class IssueDto
{
    public string id;
    public string title;
    public string description;
    public string priority;
    public string status;
    public PositionDto position;
    public string createdAt;
    public string updatedAt;
}

[Serializable]
public class CreateIssueRequest
{
    public string title;
    public string description;
    public string priority;
    public PositionDto position;
}

[Serializable]
public class ApiErrorBody
{
    public string code;
    public string message;
}

[Serializable]
public class ApiErrorEnvelope
{
    public ApiErrorBody error;
}

// 表单提交时在客户端内部传递的草稿数据。
public class IssueDraft
{
    public string title;
    public string description;
    public string priority;
    public Vector3 position;
}

public static class IssueEnums
{
    public const string PriorityLow = "low";
    public const string PriorityMedium = "medium";
    public const string PriorityHigh = "high";
}
