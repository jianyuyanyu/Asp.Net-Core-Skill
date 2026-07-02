using Microsoft.Agents.AI.Workflows;
using System;
using System.Collections.Generic;
using System.Text;

namespace _47_Checkpoint_Human_In_Loop;



internal static class WorkflowFactory
{
    /// <summary>
    /// 获取一个工作流，该工作流与人类进行交互，进行数字猜测游戏。输入端口允许外部世界在请求时向工作流提供输入。
    /// </summary>
    internal static Workflow BuildWorkflow()
    {
        // 创建一个执行器
        RequestPort numberRequest = RequestPort.Create<SignalWithNumber, int>("GuessNumber");
        JudgeExecutor judgeExecutor = new(42);
        // 构建工作流，通过将执行器连接在一个循环中
        return new WorkflowBuilder(numberRequest)
            .AddEdge(numberRequest, judgeExecutor)
            .AddEdge(judgeExecutor, numberRequest)
            .WithOutputFrom(judgeExecutor)
            .Build();
    }
}

internal enum NumberSignal
{
    Init,
    Above,
    Below,
}

/// <summary>
/// 用于在猜测和 JudgeExecutor 之间进行通信的信号。
/// </summary>
internal sealed class SignalWithNumber
{
    public NumberSignal Signal { get; }
    public int? Number { get; }

    public SignalWithNumber(NumberSignal signal, int? number = null)
    {
        this.Signal = signal;
        this.Number = number;
    }
}

/// <summary>
/// 用于判断猜测并提供反馈的执行器。
/// </summary>
[SendsMessage(typeof(SignalWithNumber))]
[YieldsOutput(typeof(string))]
internal sealed class JudgeExecutor() : Executor<int>("Judge")
{
    private readonly int _targetNumber;
    private int _tries;
    private const string StateKey = "JudgeExecutorState";

    /// <summary>
    /// 初始化一个新的 <see cref="JudgeExecutor"/> 实例。
    /// </summary>
    /// <param name="targetNumber">要猜测的数字。</param>
    public JudgeExecutor(int targetNumber) : this()
    {
        this._targetNumber = targetNumber;
    }

    public override async ValueTask HandleAsync(int message, IWorkflowContext context, CancellationToken cancellationToken = default)
    {
        this._tries++;
        if (message == this._targetNumber)
        {
            await context.YieldOutputAsync($"{this._targetNumber} 在 {this._tries} 次尝试中被找到!", cancellationToken);
        }
        else if (message < this._targetNumber)
        {
            await context.SendMessageAsync(new SignalWithNumber(NumberSignal.Below, message), cancellationToken: cancellationToken);
        }
        else
        {
            await context.SendMessageAsync(new SignalWithNumber(NumberSignal.Above, message), cancellationToken: cancellationToken);
        }
    }

    /// <summary>
    /// 检查点执行器的当前状态。
    /// 这个必须被重写，以保存恢复执行器所需的任何状态。
    /// </summary>
    protected override ValueTask OnCheckpointingAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        context.QueueStateUpdateAsync(StateKey, this._tries, cancellationToken: cancellationToken);

    /// <summary>
    /// 从检查点恢复执行器的状态。
    /// 这个必须被重写，以恢复在检查点期间保存的任何状态。
    /// </summary>
    protected override async ValueTask OnCheckpointRestoredAsync(IWorkflowContext context, CancellationToken cancellationToken = default) =>
        this._tries = await context.ReadStateAsync<int>(StateKey, cancellationToken: cancellationToken);
}
