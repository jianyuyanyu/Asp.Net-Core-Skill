
当一个 `Workflow` 包含多个 `Executor`，并且它们之间存在复杂的执行关系时，仅通过阅读代码往往很难快速理解整个工作流的结构。

此时，工作流可视化（Visualization）就能够帮助开发者更加直观地查看 `Workflow` 的拓扑结构，并验证整个 `Workflow` 是否符合预期设计。

`Agent Framework` 提供了两个 `Workflow` 的扩展方法，用于生成工作流可视化描述：

- `ToMermaidString()`：生成 Mermaid 图表格式。
- `ToDotString()`：生成 Graphviz DOT 图表格式。

我们使用之前的这篇文章的例子：

## 示例

我们构建完工作流之后不需要运行工作流，只需要调用 ToMermaidString() 或 ToDotString() 方法即可生成对应的可视化描述。

```csharp
var workflow = builder.Build();
// Mermaid
Console.WriteLine("Mermaid 字符串: \n=======");
var mermaid = workflow.ToMermaidString();
Console.WriteLine(mermaid);
Console.WriteLine("=======");

// DOT
Console.WriteLine("DiGraph 字符串: *** 提示： 导出DOT作为图像，请安装Graphviz并将DOT输出管道到 'dot -Tsvg', 'dot -Tpng' 等命令。 *** \n=======");
var dotString = workflow.ToDotString();
Console.WriteLine(dotString);
Console.WriteLine("=======");
```

## 运行结果

我们看到输出结果分别生成了 Mermaid 图表描述语言（Mermaid Diagram Syntax）和 Graphviz DOT 图表描述语言（Graphviz DOT Syntax）：





我们找个个在线工具来渲染这些图表描述语言，便可以直观地看到整个工作流的拓扑结构。

### Mermaid



### DOT






