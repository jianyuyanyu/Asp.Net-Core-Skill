// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using _41_Switch_Case;


Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
// 设置Azure OpenAI 客户端
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5.4-mini";

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential()).GetChatClient(deploymentName).AsIChatClient();
// 创建代理
AIAgent spamDetectionAgent = GetSpamDetectionAgent(chatClient);
AIAgent emailAssistantAgent = GetEmailAssistantAgent(chatClient);

// 创建执行器    
var spamDetectionExecutor = new SpamDetectionExecutor(spamDetectionAgent);
var emailAssistantExecutor = new EmailAssistantExecutor(emailAssistantAgent);
var sendEmailExecutor = new SendEmailExecutor();
var handleSpamExecutor = new HandleSpamExecutor();
var handleUncertainExecutor = new HandleUncertainExecutor();

// 构建工作流，通过添加执行器并连接它们
WorkflowBuilder builder = new(spamDetectionExecutor);
builder.AddSwitch(spamDetectionExecutor, switchBuilder =>
            switchBuilder
            .AddCase(GetCondition(expectedDecision: SpamDecision.NotSpam), emailAssistantExecutor)
            .AddCase(GetCondition(expectedDecision: SpamDecision.Spam), handleSpamExecutor)
            .WithDefault(handleUncertainExecutor)
        )
        //邮件助手编写回复后，它将被发送到发送邮件执行器
        .AddEdge(emailAssistantExecutor, sendEmailExecutor)
        .WithOutputFrom(handleSpamExecutor, sendEmailExecutor, handleUncertainExecutor);
var workflow = builder.Build();

// 这个电子邮件的内容是模棱两可的，可能会被标记为垃圾邮件，也可能不会，这取决于模型的评估。
string email = Resources.Read("ambiguous_email.txt");

// 执行工作流
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, email));
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        Console.WriteLine($"{outputEvent}");
    }
    else if (evt is WorkflowErrorEvent workflowError)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "Unknown workflow error occurred.");
        Console.ResetColor();
    }
    else if (evt is ExecutorFailedEvent executorFailed)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"Executor '{executorFailed.ExecutorId}' failed with {(executorFailed.Data == null ? "unknown error" : $"exception {executorFailed.Data}")}.");
        Console.ResetColor();
    }
}
//}

/// <summary>
/// 创建一个用于根据预期的垃圾邮件检测结果路由消息的条件。
/// </summary>
/// <param name="expectedDecision">预期的垃圾邮件检测决策</param>
/// <returns>一个函数，用于评估消息是否符合预期结果</returns>
static Func<object?, bool> GetCondition(SpamDecision expectedDecision) => detectionResult => detectionResult is DetectionResult result && result.spamDecision == expectedDecision;

/// <summary>
/// 创建一个用于垃圾邮件检测的代理。
/// </summary>
/// <returns>一个配置为垃圾邮件检测的 ChatClientAgent</returns>
static ChatClientAgent GetSpamDetectionAgent(IChatClient chatClient) =>
   new(chatClient, new ChatClientAgentOptions()
   {
       ChatOptions = new()
       {
           Instructions = "你是一个垃圾邮件检测助手，负责识别垃圾邮件。请在评估中保持谨慎。理由中文输出",
           ResponseFormat = ChatResponseFormat.ForJsonSchema<DetectionResult>()
       }
   });

/// <summary>
/// 创建一个用于电子邮件助手的代理。
/// </summary>
/// <returns>一个配置为电子邮件助手的 ChatClientAgent</returns>
static ChatClientAgent GetEmailAssistantAgent(IChatClient chatClient) =>
   new(chatClient, new ChatClientAgentOptions()
   {
       ChatOptions = new()
       {
           Instructions = "你是一个电子邮件助手，帮助用户以专业的方式撰写电子邮件回复。",
           ResponseFormat = ChatResponseFormat.ForJsonSchema<EmailResponse>()
       }
   });
//}

internal static class EmailStateConstants
{
    public const string EmailStateScope = "EmailState";
}

/// <summary>
/// 表示垃圾邮件检测的可能决策。
/// </summary>
public enum SpamDecision
{
    NotSpam,
    Spam,
    Uncertain
}

/// <summary>
/// 表示垃圾邮件检测的结果。
/// </summary>
public sealed class DetectionResult
{
    [JsonPropertyName("spam_decision")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SpamDecision spamDecision { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; } = string.Empty;

    [JsonIgnore]
    public string EmailId { get; set; } = string.Empty;
}

/// <summary>
/// 表示一封电子邮件。
/// </summary>
internal sealed class Email
{
    [JsonPropertyName("email_id")]
    public string EmailId { get; set; } = string.Empty;

