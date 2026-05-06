// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;

/// <summary>
/// 此示例介绍了如何在工作流中将 AI 代理用作执行器。
///
/// 此工作流不是使用简单的文本处理执行器，而是使用三个翻译代理：
/// 1. 法语代理 - 将输入文本翻译为法语
/// 2. 西班牙语代理 - 将法语文本翻译为西班牙语
/// 3. 英语代理 - 将西班牙语文本再翻译回英语
/// 4. 日语代理 - 将英语文本翻译为日语
/// 5. 中文代理- 将日语文本翻译为中文
///
/// 这些代理按顺序连接，形成一个翻译链，用于演示
/// 如何将 AI 驱动的组件无缝集成到工作流管道中。
/// </summary>
/// <remarks>
/// 先决条件：
/// - 必须已配置 Azure OpenAI 聊天补全部署。
/// </remarks>
public static class Program
{
    private static async Task Main()
    {
        // 设置 Azure OpenAI 客户端
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5.4-mini";
        var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential()).GetChatClient(deploymentName).AsIChatClient();

        // 创建代理
        AIAgent frenchAgent = GetTranslationAgent("French", chatClient);
        AIAgent spanishAgent = GetTranslationAgent("Spanish", chatClient);
        AIAgent englishAgent = GetTranslationAgent("English", chatClient);

        // 通过添加执行器并连接它们来构建工作流
        var workflow = new WorkflowBuilder(frenchAgent)
            .AddEdge(frenchAgent, spanishAgent)
            .AddEdge(spanishAgent, englishAgent)
            .Build();

        //执行工作流
        await using StreamingRun streamingRun = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, "Hello World!"));

        // 必须发送轮次令牌以触发这些代理。
        // 这些代理被包装为执行器。当它们接收到消息时，
        // 会先缓存消息，并且只有在收到 TurnToken 时才会开始处理。
        await streamingRun.TrySendMessageAsync(new TurnToken(emitEvents: true));
        await foreach (WorkflowEvent evt in streamingRun.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent executorComplete)
            {
                Console.WriteLine($"{executorComplete.ExecutorId}: {executorComplete.Data}");
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

    }

    /// <summary>
    /// 为指定的目标语言创建一个翻译代理。
    /// </summary>
    /// <param name="targetLanguage">翻译的目标语言</param>
    /// <param name="chatClient">代理要使用的聊天客户端</param>
    /// <returns>一个已针对指定语言完成配置的 ChatClientAgent</returns>
    private static ChatClientAgent GetTranslationAgent(string targetLanguage, IChatClient chatClient) =>
        new(chatClient, $"你是一个翻译助手，将提供的文本翻译为 {targetLanguage}。");
}