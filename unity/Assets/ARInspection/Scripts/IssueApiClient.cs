using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// 极简 Go 后端客户端：只实现"上报问题"这一个 Unity 端需要的接口。
/// 返回值为 IEnumerator，由 MonoBehaviour 通过 StartCoroutine 驱动。
/// </summary>
public class IssueApiClient
{
    readonly string baseUrl;

    public IssueApiClient(string baseUrl)
    {
        this.baseUrl = (baseUrl ?? string.Empty).TrimEnd('/');
    }

    public IEnumerator CreateIssue(IssueDraft draft, Action<IssueDto> onSuccess, Action<string> onError)
    {
        if (string.IsNullOrEmpty(baseUrl))
        {
            onError?.Invoke("未配置后端地址，请在场景中的 ARInspectionBootstrap 上填写后端地址");
            yield break;
        }

        var payload = new CreateIssueRequest
        {
            title = draft.title,
            description = draft.description,
            priority = draft.priority,
            position = PositionDto.From(draft.position),
        };
        byte[] body = Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload));

        using (var request = new UnityWebRequest(baseUrl + "/api/issues", "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(body);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 10;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(ExtractError(request));
                yield break;
            }

            IssueDto issue = null;
            try
            {
                issue = JsonUtility.FromJson<IssueDto>(request.downloadHandler.text);
            }
            catch (Exception e)
            {
                onError?.Invoke("后端返回内容无法解析：" + e.Message);
                yield break;
            }

            if (issue == null || string.IsNullOrEmpty(issue.id))
            {
                onError?.Invoke("后端返回内容缺少问题 ID");
                yield break;
            }

            onSuccess?.Invoke(issue);
        }
    }

    // 优先展示后端返回的 error.message，其次展示网络层错误。
    static string ExtractError(UnityWebRequest request)
    {
        string text = request.downloadHandler != null ? request.downloadHandler.text : null;
        if (!string.IsNullOrEmpty(text))
        {
            try
            {
                var envelope = JsonUtility.FromJson<ApiErrorEnvelope>(text);
                if (envelope != null && envelope.error != null && !string.IsNullOrEmpty(envelope.error.message))
                {
                    return envelope.error.message;
                }
            }
            catch
            {
                // 非 JSON 响应，继续走下面的兜底文案
            }
        }

        if (request.result == UnityWebRequest.Result.ConnectionError)
        {
            return "连接后端失败，请检查手机与电脑是否在同一网络、后端地址与端口是否正确（" + request.error + "）";
        }
        return "请求失败（HTTP " + request.responseCode + "）：" + request.error;
    }
}
