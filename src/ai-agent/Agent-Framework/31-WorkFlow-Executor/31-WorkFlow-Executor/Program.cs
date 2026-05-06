// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.Workflows;

/// <summary>
/// 此示例介绍了工作流中的流式输出。
///
/// 与 01_Executors_And_Edges 需要等待整个工作流完成后才显示结果不同，
/// 此示例会在每个执行器完成处理时，实时将事件流式返回给你。
/// 这对于监控长时间运行的工作流或向用户提供实时反馈非常有用。
///
/// 工作流逻辑保持不变：先将文本转换为大写，然后再反转。不同之处在于
/// 我们观察执行过程的方式——可以在中间结果产生时立即看到它们。
/// </summary>
public static class Program
{
    private static async Task Main()
    {
        // 创建执行器
        UppercaseExecutor uppercase = new();
        ReverseTextExecutor reverse = new();

        // 通过顺序连接执行器来构建工作流
        WorkflowBuilder builder = new(uppercase);
        builder.AddEdge(uppercase, reverse).WithOutputFrom(reverse);
        var workflow = builder.Build();

        // 以流式模式执行工作流
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
    }
}

/// <summary>
/// 第一个执行器：将输入文本转换为大写。
/// </summary>
internal sealed class UppercaseExecutor() : Executor<string, string>("UppercaseExecutor")
{
    /// <summary>
    /// 通过将输入消息转换为大写来进行处理。
    /// </summary>
    /// <param name="message">要转换的输入文本</param>
    /// <param name="context">用于访问工作流服务和添加事件的工作流上下文</param>
    /// <param name="cancellationToken">用于监视取消请求的 <see cref="CancellationToken"/>。
    /// 默认值为 <see cref="CancellationToken.None"/>。</param>
    /// <returns>转换为大写后的输入文本</returns>
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(message.ToUpperInvariant()); // 返回值将作为消息沿着边传递给后续执行器
}

/// <summary>
/// 第二个执行器：反转输入文本并完成工作流。
/// </summary>
internal sealed class ReverseTextExecutor() : Executor<string, string>("ReverseTextExecutor")
{
    /// <summary>
    /// 通过反转文本来处理输入消息。
    /// </summary>
    /// <param name="message">要反转的输入文本</param>
    /// <param name="context">用于访问工作流服务和添加事件的工作流上下文</param>
    /// <param name="cancellationToken">用于监视取消请求的 <see cref="CancellationToken"/>。
    /// 默认值为 <see cref="CancellationToken.None"/>。</param>
    /// <returns>反转后的输入文本</returns>
    public override ValueTask<string> HandleAsync(string message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        // 因为我们没有抑制它，所以返回结果将作为此执行器的输出被产生。
        return ValueTask.FromResult(string.Concat(message.Reverse()));
    }
}