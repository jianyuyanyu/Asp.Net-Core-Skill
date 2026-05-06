
今天我们开启Agent Framework Workflow系列，当我们在构建 AI Agent 或多步骤自动化系统时，工作通常不是一步完成的。而是一个任务可能会被拆分成多个独立步骤，每个步骤单独处理的过程。

在 Agent Framework 的 workflow 中，执行器（Executor）是工作流里的基本处理单元。它接收输入，执行一段具体逻辑，然后输出结果给下一个执行器。
> 比如，一个任务可以通过工作流编排进行串联，在执行过程中依次完成输入预处理、模型推理以及结果后处理等阶段。


## 如何创建执行器

定义了两个 executor。

第一个是 `UppercaseExecutor`，负责把输入字符串转换成大写：

```csharp
internal sealed class UppercaseExecutor() : Executor<string, string>("UppercaseExecutor")
{
    public override ValueTask<string> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(message.ToUpperInvariant());
}
```

它继承自：

```csharp
Executor
```

这表示该执行器接收一个 `string` 类型输入，并返回一个 `string` 类型结果。

构造函数中的 `"UppercaseExecutor"` 是执行器 ID，在后续流式事件输出中可以用来识别是哪一个执行器完成了处理。

第二个执行器是 `ReverseTextExecutor`，负责反转字符串：

```csharp
internal sealed class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    public override ValueTask<string> HandleAsync(
        string message,
        IWorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}
```

如果输入是：

```text
HELLO, WORLD!
```

那么它会返回：

```text
!DLROW ,OLLEH
```

## 如何构建工作流

当我们有了执行器之后，就可以使用 `WorkflowBuilder` 将它们连接起来。

示例中的核心代码如下：

```csharp
UppercaseExecutor uppercase = new();
ReverseTextExecutor reverse = new();

WorkflowBuilder builder = new(uppercase);
builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
var workflow = builder.Build();
```

这里主要做了几件事：

> 完成了工作流的构建：首先创建 UppercaseExecutor 和 ReverseTextExecutor 两个执行器实例，分别保存为 uppercase 和 reverse；然后以 uppercase 作为工作流起点，在 uppercase 和 reverse 之间建立执行顺序，使 UppercaseExecutor 的输出作为 ReverseTextExecutor 的输入；最后指定 reverse 的输出作为整个 workflow 的最终结果，并构建出 workflow 实例。

其中：

```csharp
builder.AddEdge(uppercase, reverse)
```

表示上游执行器 `uppercase` 的输出会传递给下游执行器 `reverse`。

而：

```csharp
.WithOutputFrom(reverse)
```

表示 `reverse` 执行器的结果会作为工作流的输出。

## 工作流的流式输出

在Agent Framework Workflow中，工作流不是等待全部执行完之后才返回，而是使用的是流式输出：

我们接下来看一下流式工作流的原理：

```csharp
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, input: "Hello, World!");
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is ExecutorCompletedEvent executorCompleted)
    {
        Console.WriteLine($"{executorCompleted.ExecutorId}: {executorCompleted.Data}");
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

这里的使用了 `RunStreamingAsync`。它不会只返回最终结果，而是返回一个 `StreamingRun` 对象。通过这个对象，可以监听工作流执行过程中的事件流。

接下来使用：

```csharp
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
```

逐个读取事件。这里是最最核心的地方。

### 监听执行器完成事件

当某个执行器完成处理后，会产生 `ExecutorCompletedEvent`。

示例代码中这样处理：

```csharp
if (evt is ExecutorCompletedEvent executorCompleted)
{
    Console.WriteLine($"{executorCompleted.ExecutorId}: {executorCompleted.Data}");
}
```

这意味着每当一个 executor 完成时，程序都会打印：

- 执行器 ID
- 当前执行器输出的数据

运行后，可以看到如下输出：






我们不需要等到整个工作流全部结束，才能知道中间发生了什么。每一步完成后，都可以立即拿到对应事件。

### 处理工作流错误

除了正常完成事件，示例中还处理了两类错误事件。

第一类是 `WorkflowErrorEvent`：

```csharp
else if (evt is WorkflowErrorEvent workflowError)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(
        workflowError.Exception?.ToString()
        ?? "Unknown workflow error occurred.");
    Console.ResetColor();
}
```

这类事件表示整个工作流层面发生了错误。

第二类是 `ExecutorFailedEvent`：

```csharp
else if (evt is ExecutorFailedEvent executorFailed)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(
        $"Executor '{executorFailed.ExecutorId}' failed with " +
        $"{(executorFailed.Data == null ? "unknown error" : $"exception {executorFailed.Data}")}.");
    Console.ResetColor();
}
```

这类事件表示某个具体 executor 执行失败。

相比 `WorkflowErrorEvent`，`ExecutorFailedEvent` 更具体，因为它包含了失败的 executor ID。对于复杂工作流来说，这对于排查问题非常有帮助。



## 小结


这一节我们介绍了Agent Framework Workflow的一些基础感念：
- 如何定义执行器（Executor）
- 如何使用 WorkflowBuilder 构建工作流
- 如何使用 RunStreamingAsync 启动流式工作流
- 如何使用 WatchStreamAsync 监听工作流事件
- 如何区分处理执行器完成事件、执行器失败事件和工作流错误事件


