// Copyright (c) Microsoft. All rights reserved.

using _46_Checkpoint_Rollback;
using Microsoft.Agents.AI.Workflows;
using System.Text;


Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

var workflow = WorkflowFactory.BuildWorkflow();

var checkpointManager = CheckpointManager.Default;
var checkpoints = new List<CheckpointInfo>();


await using StreamingRun checkpointedRun = await InProcessExecution.RunStreamingAsync(workflow, NumberSignal.Init, checkpointManager);
await foreach (WorkflowEvent evt in checkpointedRun.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorCompletedEvent executorCompletedEvt:
            Console.WriteLine($"* 执行器 {executorCompletedEvt.ExecutorId} 完成。");
            break;

        case SuperStepCompletedEvent superStepCompletedEvt:
            {
                CheckpointInfo? checkpoint = superStepCompletedEvt.CompletionInfo!.Checkpoint;
                if (checkpoint is not null)
                {
                    checkpoints.Add(checkpoint);
                    Console.WriteLine($"** 检查点在步骤 {checkpoints.Count} 创建。");
                }

                break;
            }

        case WorkflowOutputEvent outputEvent:
            Console.WriteLine($"工作流完成，结果为: {outputEvent.Data}");
            break;

        case WorkflowErrorEvent workflowError:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "未知的工作流错误发生。");
            Console.ResetColor();
            break;

        case ExecutorFailedEvent executorFailed:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"执行器 '{executorFailed.ExecutorId}' 失败，原因: {(executorFailed.Data == null ? "未知错误" : $"异常 {executorFailed.Data}")}.");
            Console.ResetColor();
            break;
    }
}

if (checkpoints.Count == 0)
{
    throw new InvalidOperationException("在工作流执行过程中未创建任何检查点。");
}
Console.WriteLine($"创建的检查点数量: {checkpoints.Count}");

const int CheckpointIndex = 5;
Console.WriteLine($"\n\n从第 {CheckpointIndex + 1} 个检查点恢复。");
CheckpointInfo savedCheckpoint = checkpoints[CheckpointIndex];
await checkpointedRun.RestoreCheckpointAsync(savedCheckpoint, CancellationToken.None);
await foreach (WorkflowEvent evt in checkpointedRun.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorCompletedEvent executorCompletedEvt:
            Console.WriteLine($"* 执行器 {executorCompletedEvt.ExecutorId} 完成。");
            break;

        case WorkflowOutputEvent workflowOutputEvt:
            Console.WriteLine($"工作流完成，结果为: {workflowOutputEvt.Data}");
            break;

        case WorkflowErrorEvent workflowError:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "未知的工作流错误发生。");
            Console.ResetColor();
            break;

        case ExecutorFailedEvent executorFailed:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"执行器 '{executorFailed.ExecutorId}' 失败，原因: {(executorFailed.Data == null ? "未知错误" : $"异常 {executorFailed.Data}")}.");
            Console.ResetColor();
            break;
    }
}