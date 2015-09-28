# SourceBrowser
Source browser website generator that powers http://referencesource.microsoft.com and http://source.roslyn.io.

Create and host your own static HTML website to browse your C#/VB/MSBuild/TypeScript source code.

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
 2. Set HtmlGenerator project as startup and hit F5 - it is preconfigured to generate a website for TestCode\TestSolution.sln
 3. Pass a path to an .sln file or a .csproj file (or multiple paths separated by spaces) to create an index for them
 4. Pass /out:<path> to HtmlGenerator.exe to configure where to generate the website to. This path will be used in step 5 as your "physicalPath".
 5. Edit .vs\config\applicationhost.config line 166 so that physicalPath points to \<virtualDirectory path="/" physicalPath="{Path from step 4}" />. Then you can set SourceIndexServer project as startup and run/debug the website.

##Conceptual design

At indexing time, C# and VB source code is analyzed using Roslyn and a lot of static hyperlinked HTML files are generated into the output directory. There is no database. The website is mostly static HTML where all the links, source code coloring etc. are precalculated at indexing time. All the hyperlinks are hardwired to be simple links bypassing the server. 

The only component that runs on the webserver is a service that given a search query does the lookup and returns a list of matching types and members, which are hyperlinks into the static HTML. The webservice keeps a list of all declared types and members in memory, this list is also precalculated at indexing time. All services, such as Find All References, Project Explorer, etc. are all pre-rendered. 

The generator is not incremental. You have to generate into an empty folder from scratch every time, and then replace the currently deployed folder with the new contents atomically (using e.g. Azure Deployments, robocopy /MIR to inetpub\\wwwroot, etc). For smaller projects, deploying to Azure using Dropbox or Git would work just fine.

###Limitations and known issues
 1. Indexing more than one project with the same assembly name is currently unsupported. Only the first project wins. This is due to a fundamental design decision to only reference an assembly by short name. Customizers should add a means to filter "victim" projects out in their forks to pick the best single project for inclusion in the index.
 2. The generated website can only be hosted in the root of the domain. Making it run from a subdirectory is non-trivial and unlikely to be supported.

##Project status and contributions

This is a reference implementation that showcases the concepts and Roslyn usage. It comes with no guarantees, use at your own risk. We will consider accepting high-quality pull requests that add non-trivial value, however we have no plans to do significant work on the application in its current form. Any significant rearchitecture, adding large features, big refactorings won't be accepted because of resource constraints. Feel free to use it to generate websites for your own code, integrate in your CI servers etc. Feel free to do whatever you want in your own forks. Bug reports are gratefully accepted.

For any questions, feel free to reach out to [@KirillOsenkov](https://twitter.com/KirillOsenkov) on Twitter. Thanks to [@v2_matveev](https://twitter.com/v2_matveev) for contributing TypeScript support!
