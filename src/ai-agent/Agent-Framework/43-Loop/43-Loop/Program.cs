// Copyright (c) Microsoft. All rights reserved.

using Microsoft.Agents.AI.Workflows;
using System.Text;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

GuessNumberExecutor guessNumberExecutor = new("GuessNumber", 1, 100);
JudgeExecutor judgeExecutor = new("Judge", 42);

// Build the workflow by connecting executors in a loop
var workflow = new WorkflowBuilder(guessNumberExecutor)
    .AddEdge(guessNumberExecutor, judgeExecutor)
    .AddEdge(judgeExecutor, guessNumberExecutor)
    .WithOutputFrom(judgeExecutor)
    .Build();

// Execute the workflow
await using StreamingRun run = await InProcessExecution.RunStreamingAsync(workflow, NumberSignal.Init);
await foreach (WorkflowEvent evt in run.WatchStreamAsync())
{
    if (evt is WorkflowOutputEvent outputEvent)
    {
        Console.WriteLine($"Result: {outputEvent}");
    }
    else if (evt is WorkflowErrorEvent workflowError)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "未知的工作流错误发生。");
        Console.ResetColor();
    }
    else if (evt is ExecutorFailedEvent executorFailed)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"执行器 '{executorFailed.ExecutorId}' 失败，原因：{(executorFailed.Data == null ? "未知错误" : $"异常 {executorFailed.Data}")}。");
        Console.ResetColor();
    }
}

/// <summary>
/// 信号用于 GuessNumberExecutor 和 JudgeExecutor 之间的通信。
/// </summary>
internal enum NumberSignal
{
    Init,
    Above,
    Below,
}

/// <summary>
/// 当前执行器根据当前的上下界进行猜测。
/// </summary>
[SendsMessage(typeof(int))]
class GuessNumberExecutor : Executor<NumberSignal>
{
    /// <summary>
    /// 猜测范围的下界。
    /// </summary>
    public int LowerBound { get; private set; }

    /// <summary>
    /// 猜测范围的上界。
    /// </summary>
    public int UpperBound { get; private set; }

    /// <summary>
    /// 初始化一个新的 <see cref="GuessNumberExecutor"/> 类的实例。
    /// </summary>
    /// <param name="id">执行器的唯一标识符。</param>
    /// <param name="lowerBound">猜测范围的初始下界。</param>
    /// <param name="upperBound">猜测范围的初始上界。</param>
    public GuessNumberExecutor(string id, int lowerBound, int upperBound) : base(id)
    {
        this.LowerBound = lowerBound;
        this.UpperBound = upperBound;
    }

    private int NextGuess => (this.LowerBound + this.UpperBound) / 2;

    public override async ValueTask HandleAsync(NumberSignal message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        switch (message)
        {
            case NumberSignal.Init:
                await context.SendMessageAsync(this.NextGuess, cancellationToken: cancellationToken);
                break;
            case NumberSignal.Above:
                this.UpperBound = this.NextGuess - 1;
                await context.SendMessageAsync(this.NextGuess, cancellationToken: cancellationToken);
                break;
            case NumberSignal.Below:
                this.LowerBound = this.NextGuess + 1;
                await context.SendMessageAsync(this.NextGuess, cancellationToken: cancellationToken);
                break;
        }
    }
}
/// <summary>
/// 当前执行器根据猜测的数字提供反馈。   
/// </summary>
[SendsMessage(typeof(NumberSignal))]
[YieldsOutput(typeof(string))]
public class JudgeExecutor : Executor<int>
{
    private readonly int _targetNumber;
    private int _tries;

    /// <summary>
    /// 初始化一个新的 <see cref="JudgeExecutor"/> 类的实例。
    /// </summary>
    /// <param name="id">执行器的唯一标识符。</param>
    /// <param name="targetNumber">要猜测的数字。</param>
    public JudgeExecutor(string id, int targetNumber) : base(id)
    {
        this._targetNumber = targetNumber;
    }

    public override async ValueTask HandleAsync(int message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        this._tries++;
        if (message == this._targetNumber)
        {
            await context.YieldOutputAsync($"{this._targetNumber} 被发现，经过 {this._tries} 次尝试!", cancellationToken);
        }
        else if (message < this._targetNumber)
        {
            await context.SendMessageAsync(NumberSignal.Below, cancellationToken: cancellationToken);
        }
        else
        {
            await context.SendMessageAsync(NumberSignal.Above, cancellationToken: cancellationToken);
        }
    }
}