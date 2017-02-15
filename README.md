# SourceBrowser
Source browser website generator that powers http://referencesource.microsoft.com and http://source.roslyn.io.

Create and host your own static HTML website to browse your C#/VB/MSBuild/TypeScript source code. **Note** that it still does require IIS and ASP.NET for hosting (symbol index is kept server-side), so [without IIS and ASP.NET the search function doesn't work](https://github.com/KirillOsenkov/SourceBrowser/wiki/Why-does-generated-Html-still-require-a-server-side-ASP.NET-Web-API%3F).

Of course Source Browser allows you to browse its own source code:
[http://sourcebrowser.azurewebsites.net](http://sourcebrowser.azurewebsites.net)

Now also available on NuGet:
[https://www.nuget.org/packages/Microsoft.SourceBrowser](https://www.nuget.org/packages/Microsoft.SourceBrowser)

##Instructions:
 1. git clone https://github.com/KirillOsenkov/SourceBrowser.git
 2. cd SourceBrowser
 3. msbuild
 4. cd bin\Debug\HtmlGenerator
 5. HtmlGenerator.exe ..\\..\\..\TestCode\TestSolution.sln
 6. the website in bin\Debug\HtmlGenerator\Index is ready to be served

##In Visual Studio 2015:
 1. Open SourceBrowser.sln.
 2. Set HtmlGenerator project as startup and hit F5 - it is preconfigured to generate a website for TestCode\TestSolution.sln (you may need to unload the SourceIndexServer project if you see an error message that IIS can't serve from bin\Debug\HtmlGenerator\Index). 
 3. Pass a path to an .sln file or a .csproj file (or multiple paths separated by spaces) to create an index for them
 4. Pass /out:<path> to HtmlGenerator.exe to configure where to generate the website to. This path will be used in step 6 as your "physicalPath".
 5. Pass /in:<path> to pass a file with a list of full paths to projects and solutions to include in the index
 6. Set SourceIndexServer project as startup and run/debug the website. It is pre-configured to run from \bin\Debug\HtmlGenerator\Index but you can customize in project properties -> Web -> Custom server.

**Note:** Either Visual Studio 2015 or at least MSBuild Tools 2015 [http://www.microsoft.com/en-us/download/details.aspx?id=48159](http://www.microsoft.com/en-us/download/details.aspx?id=48159) are required for Source Browser to work (Source Browser uses Roslyn and Roslyn uses MSBuild 14.0 to read projects and solutions).

##Conceptual design

At indexing time, C# and VB source code is analyzed using Roslyn and a lot of static hyperlinked HTML files are generated into the output directory. There is no database. The website is mostly static HTML where all the links, source code coloring etc. are precalculated at indexing time. All the hyperlinks are hardwired to be simple links bypassing the server. 

The only component that runs on the webserver is a service that given a search query does the lookup and returns a list of matching types and members, which are hyperlinks into the static HTML. The webservice keeps a list of all declared types and members in memory, this list is also precalculated at indexing time. All services, such as Find All References, Project Explorer, etc. are all pre-rendered. 

The generator is not incremental. You have to generate into an empty folder from scratch every time, and then replace the currently deployed folder with the new contents atomically (using e.g. Azure Deployments, robocopy /MIR to inetpub\\wwwroot, etc). For smaller projects, deploying to Azure using Dropbox or Git would work just fine.

###Limitations and known issues
 1. Indexing more than one project with the same assembly name is currently unsupported. Only the first project wins. This is due to a fundamental design decision to only reference an assembly by short name. Customizers should add a means to filter "victim" projects out in their forks to pick the best single project for inclusion in the index.
 2. The generated website can only be hosted in the root of the domain. Making it run from a subdirectory is non-trivial and unlikely to be supported.

###Features
* Solution Explorer - contents of projects merged into single tree on the left
* coloring for C#, VB, MSBuild, XAML and TypeScript
* Go To Definition (click on a reference)
* Find All Reference (click on a definition)
* Project Explorer - in any document click on the Project link at the bottom
* Namespace explorer - for a project view all types and members nested in namespace hierarchy
* Document Outline - for a document click on the button in top right to display types and members in the current file
* http://\<URL>/i.txt for the entire solution and /AssemblyName/i.txt (for an assembly) displays source code stats, lines of code, etc
* http://\<URL>/#EmptyArrayAllocation finds all allocations of empty arrays (this feature is one-off and hardcoded and not extensible)
* Clicking on the partial keyword will display a list of all files where this type is declared
* MSBuild files (.csproj etc) have hyperlinks
* TypeScript files (*.ts) are indexed if they're part of a C# project. Work underway to allow an arbitrary array of TypeScript files.
* Search for GUIDs in C#/VB string literals is supported

##Project status and contributions

This is a reference implementation that showcases the concepts and Roslyn usage. It comes with no guarantees, use at your own risk. We will consider accepting high-quality pull requests that add non-trivial value, however we have no plans to do significant work on the application in its current form. Any significant rearchitecture, adding large features, big refactorings won't be accepted because of resource constraints. Feel free to use it to generate websites for your own code, integrate in your CI servers etc. Feel free to do whatever you want in your own forks. Bug reports are gratefully accepted.

For any questions, feel free to reach out to [@KirillOsenkov](https://twitter.com/KirillOsenkov) on Twitter. Thanks to [@v2_matveev](https://twitter.com/v2_matveev) for contributing TypeScript support!
