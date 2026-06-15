# Agent Framework 中使用 Switch Case 构建多分支智能工作流

在上一篇文章中，我们介绍了 Agent Framework Workflow 中的条件边（Condition Edge）。

其实在之前的一篇文章里面我们已经用到过 Switch Case ··

Condition Edge 和 AddSwitch 都能实现流程分支。

当分支较少时，Condition Edge 更简洁；当一个节点存在多个状态、多条处理路径时，AddSwitch 能够将所有路由逻辑集中管理，从而获得更好的可读性和可维护性。

## 示例场景

我们继续沿用上一节自动邮件处理的案例。

当系统收到一封邮件后，会先通过垃圾邮件检测节点进行分析。

检测结果可能有三种：

```csharp
public enum SpamDecision
{
    NotSpam,
    Spam,
    Uncertain
}
```

分别表示：

* `NotSpam`：正常邮件；
* `Spam`：垃圾邮件；
* `Uncertain`：当前无法确定。

不同状态会进入不同处理流程：

* `NotSpam`：生成邮件回复并发送；
* `Spam`：标记为垃圾邮件；
* `Uncertain`：进入人工复核或后续分析流程。

对应的工作流路径如下：

> 【流程图】

虽然本示例只有三个状态，但在真实业务系统中，一个节点往往会产生更多业务状态。例如订单审批可能存在 Approved、Rejected、Pending、ManualReview 等多种结果。随着状态数量增加，使用 AddSwitch 集中管理路由逻辑会比大量条件边更加清晰。

为了实现上述流程，本示例定义了以下几个执行器：

* SpamDetectionExecutor：负责识别邮件类型；
* EmailAssistantExecutor：负责生成邮件回复；
* SendEmailExecutor：负责发送邮件；
* HandleSpamExecutor：负责处理垃圾邮件；
* HandleUncertainExecutor：负责处理无法确定的邮件。

## 核心代码实现

### 1. 配置模型

这是与模型交互的基础配置，和之前一样：

```csharp
var endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT") ?? throw new InvalidOperationException("AZURE_OPENAI_ENDPOINT is not set.");
var deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT_NAME") ?? "gpt-5.4-mini";
```

### 2. 定义 Agent 和 Executor

我把执行器分为两类：Agent 驱动执行器和业务驱动执行器。

Agent驱动执行器：`SpamDetectionAgent` 和 `EmailAssistantAgent`，Agent执行器负责调用模型，大模型进行决策。

业务驱动执行器：`SendEmailExecutor`、`HandleSpamExecutor` 和 `HandleUncertainExecutor`，这类执行器是我们传统意义上的执行器，没有和模型进行交互，而是根据已经确定的业务状态执行对应动作的节点。

具体代码请参考例子：

```csharp
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
```

### 3. 对节点进行编排

这个示例中最核心的部分是 WorkflowBuilder：

```csharp
WorkflowBuilder builder = new(spamDetectionExecutor);
builder.AddSwitch(spamDetectionExecutor, switchBuilder =>
    switchBuilder
        .AddCase(
            GetCondition(expectedDecision: SpamDecision.NotSpam),
            emailAssistantExecutor
        )
        .AddCase(
            GetCondition(expectedDecision: SpamDecision.Spam),
            handleSpamExecutor
        )
        .WithDefault(
            handleUncertainExecutor
        )
)
.AddEdge(emailAssistantExecutor, sendEmailExecutor)
.WithOutputFrom(handleSpamExecutor, sendEmailExecutor, handleUncertainExecutor);
var workflow = builder.Build();

```

我们通过 WorkflowBuilder 对整个工作流进行编排。

首先，指定工作流的入口节点：

```csharp
WorkflowBuilder builder = new(spamDetectionExecutor);
```

这里的 SpamDetectionExecutor 是整个工作流的起点。它负责接收用户邮件，调用垃圾邮件检测 Agent，并返回结构化结果 DetectionResult。

接下来，基于入口节点的输出结果进行分支路由：

```csharp
builder.AddSwitch(spamDetectionExecutor, switchBuilder => ...)
```

这里表示，当 SpamDetectionExecutor 执行完成后，工作流不会固定进入某一个下游节点，而是根据其返回的 DetectionResult 执行 Switch 路由，并选择对应的处理分支。

随后，通过 AddCase 定义具体的路由规则：

```csharp
.AddCase(
    GetCondition(expectedDecision: SpamDecision.NotSpam),
    emailAssistantExecutor
)
.AddCase(
    GetCondition(expectedDecision: SpamDecision.Spam),
    handleSpamExecutor
)
```

当检测结果为 NotSpam 时，工作流进入 EmailAssistantExecutor，生成邮件回复；

当检测结果为 Spam 时，工作流进入 HandleSpamExecutor，执行垃圾邮件处理逻辑。

最后，通过 WithDefault 定义默认分支：

```csharp
.WithDefault(handleUncertainExecutor)
```

当所有 Case 条件都未命中时，工作流将进入默认分支 HandleUncertainExecutor。

在当前示例中，我们使用默认分支处理 Uncertain 状态。但需要注意的是，Default 并不等同于 Uncertain。从设计角度来看，它表示所有未被显式匹配的状态，因此在实际项目中通常还承担未知状态、异常状态以及新增状态的兜底处理职责。

### 条件函数

每个 Case 背后实际上都对应一个条件函数：

```csharp
private static Func<object?, bool> GetCondition(
    SpamDecision expectedDecision) =>
    detectionResult =>
        detectionResult is DetectionResult result &&
        result.spamDecision == expectedDecision;
```

