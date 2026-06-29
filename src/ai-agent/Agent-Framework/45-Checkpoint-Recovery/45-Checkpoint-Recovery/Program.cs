using _45_Checkpoint;
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
            Console.WriteLine($"* 执行 {executorCompletedEvt.ExecutorId} 完成.");
            break;

        case SuperStepCompletedEvent superStepCompletedEvt:
            {             
                //检查点在每个超级步骤结束时自动创建，当提供检查点管理器时。您可以存储检查点信息以备后用。
                CheckpointInfo? checkpoint = superStepCompletedEvt.CompletionInfo!.Checkpoint;
                if (checkpoint is not null)
                {
                    checkpoints.Add(checkpoint);
                    Console.WriteLine($"** 检查点在步骤 {checkpoints.Count} 创建.");
                }
                break;
            }

        case WorkflowOutputEvent outputEvent:
            Console.WriteLine($"工作流完成，结果: {outputEvent.Data}");
            break;

        case WorkflowErrorEvent workflowError:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "未知的工作流错误发生.");
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

// 从保存的检查点重新实例化一个新的工作流实例并继续执行
var newWorkflow = WorkflowFactory.BuildWorkflow();
const int CheckpointIndex = 5;
Console.WriteLine($"\n\n从第 {CheckpointIndex + 1} 个检查点恢复新的工作流实例。");
CheckpointInfo savedCheckpoint = checkpoints[CheckpointIndex];

await using StreamingRun newCheckpointedRun =
    await InProcessExecution.ResumeStreamingAsync(newWorkflow, savedCheckpoint, checkpointManager);

await foreach (WorkflowEvent evt in newCheckpointedRun.WatchStreamAsync())
{
    switch (evt)
    {
        case ExecutorCompletedEvent executorCompletedEvt:
            Console.WriteLine($"* 执行 {executorCompletedEvt.ExecutorId} 完成.");
            break;

        case WorkflowOutputEvent workflowOutputEvt:
            Console.WriteLine($"工作流完成，结果: {workflowOutputEvt.Data}");
            break;

        case WorkflowErrorEvent workflowError:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(workflowError.Exception?.ToString() ?? "未知的工作流错误发生.");
            Console.ResetColor();
            break;

        case ExecutorFailedEvent executorFailed:
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"执行器 '{executorFailed.ExecutorId}' 失败，原因: {(executorFailed.Data == null ? "未知错误" : $"异常 {executorFailed.Data}")}.");
            Console.ResetColor();
            break;
    }
}
