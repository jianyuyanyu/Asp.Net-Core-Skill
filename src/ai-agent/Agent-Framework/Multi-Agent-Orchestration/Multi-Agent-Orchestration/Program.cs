// Copyright (c) Microsoft. All rights reserved.

using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using System.Text;
using System.Text.Json;

namespace WorkflowAgentsInWorkflowsSample;

/// <summary>
/// 本示例演示如何在工作流中将 AI 代理作为执行器使用，
/// 并通过 <see cref="AgentWorkflowBuilder"/> 将这些代理组合成几种常见模式之一。
/// </summary>
/// <remarks>
/// 前置条件：
/// - 必须已配置 Azure OpenAI 聊天补全部署。
/// </remarks>
public static class Program
{
    private static async Task Main()
    {

        Console.InputEncoding = Encoding.UTF8;
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        // 配置 Azure OpenAI 客户端。
        var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new InvalidOperationException("未设置 AZURE_OPENAI_ENDPOINT。");
        var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5.4-mini";
        var client = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential()).GetChatClient(deploymentName).AsIChatClient();

        Console.Write("请选择工作流类型（'sequential'、'concurrent'、'handoffs'、'groupchat'）：");

        var translationAgents = from lang in new[] { "法语", "中文", "西班牙语", "英语" } select GetTranslationAgent(lang, client);
        switch (Console.ReadLine())
        {
            case "sequential":
                await RunWorkflowAsync(
                    AgentWorkflowBuilder.BuildSequential(translationAgents), [new(ChatRole.User, "Hello，World！")]);
                break;
            case "concurrent":
                await RunWorkflowAsync(
                    AgentWorkflowBuilder.BuildConcurrent(translationAgents, results =>
                    {
                        var merged = new List<ChatMessage>();

                        foreach (var agentOutput in results)
                        {
                            merged.AddRange(agentOutput);
                        }
                        return merged;
                    }), [new(ChatRole.User, "Hello，World！")]);
                break;

            case "handoffs":
                ChatClientAgent historyTutor = new ChatClientAgent(client, "你负责回答历史相关问题。请清晰解释重要事件和背景。仅回答历史相关内容。", "history_tutor", "用于处理历史问题的专业代理");
                ChatClientAgent mathTutor = new ChatClientAgent(client, "你负责解答数学问题。请逐步解释你的推理过程，并包含示例。仅回答数学相关内容。", "math_tutor", "用于处理数学问题的专业代理");
                ChatClientAgent triageAgent = new ChatClientAgent(client, "你需要根据用户的作业问题决定应使用哪个代理。必须始终将问题交接给其他代理。", "triage_agent", "将消息路由到合适专业代理的分流代理");

#pragma warning disable MAAIW001
                var workflow = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
                    .WithHandoffs(triageAgent, [mathTutor, historyTutor])
                    .WithHandoffs([mathTutor, historyTutor], triageAgent)
                    .Build();
#pragma warning restore MAAIW001

                List<ChatMessage> messages = [];
                while (true)
                {
                    Console.Write("问题：");
                    messages.Add(new(ChatRole.User, Console.ReadLine()));
                    messages.AddRange(await RunWorkflowAsync(workflow, messages));
                }
                
            case "groupchat":
                await RunWorkflowAsync(
                    AgentWorkflowBuilder.CreateGroupChatBuilderWith(agents => new RoundRobinGroupChatManager(agents) { MaximumIterationCount = 5 })
                        .AddParticipants(translationAgents)
                        .WithName("翻译轮询工作流")
                        .WithDescription("一个由三个翻译代理按轮询方式依次响应的工作流。")
                        .Build(),
                    [new(ChatRole.User, "Hello，World！")]);
                break;

            default:
                throw new InvalidOperationException("无效的工作流类型。");
        }

        static async Task<List<ChatMessage>> RunWorkflowAsync(Workflow workflow, List<ChatMessage> messages)
        {
            string? lastExecutorId = null;

            await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, messages);
            await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
            await foreach (WorkflowEvent evt in run.WatchStreamAsync())
            {
                if (evt is AgentResponseUpdateEvent e)
                {
                    if (e.ExecutorId != lastExecutorId)
                    {
                        lastExecutorId = e.ExecutorId;
                        Console.WriteLine();
                        Console.WriteLine(e.ExecutorId);
                    }

                    Console.Write(e.Update.Text);
                    if (e.Update.Contents.OfType<FunctionCallContent>().FirstOrDefault() is FunctionCallContent call)
                    {
                        Console.WriteLine();
                        Console.WriteLine($"  [正在调用函数“{call.Name}”，参数：{JsonSerializer.Serialize(call.Arguments)}]");
                    }
                }
                else if (evt is WorkflowOutputEvent output)
                {
                    Console.WriteLine();
                    return output.As<List<ChatMessage>>()!;
                }
                else if (evt is WorkflowErrorEvent workflowError)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "发生未知工作流错误。");
                    Console.ResetColor();
                }
                else if (evt is ExecutorFailedEvent executorFailed)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Error.WriteLine($"执行器“{executorFailed.ExecutorId}”失败，{(executorFailed.Data == null ? "错误未知" : $"异常信息：{executorFailed.Data}")}。");
                    Console.ResetColor();
                }
            }

            return [];
        }
    }

    /// <summary>为指定目标语言创建一个翻译代理。</summary>
    private static ChatClientAgent GetTranslationAgent(string targetLanguage, IChatClient chatClient) =>
        new(chatClient,
            @$"你是一个翻译助手。请将用户输入翻译为：{targetLanguage}。只输出翻译结果，不要输出解释或语言名称。");
}