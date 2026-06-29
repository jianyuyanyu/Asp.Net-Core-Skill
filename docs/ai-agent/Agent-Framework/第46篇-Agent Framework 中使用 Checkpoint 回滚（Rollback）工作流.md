# 第46篇：Agent Framework 中使用 Checkpoint 回滚（Rollback）工作流

在上一篇文章中，我们介绍了如何使用 Checkpoint 构建支持故障恢复的工作流。

当 Workflow 因程序退出、服务器重启或者其他异常原因中断时，可以通过 `ResumeStreamingAsync` 创建新的 Workflow 实例，并从之前保存的 Checkpoint 继续执行。

不过，Checkpoint 的作用并不仅仅局限于故障恢复。

在很多业务场景下，Workflow 本身仍然处于运行状态，我们并不希望重新创建 Workflow，而是希望它能够回到之前某一个执行阶段，然后重新开始执行。

这些场景都属于 Workflow 回滚（Rollback），而不是 Workflow 恢复（Recovery）。

Agent Framework 提供的 `RestoreCheckpointAsync`，正是用于解决这一类问题。

## 示例场景

本示例继续使用上一篇文章中的猜数字 Workflow。

Workflow 仍然由 `GuessNumberExecutor` 和 `JudgeExecutor` 两个 Executor 组成，通过 Loop 不断完成数字猜测，直到找到正确答案。

与上一篇不同的是，本示例并不会重新创建新的 Workflow，而是在 Workflow 已经运行完成之后，将当前 Workflow 回滚到之前保存的某一个 Checkpoint，并继续执行剩余流程。

假设 Workflow 已经完成了全部执行，并且在执行过程中保存了多个 Checkpoint。

此时，我们选择其中的第六个 Checkpoint，将 Workflow 恢复到该位置，然后继续执行后续流程。

整个过程中，Workflow 并没有重新开始，而是回到了之前保存的执行现场。

## 核心代码实现

整个 Workflow 的定义与上一篇完全相同，因此这里不再重复介绍。

真正发生变化的是 Workflow 恢复方式。

首先，从之前保存的 Checkpoint 集合中取出一个 Checkpoint：

```csharp
CheckpointInfo savedCheckpoint = checkpoints[CheckpointIndex];
```

随后，不再调用 `ResumeStreamingAsync`，而是调用：

```csharp
await checkpointedRun.RestoreCheckpointAsync(
        savedCheckpoint,
        CancellationToken.None);
```

这里有一个非常重要的区别：

- 上一篇文章中的 `ResumeStreamingAsync` 会重新创建一个新的 Workflow 实例，然后根据 Checkpoint 恢复整个 Workflow。
- 这里的 `RestoreCheckpointAsync` 并不会创建新的 Workflow。

它直接作用于当前已经存在的 `StreamingRun`，将当前 Workflow 的运行状态恢复到指定 Checkpoint，然后继续执行。

因此，这里的 Workflow 一直都是同一个实例，只是它的执行状态发生了变化。

## RestoreCheckpointAsync 是如何工作的

很多人第一次看到 `RestoreCheckpointAsync` 时，很容易把它理解成另一种 Resume。

实际上，两者解决的是完全不同的问题。

`RestoreCheckpointAsync` 并不是重新创建 Workflow，而是恢复当前 Workflow 的执行状态。

恢复过程中，Framework 会根据 Checkpoint 中保存的信息，将整个 Workflow 恢复到当时保存的运行现场。

恢复完成之后，Workflow 会继续执行后续流程，就像中间从未发生过任何变化一样。

由于恢复的是整个运行现场，因此恢复的内容不仅包括当前执行位置，同时还包括：

- 所有 Executor 保存的状态
- Workflow State
- 待处理消息
- 其他运行时信息

对于开发者来说，只需要调用一个 API，就能够完成整个恢复过程，而无需自己重新构造 Workflow 的运行环境。

## 为什么不需要重新创建 Workflow

上一篇文章中，我们使用的是：

```csharp
InProcessExecution.ResumeStreamingAsync(...)
```

之所以需要重新创建 Workflow，是因为原来的 Workflow 已经结束或者运行实例已经不存在。

Framework 只能重新创建新的 Workflow，然后根据 Checkpoint 恢复整个执行过程。

而本示例中，Workflow 仍然存在。

因此，不需要重新创建 Workflow，也不需要再次调用 `RunStreamingAsync`。

Framework 只需要将当前 Workflow 回滚到指定 Checkpoint，然后继续运行即可。

这也是 `RestoreCheckpointAsync` 与 `ResumeStreamingAsync` 最大的区别：

- 前者恢复的是当前 Workflow 的执行状态。
- 后者恢复的是一个新的 Workflow 实例。

## RestoreCheckpointAsync 与 ResumeStreamingAsync 的区别

虽然两个 API 都依赖 Checkpoint，但它们解决的问题完全不同。

- `ResumeStreamingAsync` 更关注 Workflow 的故障恢复。  
    当程序退出、服务器重启或者进程崩溃之后，可以重新创建 Workflow，并从之前保存的位置继续执行。
- `RestoreCheckpointAsync` 更关注 Workflow 的回滚能力。  
    Workflow 本身并没有结束，只是恢复到某一个历史状态，然后重新开始执行。

可以理解为，一个负责 Recovery，一个负责 Rollback。

两者共同构成了 Agent Framework 中 Checkpoint 的两种核心使用方式。

## 小结

上一篇文章介绍了 Checkpoint 如何帮助 Workflow 在程序中断之后继续执行。

而本示例进一步展示了 Checkpoint 的另一项能力——Workflow 回滚。

通过 `RestoreCheckpointAsync`，Framework 可以将当前 Workflow 恢复到指定 Checkpoint，并继续执行后续流程，而无需重新创建 Workflow。

从架构角度来看：

- `ResumeStreamingAsync` 解决的是 Workflow 的可靠性问题，让 Workflow 能够在故障之后继续运行。
- `RestoreCheckpointAsync` 则提供了 Workflow 的回滚能力，使 Workflow 可以回到历史状态重新执行。

对于需要人工干预、AI 重试、Prompt 调试以及复杂业务流程回放等场景来说，Workflow 回滚是一项非常重要的能力，也进一步体现了 Checkpoint 在 Agent Framework 中不仅承担状态持久化的职责，同时也是 Workflow 生命周期管理的重要组成部分。
