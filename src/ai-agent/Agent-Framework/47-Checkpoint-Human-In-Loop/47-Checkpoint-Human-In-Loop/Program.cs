using _47_Checkpoint_Human_In_Loop;
using Microsoft.Agents.AI.Workflows;
using System.Text;


Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

//创建工作流
var workflow = WorkflowFactory.BuildWorkflow();

// 创建检查点管理器
var checkpointManager = CheckpointManager.Default;
var checkpoints = new List<CheckpointInfo>();

// 执行工作流并保存检查点
await using StreamingRun checkpointedRun = await InProcessExecution.RunStreamingAsync(workflow, new SignalWithNumber(NumberSignal.Init), checkpointManager)
    ;
await foreach (WorkflowEvent evt in checkpointedRun.WatchStreamAsync())
{
    switch (evt)
    {
        case RequestInfoEvent requestInputEvt:
            // 处理工作流中的 `RequestInfoEvent`   
            ExternalResponse response = HandleExternalRequest(requestInputEvt.Request);
            await checkpointedRun.SendResponseAsync(response);
            break;
        case ExecutorCompletedEvent executorCompletedEvt:
            Console.WriteLine($"* 执行器 {executorCompletedEvt.ExecutorId} 完成.");
            break;
        case SuperStepCompletedEvent superStepCompletedEvt:
            // 检查点会在每个超级步骤结束时自动创建，当提供了检查点管理器时。
            // 您可以存储检查点信息以备后用。
            CheckpointInfo? checkpoint = superStepCompletedEvt.CompletionInfo!.Checkpoint;
            if (checkpoint is not null)
            {
                checkpoints.Add(checkpoint);
                Console.WriteLine($"** 检查点在步骤 {checkpoints.Count} 创建.");
            }
            break;
        case WorkflowOutputEvent workflowOutputEvt:
            Console.WriteLine($"工作流完成，结果为: {workflowOutputEvt.Data}");
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
    throw new InvalidOperationException("在工作流执行期间未创建任何检查点.");
}
Console.WriteLine($"创建的检查点数量: {checkpoints.Count}");

// 从检查点恢复并继续执行
const int CheckpointIndex = 1;
Console.WriteLine($"\n\n从第 {CheckpointIndex + 1} 个检查点恢复.");
CheckpointInfo savedCheckpoint = checkpoints[CheckpointIndex];
// 注意，我们将状态直接恢复到同一个运行实例。
await checkpointedRun.RestoreCheckpointAsync(savedCheckpoint, CancellationToken.None);
await foreach (WorkflowEvent evt in checkpointedRun.WatchStreamAsync())
{
    switch (evt)
    {
        case RequestInfoEvent requestInputEvt:
            ExternalResponse response = HandleExternalRequest(requestInputEvt.Request);
            await checkpointedRun.SendResponseAsync(response);
            break;
        case ExecutorCompletedEvent executorCompletedEvt:
            Console.WriteLine($"* 执行器 {executorCompletedEvt.ExecutorId} 完成.");
            break;
        case WorkflowOutputEvent workflowOutputEvt:
            Console.WriteLine($"工作流完成，结果为: {workflowOutputEvt.Data}");
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

static ExternalResponse HandleExternalRequest(ExternalRequest request)
{
    if (request.TryGetDataAs<SignalWithNumber>(out var signal))
    {
        switch (signal.Signal)
        {
            case NumberSignal.Init:
                int initialGuess = ReadIntegerFromConsole("请输入您的初始猜测: ");
                return request.CreateResponse(initialGuess);
            case NumberSignal.Above:
                int lowerGuess = ReadIntegerFromConsole($"您之前猜测的数字 {signal.Number} 太大。请输入一个新的猜测: ");
                return request.CreateResponse(lowerGuess);
            case NumberSignal.Below:
                int higherGuess = ReadIntegerFromConsole($"您之前猜测的数字 {signal.Number} 太小。请输入一个新的猜测: ");
                return request.CreateResponse(higherGuess);
        }
    }

    throw new NotSupportedException($"执行 {request.PortInfo.RequestType} 不被支持");
}
static int ReadIntegerFromConsole(string prompt)
{
    while (true)
    {
        Console.Write(prompt);
        string? input = Console.ReadLine();
        if (int.TryParse(input, out int value))
        {
            return value;
        }
        Console.WriteLine("输入无效。请输入一个有效的整数。");
    }
}