param(
    [Parameter(Mandatory=$true)]
    [string]$PptxPath
)

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Set-ShapeParagraphs {
    param(
        [xml]$Doc,
        [System.Xml.XmlNamespaceManager]$Ns,
        [int]$ShapeIndex,
        [string[]]$Lines,
        [bool]$UseNumberedPrefix = $false
    )

    $shapes = @()
    foreach ($sp in $Doc.SelectNodes('//p:sp', $Ns)) {
        $texts = $sp.SelectNodes('.//a:t', $Ns)
        if ($texts.Count -gt 0) {
            $shapes += $sp
        }
    }

    $shape = $shapes[$ShapeIndex - 1]
    $body = $shape.SelectSingleNode('.//p:txBody', $Ns)
    $paragraphs = @($body.SelectNodes('a:p', $Ns))
    if ($paragraphs.Count -eq 0) { return }

    $template = $paragraphs[0]
    foreach ($p in $paragraphs) {
        [void]$body.RemoveChild($p)
    }

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $p = $template.CloneNode($true)
        $tNodes = @($p.SelectNodes('.//a:t', $Ns))
        if ($tNodes.Count -eq 0) { continue }

        $value = $Lines[$i]
        $tNodes[0].InnerText = $value
        for ($j = 1; $j -lt $tNodes.Count; $j++) {
            $tNodes[$j].InnerText = ''
        }

        [void]$body.AppendChild($p)
    }
}

function Update-Slide {
    param(
        [System.IO.Compression.ZipArchive]$Zip,
        [int]$SlideNumber,
        [scriptblock]$Updater
    )

    $entryName = "ppt/slides/slide$SlideNumber.xml"
    $entry = $Zip.GetEntry($entryName)
    if ($null -eq $entry) {
        throw "Missing $entryName"
    }

    $reader = [System.IO.StreamReader]::new($entry.Open())
    $xmlText = $reader.ReadToEnd()
    $reader.Close()

    [xml]$doc = $xmlText
    $ns = [System.Xml.XmlNamespaceManager]::new($doc.NameTable)
    $ns.AddNamespace('p', 'http://schemas.openxmlformats.org/presentationml/2006/main')
    $ns.AddNamespace('a', 'http://schemas.openxmlformats.org/drawingml/2006/main')

    & $Updater $doc $ns

    $entry.Delete()
    $newEntry = $Zip.CreateEntry($entryName)
    $writer = [System.IO.StreamWriter]::new($newEntry.Open(), [System.Text.UTF8Encoding]::new($false))
    $doc.Save($writer)
    $writer.Close()
}

$zip = [System.IO.Compression.ZipFile]::Open($PptxPath, [System.IO.Compression.ZipArchiveMode]::Update)
try {
    Update-Slide $zip 3 {
        param($doc, $ns)
        Set-ShapeParagraphs $doc $ns 1 @('议程')
        Set-ShapeParagraphs $doc $ns 2 @(
            '01 Vibe Coding：理念与实践路径',
            '02 Copilot CLI：终端里的 Agent 入口',
            '03 BYOK 与多模型启动方案',
            '04 小黄鸭与 Agent 协作',
            '05 实战：从需求到代码',
            '06 最佳实践与团队规范'
        )
    }

    Update-Slide $zip 4 {
        param($doc, $ns)
        Set-ShapeParagraphs $doc $ns 1 @('Vibe Coding：理念与实践路径')
        Set-ShapeParagraphs $doc $ns 2 @(
            'Vibe Coding 不是“让 AI 替我写代码”，',
            '而是用自然语言表达目标、组织上下文、审查结果。',
            '开发者负责方向、约束、验证与最终判断；',
            'AI Agent 负责读取、生成、修改与执行。',
            'Copilot CLI 是这种协作方式在终端里的实践入口。'
        )
    }
}
finally {
    $zip.Dispose()
}
