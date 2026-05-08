
在上一节中我们介绍了Agent Framework Workflow的基本概念和一些常用类和组件。

接下来，我们将使用 Workflow 对 Agent 进行基础编排。首先，创建一个简单的 AI Agent。

## 创建 AI Agent

在 C# 中，一个 Agent 通常会基于一个聊天模型客户端创建。

例如，可以先准备一个 Azure OpenAI 的 chat client：

```csharp
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-5.4-mini";

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

关于创建基础Agent的细节，可以参考之前的文章：


## 给 Agent 分配具体的任务

创建 Agent 只是第一步，更重要的是明确它做什么任务。

通常通过系统提示词来定义：

```csharp
private static ChatClientAgent GetTranslationAgent(string targetLanguage,IChatClient chatClient)
{
  return new ChatClientAgent(chatClient, $"你是一个翻译助手，将提供的文本翻译为 {targetLanguage}.");
}
```

通过传入不同语言，就可以得到不同角色的 Agent：

```
ChatClientAgent frenchAgent = GetTranslationAgent("法语", chatClient);
ChatClientAgent spanishAgent = GetTranslationAgent("西班牙语", chatClient);
ChatClientAgent englishAgent = GetTranslationAgent("英语", chatClient);
```

## 使用 Workflow 对多个 Agent进行编排

有了多个Agent 后，我们就可以使用Workflow对它们进行编排了。这里我们介绍最简单的编排方式：Sequential（顺序执行）。
后面我们还会介绍其他更复杂的编排方式。

```csharp
var workflow = new WorkflowBuilder(frenchAgent)
    .AddEdge(frenchAgent, spanishAgent)
    .AddEdge(spanishAgent, englishAgent)
    .Build();
```

这段代码定义了一条顺序执行链路：



## 运行Workflow

`Workflow` 构建完成后，可以通过以下方式启动运行：

```csharp
await using StreamingRun streamingRun = await InProcessExecution.RunStreamingAsync(workflow, new ChatMessage(ChatRole.User, "Hello World!"));
// 必须发送轮次令牌以触发Agent。这些Agent被包装为执行器。当它们接收到消息时，会先缓存消息，并且只有在收到 TurnToken 时才会开始处理。
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
```

## 触发执行：TurnToken

接下来这一步非常关键：

```csharp
await run.TrySendMessageAsync(new TurnToken(emitEvents: true));
```

这段代码可以理解为执行触发信号。驱动 Workflow 开始处理。

## 监听执行过程（Streaming Workflow）

这段代码前面已经讲过了，请查看之前的文章：




## 小结

本节主要演示了多 Agent 的基础编排方式，以及基于 Workflow 的执行机制。
下一节将深入介绍更复杂的编排策略。