这个函数接收一个期望的 `SpamDecision`，并返回一个用于条件判断的委托。

例如：

```csharp
GetCondition(expectedDecision: SpamDecision.NotSpam)
```

最终会生成如下判断逻辑：

```csharp
detectionResult is DetectionResult result &&
result.spamDecision == SpamDecision.NotSpam
```

当 `DetectionResult.spamDecision` 的值为 `NotSpam` 时，该条件返回 `true`，对应的 Case 被命中，工作流进入 `EmailAssistantExecutor`。

从这里可以看出，`AddSwitch` 本身并不知道什么是 `NotSpam`、`Spam` 或 `Uncertain`。

它只负责依次执行各个 Case 对应的条件函数，并根据返回结果决定进入哪个分支。

因此，真正决定路由结果的并不是 Switch，而是每个 Case 背后的条件判断逻辑。

这里还有一个非常重要的概念：

Switch 判断的并不是原始输入邮件，而是上游节点的输出结果。

在当前示例中：

```text
用户邮件
    ↓

SpamDetectionExecutor
    ↓

DetectionResult
    ↓

AddSwitch
```

也就是说，`SpamDetectionExecutor` 会先执行，并生成结构化结果 `DetectionResult`；随后 `AddSwitch` 再根据这个结果选择对应的处理路径。

这也是 Agent Framework Workflow 中最常见的路由模式：

```text
上游节点产生结构化结果
        ↓
Workflow 根据结果进行路由
        ↓
下游节点执行对应动作
```

无论是 Condition Edge 还是 AddSwitch，本质上都是围绕上游节点的输出结果进行判断和编排。

### 状态管理

状态管理的目的不仅仅是让执行器之间传递的数据更轻，更重要的是实现**决策结果与业务数据的分离**。

在本示例中，`SpamDetectionExecutor` 在执行时会先生成一个邮件 ID，并将原始邮件内容写入 Workflow State：

```csharp
var newEmail = new Email
{
    EmailId = Guid.NewGuid().ToString("N"),
    EmailContent = message.Text
};

await context.QueueStateUpdateAsync(
    newEmail.EmailId,
    newEmail,
    scopeName: EmailStateConstants.EmailStateScope,
    cancellationToken);
```

随后，它会将 `EmailId` 写回到检测结果中：

```csharp
detectionResult!.EmailId = newEmail.EmailId;
```

这样后续节点之间传递的就不再是完整邮件对象，而是一个轻量级的 `DetectionResult`，其中包含用于关联业务数据的 `EmailId`。

当后续节点需要访问原始邮件内容时，再通过 `EmailId` 从 Workflow State 中读取：

```csharp
var email = await context.ReadStateAsync<Email>(
    message.EmailId,
    scopeName: EmailStateConstants.EmailStateScope,
    cancellationToken);
```

这种设计带来了几个明显的好处。

首先，节点之间传递的数据会更加轻量，避免在多个执行器之间重复传递大对象。

其次，业务数据与流程状态解耦。工作流中流转的是决策结果，而原始业务数据统一保存在 State 中。

最后，这种模式也更容易实现日志追踪、审计以及人工复核等企业级需求。

从设计角度来看，可以简单理解为：

```text
Workflow 负责流转状态

State 负责存储上下文
```

这种模式与传统业务系统中的 Repository、Session 或 Context 设计思想非常接近。

### WithOutputFrom 的作用

最后还需要关注这句代码：

.WithOutputFrom(
    handleSpamExecutor,
    sendEmailExecutor,
    handleUncertainExecutor);
```

很多开发者第一次接触 Workflow 时，容易把它理解为“流程出口”。

实际上，它的作用更接近于：

**Workflow 输出映射**

也就是说，它用于声明哪些节点产生的结果需要作为整个 Workflow 的最终输出。

在当前示例中，三条执行路径都会产生最终结果：

正常邮件路径：SendEmailExecutor 输出“邮件已发送”；
垃圾邮件路径：HandleSpamExecutor 输出“邮件已标记为垃圾邮件”；
不确定邮件路径：HandleUncertainExecutor 输出“不确定原因和邮件内容”。

这些节点内部都会调用：

await context.YieldOutputAsync(...);

用于产生输出事件。

但需要注意的是，调用 YieldOutputAsync 并不意味着该结果一定会成为 Workflow 的最终输出。

只有被：

```csharp
.WithOutputFrom(...)
```

显式声明的节点，其输出才会被 Workflow 对外暴露。

因此，WithOutputFrom 可以理解为 Workflow 的输出映射配置，它负责告诉框架：

* 哪些节点的输出
* 代表整个 Workflow 的最终结果

对于多分支工作流来说，这一点尤为重要。因为不同分支的终点可能不同，而 WithOutputFrom 能够统一定义这些分支的最终输出行为。

## 小结

在上一篇文章中，我们介绍了通过 Condition Edge 实现流程分支。

Condition Edge 和 AddSwitch 都能够根据上游节点的输出结果实现路由，它们的区别并不在于功能，而是在于适用场景：

当分支较少时，Condition Edge 更加直接；当一个节点可能产生多个业务状态时，AddSwitch 能够将所有路由逻辑集中管理，从而获得更好的可读性和可维护性。

在本文的邮件处理示例中：

* SpamDetectionExecutor 负责调用垃圾邮件检测 Agent；
* DetectionResult 使用 SpamDecision 枚举表达结构化决策；
* AddSwitch 根据不同状态进入不同处理流程；
* Workflow State 负责共享邮件上下文；
* WithOutputFrom 统一定义 Workflow 的最终输出。

