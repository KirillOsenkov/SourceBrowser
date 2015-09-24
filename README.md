# SourceBrowser
Source browser website generator that powers http://referencesource.microsoft.com and http://source.roslyn.io.

Create and host your own static HTML website to browse your C#/VB/MSBuild/TypeScript source code.

##Instructions:
 1. git clone https://github.com/KirillOsenkov/SourceBrowser.git
 2. cd SourceBrowser
 3. msbuild
 4. cd bin\Debug\HtmlGenerator
 5. HtmlGenerator.exe ..\\..\\..\\TestCode\TestSolution.sln
 6. the website in bin\Debug\HtmlGenerator\Index is ready to be served

##In Visual Studio 2015:
 1. Open SourceBrowser.sln.
 2. Set HtmlGenerator project as startup and hit F5 - it is preconfigured to generate a website for TestCode\TestSolution.sln
 3. Pass a path to an .sln file or a .csproj file (or multiple paths separated by spaces) to create an index for them
 4. pass /out:<path> to HtmlGenerator.exe to configure where to generate the website to