    [JsonPropertyName("email_content")]
    public string EmailContent { get; set; } = string.Empty;
}

/// <summary>
/// 表示电子邮件助手的响应。
/// </summary>
public sealed class EmailResponse
{
    [JsonPropertyName("response")]
    public string Response { get; set; } = string.Empty;
}
/// <summary>
/// 使用 AI 代理检测垃圾邮件的执行器。
/// </summary>
internal sealed class SpamDetectionExecutor : Executor<ChatMessage, DetectionResult>
{
    private readonly AIAgent _spamDetectionAgent;

    /// <summary>
    /// 创建一个新的 <see cref="SpamDetectionExecutor"/> 实例。
    /// </summary>
    /// <param name="spamDetectionAgent">用于垃圾邮件检测的 AI 代理</param>
    public SpamDetectionExecutor(AIAgent spamDetectionAgent) : base("SpamDetectionExecutor")
    {
        this._spamDetectionAgent = spamDetectionAgent;
    }

    public override async ValueTask<DetectionResult> HandleAsync(ChatMessage message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // 生成一个随机的电子邮件 ID 并存储电子邮件内容
        var newEmail = new Email
        {
            EmailId = Guid.NewGuid().ToString("N"),
            EmailContent = message.Text
        };
        await context.QueueStateUpdateAsync(newEmail.EmailId, newEmail, scopeName: EmailStateConstants.EmailStateScope, cancellationToken);
        // 调用代理
        var response = await this._spamDetectionAgent.RunAsync(message, cancellationToken: cancellationToken);
        var detectionResult = JsonSerializer.Deserialize<DetectionResult>(response.Text);

        detectionResult!.EmailId = newEmail.EmailId;

        return detectionResult;
    }
}
/// <summary>
/// 使用 AI 代理协助电子邮件响应的执行器。   
/// </summary>
internal sealed class EmailAssistantExecutor : Executor<DetectionResult, EmailResponse>
{
    private readonly AIAgent _emailAssistantAgent;

    /// <summary>
    /// 创建一个新的 <see cref="EmailAssistantExecutor"/> 实例。
    /// </summary>
    /// <param name="emailAssistantAgent">用于电子邮件协助的 AI 代理</param>
    public EmailAssistantExecutor(AIAgent emailAssistantAgent) : base("EmailAssistantExecutor")
    {
        this._emailAssistantAgent = emailAssistantAgent;
    }

    public override async ValueTask<EmailResponse> HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message.spamDecision == SpamDecision.Spam)
        {
            throw new InvalidOperationException("这个执行器只应处理非垃圾邮件消息。");
        }

        // 从上下文中检索电子邮件内容
        var email = await context.ReadStateAsync<Email>(message.EmailId, scopeName: EmailStateConstants.EmailStateScope, cancellationToken);

        // 调用代理
        var response = await this._emailAssistantAgent.RunAsync(email!.EmailContent, cancellationToken: cancellationToken);
        var emailResponse = JsonSerializer.Deserialize<EmailResponse>(response.Text);

        return emailResponse!;
    }
}

/// <summary>
/// 发送电子邮件的执行器。它从电子邮件助手执行器接收响应，并模拟发送电子邮件的过程。
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class SendEmailExecutor() : Executor<EmailResponse>("SendEmailExecutor")
{
    /// <summary>
    /// 模拟发送电子邮件的过程。
    /// </summary>
    public override async ValueTask HandleAsync(EmailResponse message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        await context.YieldOutputAsync($"邮件已发送: {message.Response}", cancellationToken);
}

/// <summary>
/// 执行器，用于处理垃圾邮件。它从垃圾邮件检测执行器接收消息，并模拟处理垃圾邮件的过程。
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class HandleSpamExecutor() : Executor<DetectionResult>("HandleSpamExecutor")
{
    /// <summary>
    /// 模拟处理垃圾邮件的情况。
    /// </summary>
    public override async ValueTask HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message.spamDecision == SpamDecision.Spam)
        {
            await context.YieldOutputAsync($"邮件标记为垃圾邮件: {message.Reason}", cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("这个执行器只应处理垃圾邮件消息。");
        }
    }
}

/// <summary>
/// 执行器，用于处理垃圾邮件检测结果不确定的情况。它从垃圾邮件检测执行器接收消息，并模拟处理不确定结果的过程。
/// </summary>
[YieldsOutput(typeof(string))]
internal sealed class HandleUncertainExecutor() : Executor<DetectionResult>("HandleUncertainExecutor")
{
    /// <summary>
    /// 模拟处理垃圾邮件检测结果不确定的情况。
    /// </summary>
    /// <param name="message"></param>
    /// <param name="context"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    public override async ValueTask HandleAsync(DetectionResult message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        if (message.spamDecision == SpamDecision.Uncertain)
        {
            var email = await context.ReadStateAsync<Email>(message.EmailId, scopeName: EmailStateConstants.EmailStateScope, cancellationToken);
            await context.YieldOutputAsync($"邮件标记为不确定: {message.Reason}. 邮件内容: {email?.EmailContent}", cancellationToken);
        }
        else
        {
            throw new InvalidOperationException("这个执行器只应处理不确定的垃圾邮件决策。");
        }
    }
}