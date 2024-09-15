param([switch]$force)

$forceArg = ''
if ($force) {
    $forceArg = '/force'
}

src\HtmlGenerator\bin\Debug\net8.0\HtmlGenerator.exe TestCode\TestSolution.sln /out:src\HtmlGenerator\bin\Debug\net8.0\Web\Index $forceArg