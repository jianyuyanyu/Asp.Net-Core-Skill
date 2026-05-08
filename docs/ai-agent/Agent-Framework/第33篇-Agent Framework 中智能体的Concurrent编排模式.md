
上一节我们介绍了 Sequential（顺序编排）这种最基础的 Agent 编排模式。
实际上，在 Agent Framework 中，顺序执行链路既可以通过 AgentWorkflowBuilder.BuildSequential() 快速创建，也可以通过更底层的 WorkflowBuilder 手动定义执行关系。
前者本质上是对后者的一层语法糖封装，用于简化线性链路的构建。

## 创建 AI Agent

在 C# 中，一个 Agent 通常会基于一个聊天模型客户端创建。

```csharp
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
    ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");

var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME")
    ?? "gpt-5.4-mini";

var chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureCliCredential())
    .GetChatClient(deploymentName)
    .AsIChatClient();
```

这里我们不过多介绍如何创建基础Agent，毕竟不是我们关注的重点，关于创建基础Agent的细节，可以参考之前的文章：


## Sequential另外一种实现方式

Sequential 是最基础的一种编排模式。在该模式下，多个 Agent 会按照预定义的顺序依次执行，前一个 Agent 的输出会作为后一个 Agent 的输入。
在 Agent Framework 中，可以通过 `BuildSequential` 方法快速构建顺序执行的 Workflow，其定义如下：

```csharp
public static Workflow BuildSequential(params IEnumerable<AIAgent> agents)
```
该方法接收一个 IEnumerable<AIAgent>，用于指定执行链路中的 Agent 顺序，并返回一个 Workflow 实例。


示例

下面的示例中，多个翻译 Agent 会按照定义顺序依次执行：

```csharp
var translationAgents =
    from lang in new[] { "法语", "中文", "西班牙语", "英语" }
    select GetTranslationAgent(lang, client);

// 构建顺序编排 Workflow
var workflow = AgentWorkflowBuilder.BuildSequential(translationAgents);

// 执行
await RunWorkflowAsync(
    workflow,
    [new(ChatRole.User, "Hello，World！")]
);
```

输出结果：

【图片】

其实Sqeuential（顺序执行）是最简单的编排方式，其实本质和我们上节介绍的是一样的，BuildSequential 本质上只是对顺序编排的一种语法糖，用于简化线性执行链路的构建。


## Concurrent：并发编排

Concurrent 是一种并发执行的编排模式。在该模式下，多个 Agent 会基于同一输入同时执行，各自生成结果。

创建并发 Workflow 的核心方法是 `BuildConcurrent`，其定义如下：


```csharp
public static Workflow BuildConcurrent(IEnumerable<AIAgent> agents, Func<IList<List<ChatMessage>>, List<ChatMessage>>? aggregator = null)
```

其中：

第一个参数 agents 用于指定参与执行的 Agent 集合
第二个参数 aggregator 是一个可选的聚合函数，用于在并发执行完成后对多个 Agent 的输出结果进行统一处理


### 聚合函数（Aggregator）

因为在 Concurrent 模式下，每个 Agent 都会独立生成自己的输出结果。

如果没有统一的聚合逻辑，系统就会得到多份彼此独立的结果，而无法形成最终统一输出。

Aggregator 的作用就是负责对这些结果进行收敛处理。

开发者可以在此基础上进行多种处理，例如：

- 合并（Merge）
- 筛选（Filter）
- 排序（Rank）
- 融合（Fusion）

如果未提供该参数，框架将使用默认策略对结果进行合并。

### 示例：并发翻译

下面的示例中，多个翻译 Agent 会同时处理同一输入，并通过聚合函数将结果合并：

```csharp
var translationAgents =
    from lang in new[] { "法语", "中文", "西班牙语", "英语" }
    select GetTranslationAgent(lang, client);

var workflow = AgentWorkflowBuilder.BuildConcurrent(
    translationAgents,
    results =>
    {
        var merged = new List<ChatMessage>();

        foreach (var agentOutput in results)
        {
            merged.AddRange(agentOutput);
        }

        return merged;
    });

await RunWorkflowAsync(
    workflow,
    [new(ChatRole.User, "Hello，World！")]);
```

输出结果如下：

【图片】


## 总结

本节介绍了 Agent Framework 中两种最基础的 Agent 编排模式：Sequential 与 Concurrent。

Sequential（顺序编排）适用于存在明确上下游依赖的任务链路，多个 Agent 按顺序依次执行，前一个 Agent 的输出会传递给下一个 Agent。
Concurrent（并发编排）适用于多个 Agent 可以独立处理同一输入的场景，各 Agent 并行执行，最终通过 Aggregator 对结果进行统一收敛。