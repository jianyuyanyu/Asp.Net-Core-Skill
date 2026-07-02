# Agent Framework 中使用 Checkpoint 构建可恢复的 Human-in-the-Loop 工作流


前面的文章中我们介绍了 Human-in-the-Loop 和 Checkpoint 的基本概念，以及如何在 Agent Framework 中使用 Checkpoint 实现 Workflow 的故障恢复以及回滚。

Loop保证工作流内部循环，Human-in-the-Loop(HITL) 确保工作能和用户交互，Checkpoint 用于保存 Workflow 的运行状态。

对于 Human-in-the-Loop 工作流来说，Checkpoint 并不是必备可选的能力，但是它确实能够使你的工作流
在遇到灾难时仍然能够保证 Workflow 的持续运行。


## 示例场景


本示例继续使用猜数字游戏。

Workflow 首先通过 RequestPort 对外请求用户输入的一个数字。

RequestPort 会将用户输入的数字传导给 JudgeExecutor。

如果数字过大或者过小，JudgeExecutor 会把结果返回给 RequestPort，RequestPort 则再次请求用户输入新的数字。

整个流程会不断重复这一过程，直到猜中目标数字。


这个例子和之前的Human-in-the-Loop 是完全一样的，只不过我们在例子中加入了 Checkpoint。

与上一篇 Human-in-the-Loop 示例不同的是，本示例在整个 Workflow 运行过程中启用了 Checkpoint。

这样不仅能够在工作流发生故障时恢复执行，还能够根据业务需要回滚到之前保存的检查点，重新执行后续流程。


> （这里放流程图）

## 核心代码

这个例子的代码其实和之前的 Human-in-the-Loop 示例完全一样，唯一
不同的是，启动 Workflow 时增加了 CheckpointManager：

```csharp
var checkpointManager = CheckpointManager.Default;

await using StreamingRun checkpointedRun =
    await InProcessExecution.RunStreamingAsync(
        workflow,
        new SignalWithNumber(NumberSignal.Init),
        checkpointManager);
```

启用之后，Framework 会在每个 Super Step 完成后自动创建 Checkpoint。

开发者无需主动保存 Workflow。

## 从第2个 Checkpoint 恢复

示例最后使用：

```csharp
const int CheckpointIndex = 1;
Console.WriteLine($"\n\n从第 {CheckpointIndex + 1} 个检查点恢复.");
CheckpointInfo savedCheckpoint = checkpoints[CheckpointIndex];
// 注意，我们将状态直接恢复到同一个运行实例。
await checkpointedRun.RestoreCheckpointAsync(savedCheckpoint, CancellationToken.None);
```

我们从第 2 个 Checkpoint 恢复 Workflow 的运行状态。恢复之后我们可以看到我们第一次输入的数字的值被保留了下来，Workflow 继续运行。



## 小结

在这一节中我们更好的把我们前面学到的 Human-in-the-Loop 和 Checkpoint 的知识结合起来。
从而来保证工作流的可恢复性，这也是企业级工作流中最常见的一种技术手段。

