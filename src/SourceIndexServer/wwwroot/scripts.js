var currentSelection = null;
var currentResult = null;
var useSolutionExplorer = /*USE_SOLUTION_EXPLORER*/true/*USE_SOLUTION_EXPLORER*/;
var anchorSplitChar = ",";

var externalUrlMap = [
    /*EXTERNAL_URL_MAP*/"https://referencesource.microsoft.com/", "http://source.roslyn.io/"/*EXTERNAL_URL_MAP*/
];

var supportedFileExtensions = [
    "cs",
    "vb",
    "ts",
    "csproj",
    "vbproj",
    "targets",
    "props",
    "xaml"
];

function redirectLocation(frame, newLocation) {
    if (frame.location.href == newLocation) {
        return;
    }

    frame.location.replace(newLocation);
}

function setHash(newHash) {
    if (newHash.charAt(0) == '#') {
        newHash = newHash.slice(1);
    }

    top.history.replaceState(null, top.document.title, '#' + newHash);
}

function onHashChanged(e) {
    processHash();
}

function populateSearchBox(text) {
    ensureSearchBox();
    searchBox.focus();
    searchBox.value = text;
    searchBox.onkeyup();
}

function processHash() {
    var anchor = document.location.hash;
    if (anchor) {
        anchor = anchor.slice(1);

        if (!anchor) {
            if (top.location.pathname != "/") {
                redirectLocation(top, "/");
            }

            return;
        }

        if (startsWithIgnoreCase(anchor, "MSBuildProperty=")) {
            redirectLocation(n, "/MSBuildProperties/R/" + anchor.slice("MSBuildProperty=".length) + ".html");
            return;
        }

        if (startsWithIgnoreCase(anchor, "MSBuildItem=")) {
            redirectLocation(n, "/MSBuildItems/R/" + anchor.slice("MSBuildItem=".length) + ".html");
            return;
        }

        if (startsWithIgnoreCase(anchor, "MSBuildTarget=")) {
            redirectLocation(n, "/MSBuildTargets/R/" + anchor.slice("MSBuildTarget=".length) + ".html");
            return;
        }

        if (startsWithIgnoreCase(anchor, "MSBuildTask=")) {
            redirectLocation(n, "/MSBuildTasks/R/" + anchor.slice("MSBuildTask=".length) + ".html");
            return;
        }

        if (startsWithIgnoreCase(anchor, "EmptyArrayAllocation")) {
            redirectLocation(n, "/mscorlib/R/EmptyArrayAllocation.html");
            return;
        }

        if (startsWith(anchor, "q=")) {
            anchor = anchor.slice(2);
            anchor = unescape(anchor);
            populateSearchBox(anchor);
            return;
        }

        var hashParts = anchor.split(anchorSplitChar);
        if (anchor.indexOf(anchorSplitChar) == -1 && anchor.indexOf("#") > -1) {
            // keep old URLs working for compat
            hashParts = anchor.split("#");
        }

        var potentialFile = anchor;
        var entireAnchorIsFile = true;
        var specialAnchorType = "";
        var hashOrLine = "";
        if (hashParts.length > 1) {
            var lastPart = hashParts[hashParts.length - 1];
            if (lastPart == "references" || lastPart == "namespaces") {
                specialAnchorType = hashParts.pop();
                entireAnchorIsFile = false;
            }
            lastPart = hashParts[hashParts.length - 1];
            var lineNumberRegex = new RegExp("^\\d+$");
            var hashRegex = new RegExp("^[0-9a-f]{16}$")
            if (lineNumberRegex.test(lastPart) || hashRegex.test(lastPart)) {
                hashOrLine = hashParts.pop();
                entireAnchorIsFile = false;
            }
            potentialFile = hashParts.join(anchorSplitChar);
        }

        potentialFile = decodeURIComponent(potentialFile);

        if (potentialFile.charAt(0) == "/") {
            potentialFile = potentialFile.slice(1);
        }

        if (potentialFile.charAt(potentialFile.length - 1) == "/") {
            potentialFile = potentialFile.slice(0, potentialFile.length - 1);
        }

        if (isFile(potentialFile)) {
            var fileUrl = potentialFile;

            if (!endsWithIgnoreCase(fileUrl, ".html")) {
                fileUrl = fileUrl + ".html";
            }

            if (hashOrLine) {
                fileUrl = fileUrl + "#" + createSafeLineNumber(hashOrLine);
            }

            redirectLocation(s, fileUrl);

            var pathParts = potentialFile.split("/");
            if (pathParts.length > 1) {
                if (specialAnchorType == "references") {
                    redirectLocation(n, "/" + pathParts[0] + "/R/" + hashOrLine + ".html");
                }
                else {
                    if (pathParts[0] != "MSBuildFiles" && pathParts[0] != "TypeScriptFiles") {
                        redirectLocation(n, "/" + pathParts[0] + "/ProjectExplorer.html");
                    }
                }
            }
        } else if (entireAnchorIsFile && potentialFile.indexOf("/") == -1) {
            redirectLocation(n, "/" + potentialFile + "/ProjectExplorer.html");
        } else if (specialAnchorType == "namespaces" && potentialFile.indexOf("/") == -1) {
            redirectLocation(n, "/" + potentialFile + "/namespaces.html");
        }
    } else if (useSolutionExplorer) {
        redirectLocation(n, "solutionexplorer.html");
    }
}

function onPageLoaded() {
    if (navigator.appVersion.indexOf("MSIE") == -1) {
        document.getElementById("s").frameBorder = "1";
    }

    window.onhashchange = onHashChanged;

    top.name = "topFrame";

    var query = document.location.search;
    if (query && query.slice(0, 3) == "?q=") {
        redirectLocation(top, "/#" + query.slice(1));
        return;
    }

    var pathname = document.location.pathname;
    if (pathname.toLowerCase().slice(0, 11) == "/index.html") {
        redirectLocation(top, "/");
        return;
    }

    if (pathname.length > 1) {
        setHash(pathname.slice(1) + location.hash);
        redirectLocation(top, "/");
        return;
    }

    processHash();
}

function onHeaderLoad() {
    ensureSearchBox();
    searchTimerID = -1;
    lastQuery = null;
    lastSearchString = searchBox.value;

    // Place the cursor at the end of the search box by default
    searchBox.focus();

    searchBox.onkeyup = function () {
        if (this.value != lastSearchString || (event && event.keyCode == 13)) {
            lastSearchString = this.value;
            if (!top.n.document.getElementById("symbols")) {
                top.n.location = "results.html";
                setTimeout(onSearchChange, 50);
            }

            onSearchChange();
        }
    };
}

function onResultsLoad() {
    ensureSearchBox();

    if (searchBox && searchBox.value && searchBox.value.length > 2) {
        runSearch();
    }
}

// init document (called in body onload)
function i(lineNumberCount) {
    if (isTopFrame()) {
        redirectToIndex();
        return;
    }

    var isLargeFile = lineNumberCount > 30000;

    setPageTitle(document.title);

    if (!isLargeFile) {
        generateLineNumbers("ln", lineNumberCount);
        initializeHighlightReferences();

        if (top.symbolIdToHighlight) {
            highlightOccurrence(top.lineNumberToHighlight, top.symbolIdToHighlight);
            top.symbolIdToHighlight = null;
            top.lineNumberToHighlight = null;
        }

        addToolbar();

        rewriteExternalLinks();
        trackActiveItemInSolutionExplorer();

        var projectPathLink = document.getElementById("projectPath");
        if (projectPathLink) {
            projectPathLink.onclick = function () {
                var assemblyName = getAssemblyName();
                top.n.location = "/" + assemblyName + "/ProjectExplorer.html";
                setHash(assemblyName);
                return false;
            }
        }
    }

    updateTopHashFromRightPane();

    var element = top.s.document.getElementById(top.s.location.hash.slice(1));
    if (element && !isLargeFile) { // for some reason focusing here for a large file hangs IE
        element.focus();
    }
}

function updateTopHashFromRightPane() {
    var filePath = top.s.location.pathname.slice(1);
    filePath = getDisplayableFileName(filePath);

    var newHash = filePath;

    var oldRightPaneHash = top.s.location.hash;
    if (oldRightPaneHash) {
        var newRightPaneHash = createSafeLineNumber(oldRightPaneHash);
        if (newRightPaneHash != oldRightPaneHash) {
            top.s.location.hash = newRightPaneHash;
        }

        newHash = newHash + anchorSplitChar + getDisplayableLineNumber(newRightPaneHash).slice(1);
    }

    setHash(newHash);
}

// init xml document (called in body onload)
function ix(lineNumberCount) {
    if (isTopFrame()) {
        redirectToIndex();
        return;
    }

    var isLargeFile = lineNumberCount > 30000;

    setPageTitle(document.title);

    if (!isLargeFile) {
        generateLineNumbers("ln", lineNumberCount);
        initializeHighlightReferences();

        if (top.symbolIdToHighlight) {
            highlightOccurrence(top.lineNumberToHighlight, top.symbolIdToHighlight);
            top.symbolIdToHighlight = null;
            top.lineNumberToHighlight = null;
        }

        trackActiveItemInSolutionExplorer();
    }

    updateTopHashFromRightPane();

    var element = top.s.document.getElementById(top.s.location.hash.slice(1));
    if (element && !isLargeFile) { // for some reason focusing here for a large file hangs IE
        element.focus();
    }
}

function rewriteExternalLinks() {
    var links = document.links;
    var length = links.length;
    for (var i = 0; i < length; i++) {
        var link = links[i];
        rewriteExternalLink(link);
    }
}

function rewriteExternalLink(link) {
    var url = link.getAttribute("href");

    var firstIndex = url.indexOf("@");
    if (firstIndex > -1) {
        var indexLength = url.indexOf("@", firstIndex + 1);
        var externalIndexNumber = url.slice(firstIndex + 1, indexLength);
        var externalUrl = externalUrlMap[externalIndexNumber];
        url = externalUrl + url.slice(indexLength + 1);
        link.href = url;
        link.target = "_top";
    }

    if (link.hash && link.hash.length == 17) {
        link.onclick = function () {
            var filePath = top.s.location.pathname.slice(1);
            filePath = getDisplayableFileName(filePath);
            filePath = filePath + anchorSplitChar + this.hash.slice(1);
            setHash(filePath);
        };
        return;
    }

    if (endsWith(url, "/0000000000.html")) {
        var filePath = top.s.location.pathname.slice(1);
        filePath = getDisplayableFileName(filePath);
        filePath = "/#" + filePath + anchorSplitChar + link.id;
        link.href = filePath;
        link.onclick = function () {
            redirectLocation(top.n, "/0000000000.html");
            return false;
        };
        return;
    }
}

function rewriteSolutionExplorerLink(link) {
    var url = link.href;
    var fileName = trimFromEnd(url, ".html");
    var extension = getExtension(fileName);
    var pathname = link.pathname;

    var setClassName = null;
    if (isSupportedExtension(extension) && !link.className) {
        setClassName = extension;
    } else {
        rewriteExternalLink(link);
    }

    if (setClassName) {
        link.className = setClassName;
        link.target = "s";
        link.textContent = getFileName(url);
        var assembly = getAssemblyFromExplorerFile(link);
        if (assembly) {
            if (extension != "ts") {
                link.href = assembly + pathname;
            }
        }
    }
}

function getFileName(url) {
    var lastSlash = url.lastIndexOf('/');
    if (lastSlash != -1) {
        url = url.slice(lastSlash + 1);
    }

    url = url.slice(0, url.length - 5);
    url = unescape(url);
    return url;
}

function getAssemblyFromExplorerFile(a) {
    while (a) {
        a = a.parentElement;
        if (a && a.getAttribute("data-assembly")) {
            return a.getAttribute("data-assembly");
        }
    }

    return null;
}

// onload of references file
function ro() {
    if (isTopFrame()) {
        redirectToSymbolReferences();
        return;
    }

    setPageTitle(document.title);

    var path = document.location.pathname;
    var symbolId = path.substring(path.lastIndexOf("/") + 1, path.lastIndexOf("."))
    for (var i = 0; i < document.links.length; i++) {
        var link = document.links[i];
        link.target = "s";
        link.className = "rL";
        link.onclick = function () {
            var actual = top.s.document.location.pathname;
            var expected = this.pathname;
            if (actual == expected || actual.substring(1) == expected) {
                highlightOccurrence(this.hash.substring(1), symbolId);
            } else {
                top.symbolIdToHighlight = symbolId;
                top.lineNumberToHighlight = this.hash.substring(1);
            }
        };
    }

    var displayableFileName = getDisplayableFileName(top.s.location.pathname.slice(1));
    var newHash = displayableFileName + anchorSplitChar + symbolId + anchorSplitChar + "references";
    if (startsWithIgnoreCase(path, "/MSBuildProperties")) {
        newHash = "MSBuildProperty=" + symbolId;
    } else if (startsWithIgnoreCase(path, "/MSBuildItems")) {
        newHash = "MSBuildItem=" + symbolId;
    } else if (startsWithIgnoreCase(path, "/MSBuildTargets")) {
        newHash = "MSBuildTarget=" + symbolId;
    } else if (startsWithIgnoreCase(path, "/MSBuildTasks")) {
        newHash = "MSBuildTask=" + symbolId;
    } else if (startsWithIgnoreCase(path, "/mscorlib/R/EmptyArrayAllocation")) {
        newHash = "EmptyArrayAllocation";
    }

    setHash(newHash);

    var headers = document.getElementsByClassName("rA");
    for (var i = 0; i < headers.length; i++) {
        var header = headers[i];
        header.onclick = function () {
            var collapsible = this.nextSibling;
            if (collapsible.style.display == "none") {
                collapsible.style.display = "block";
                this.style.backgroundImage = "url(../../content/icons/minus.png)";
            } else {
                collapsible.style.display = "none";
                this.style.backgroundImage = "url(../../content/icons/plus.png)";
            }
        };
    }

    var fileHeaders = document.getElementsByClassName("rN");
    for (var i = 0; i < fileHeaders.length; i++) {
        var fileHeader = fileHeaders[i];
        var fileName = getInnerText(fileHeader);
        var openParen = fileName.lastIndexOf(" (");
        if (openParen !== -1) {
            fileName = fileName.slice(0, openParen);
        }

        var extension = fileName.substring(fileName.length - 2);
        var imageUrl = null;
        if (extension == "cs") {
            imageUrl = "url(../../content/icons/196.png)";
        } else if (extension == "vb") {
            imageUrl = "url(../../content/icons/195.png)";
        }

        if (imageUrl) {
            fileHeader.style.backgroundImage = imageUrl;
        }
    }
}

function onDocumentOutlineLoad() {
    var root = document.getElementById('root');
    root.style.cursor = "pointer";
    var doc = top.s.document;
    var links = doc.getElementsByTagName('a');
    for (var i = 0; i < links.length; i++) {
        var link = links[i];
        var dataGlyphText = link.getAttribute('data-glyph');
        if (link && link.id && link.id.length == 16 && dataGlyphText) {
            var a = document.createElement('a');
            a.href = doc.location.pathname + "#" + link.id;
            a.target = "s";

            var dataGlyph = dataGlyphText.split(",");
            var glyph = dataGlyph[0];
            var indent = dataGlyph[1];

            var div = document.createElement('div');
            div.className = "documentOutlineDiv";
            div.style.marginLeft = (32 * indent) + 'px';
            div.style.paddingTop = "2px";
            div.style.paddingBottom = "2px";

            var img = document.createElement('img');
            img.src = '/content/icons/' + glyph + '.png';
            img.style.marginRight = '8px';
            img.style.verticalAlign = 'bottom';
            div.appendChild(img);

            var keywordText = getKeywordsFromGlyph(glyph);
            var keyword = document.createElement('span');
            keyword.className = "k";
            setInnerText(keyword, keywordText);
            keyword.style.marginRight = '6px';
            keyword.style.verticalAlign = 'center';
            //div.appendChild(keyword);

            var name = document.createElement('span');
            var text = link.textContent;

            // append ~ for destructors
            if (link.previousSibling && endsWith(link.previousSibling.textContent, "~")) {
                text = "~" + text;
            }

            setInnerText(name, text);
            name.style.verticalAlign = 'center';
            div.appendChild(name);

            a.appendChild(div);

            root.appendChild(a);
        }
    }
}

function resultClick(sender) {
    if (currentResult) {
        if (currentResult.classList) {
            currentResult.classList.remove("currentResult");
        } else {
            currentResult.className = "resultItem";
        }
    }

    currentResult = sender;
    if (currentResult.classList) {
        currentResult.classList.add("currentResult");
    } else {
        currentResult.className = "resultItem currentResult";
    }
}

function isFile(path) {
    if (endsWithIgnoreCase(path, ".html")) {
        path = path.slice(0, path.length - 5);
    }

    var extension = getExtension(path);
    return isSupportedExtension(extension);
}

function ensureSearchBox() {
    if (typeof searchBox === "object" && searchBox != null) {
        return;
    }

    if (typeof h === "object") {
        searchBox = h.document.getElementById("search-box");
    } else if (typeof top.h === "object") {
        searchBox = top.h.document.getElementById("search-box");
    } else {
        searchBox = document.getElementById("search-box");
    }
}

function onSearchChange() {
    ensureSearchBox();
    if (searchBox.value.length > 2) {
        if (searchTimerID == -1) {
            searchTimerID = setTimeout(runSearch, 200);
        }
    } else {
        loadSearchResults("<div class='note'>Enter at least 3 characters.</div>");
    }
}

function runSearch() {
    ensureSearchBox();
    searchTimerID = -1;
    if (typeof lastQuery === "object" && lastQuery !== null) {
        lastQuery.abort();
        lastQuery = null;
    }

    setPageTitle(searchBox.value);
    lastQuery = getUrl("api/symbols/?symbol=" + escape(searchBox.value), loadSearchResults);
}

function getUrl(url, callback) {
    //return $.get(url, null, callback, null);
    var xhr = new XMLHttpRequest();
    xhr.open("GET", url, true);
    xhr.setRequestHeader("Accept", "text/html");
    xhr.onreadystatechange = function () {
        if (xhr.readyState == 4) {
            var data = xhr.responseText;
            if (typeof data === "string" && data.length > 0) {
                callback(data);
            }
        }
    };
    xhr.send();
    return xhr;
}

function loadSearchResults(data) {
    if (top.n) {
        var container = top.n.document.getElementById("symbols");
        if (!container) {
            container = top.n.document.getElementById("note");
        }

        if (container) {
            container.innerHTML = data;
            if (searchBox && searchBox.value && searchBox.value.length > 2) {
                setHash("q=" + escape(searchBox.value));
            }

            if (data && data.length > 40 && data.slice(0, 40) === '<div class="note">Index is being rebuilt') {
                searchTimerID = -1;
                onSearchChange();
            }
        }
    }
}

// this is usually called in the "s" frame
function redirect(map, prefixLength) {
    if (!prefixLength) {
        prefixLength = 16;
    }

    var anchor = document.location.hash;
    if (anchor) {
        anchor = anchor.slice(1);
        var hashParts = anchor.split(anchorSplitChar);
        var anchorHasReferencesSuffix = false;
        if (hashParts.length > 1 && hashParts[hashParts.length - 1] == "references") {
            anchorHasReferencesSuffix = true;
            hashParts.pop();
        }
        var id = hashParts.join(anchorSplitChar);
        var shortId = id;
        if (prefixLength < shortId.length) {
            shortId = shortId.slice(0, prefixLength);
        }

        // all the keys have their first character trimmed since it's a bucket file aX.html
        // and X is the same for all ids
        shortId = shortId.slice(1);

        var redirectTo = map[shortId];
        if (redirectTo) {
            var destination = redirectTo + ".html" + "#" + createSafeLineNumber(id);
            if (anchorHasReferencesSuffix) {
                destination = destination + anchorSplitChar + "references";
            }

            redirectLocation(document, destination);
        }
    }
}

// multi-staged redirect a.html -> a0.html -> filePath.html (to reduce size of a.html)
function redirectToNextLevelRedirectFile() {
    var anchor = document.location.hash;
    if (anchor) {
        anchor = anchor.slice(1);
        var hashParts = anchor.split(anchorSplitChar);
        var anchorHasReferencesSuffix = false;
        if (hashParts.length > 1 && hashParts[hashParts.length - 1] == "references") {
            anchorHasReferencesSuffix = true;
            hashParts.pop();
        }
        var id = hashParts.join(anchorSplitChar);

        var destination = "A" + id.slice(0, 1) + ".html" + "#" + createSafeLineNumber(id);
        if (anchorHasReferencesSuffix) {
            destination = destination + anchorSplitChar + "references";
        }

        redirectLocation(document, destination);
    }
}

// this is usually called in the "n" frame
function redirectToReferences() {
    var anchor = document.location.hash;
    if (anchor) {
        var destination = "R/" + anchor + ".html";
        redirectLocation(document, destination);
    }
}

function generateLineNumbers(id, count) {
    if (count == 0) {
        return;
    }

    var filePath = document.location.pathname.slice(1);
    filePath = getDisplayableFileName(filePath);

    var list = [];
    for (var i = 1; i <= count; i++) {
        var line =
            "<a id=\"l" +
            i +
            "\" href=\"" +
            "/#" +
            filePath + anchorSplitChar +
            i +
            "\" target=\"_self\" onclick=\"setHash('" +
            filePath.replace("'", "\\'") + anchorSplitChar + i + "');document.location.hash='l" +
            i +
            "';return false;\">" + i + "</a><br>";
        list.push(line);
    }

    var text = list.join("");

    document.getElementById(id).innerHTML = text;
}

function highlightOccurrence(lineNumber, symbolId) {
    var sourceDocument = top.s.document;
    if (sourceDocument.currentLine) {
        sourceDocument.currentLine.style.background = "transparent";
    }

    var lineNumberId = createSafeLineNumber(lineNumber);
    sourceDocument.location.hash = lineNumberId;

    var lineNumberSpan = sourceDocument.getElementById(lineNumberId);
    lineNumberSpan.style.background = "lime";
    sourceDocument.currentLine = lineNumberSpan;

    // there are two kinds of links in the document page:
    // 1. links to definitions
    // 2. links on line numbers
    // Clear the links which aren't references to the symbol currently referenced
    // and the line numbers which aren't the current line
    for (var i = 0; i < sourceDocument.links.length; i++) {
        var link = sourceDocument.links[i];
        var target = link.hash.substring(1);
        if (target == symbolId) {
            link.style.background = "yellow";
        }
        else if (link !== lineNumberSpan) {
            link.style.background = "transparent";
        }
    }
}

// highlight references
function t(sender) {
    var classname = sender.className;

    var elements;
    if (currentSelection) {
        elements = document.getElementsByClassName(currentSelection);
        for (var i = 0; i < elements.length; i++) {
            elements[i].style.background = "transparent";
        }

        var def = document.getElementById(currentSelection.replace(" r", " rd"));
        if (def) {
            def.style.borderColor = "transparent";
        }

        if (classname == currentSelection) {
            currentSelection = null;
            return;
        }
    }

    currentSelection = classname;

    elements = document.getElementsByClassName(currentSelection);
    for (var i = 0; i < elements.length; i++) {
        elements[i].style.background = "cyan";
    }

    var def = document.getElementById(currentSelection.replace(" r", " rd"));
    if (def) {
        def.style.borderColor = "black";
        def.style.borderStyle = "solid";
        def.style.borderWidth = "1px";
    }
}

function initializeHighlightReferences() {
    elements = document.getElementsByClassName("r");
    for (var i = 0; i < elements.length; i++) {
        elements[i].onclick = function () { t(this); };
    }
}

function addToolbar() {
    var documentOutlineButton = document.createElement('img');
    documentOutlineButton.setAttribute('src', '/content/icons/DocumentOutline.png');
    documentOutlineButton.title = "Document Outline";
    documentOutlineButton.className = 'documentOutlineButton';
    documentOutlineButton.onclick = showDocumentOutline;
    document.body.appendChild(documentOutlineButton);

    var projectExplorerButton = document.createElement('img');
    var projectExplorerIcon = '/content/icons/CSharpProjectExplorer.png';
    if (document.title.slice(document.title.length - 2) == "vb") {
        projectExplorerIcon = '/content/icons/VBProjectExplorer.png';
    }

    projectExplorerButton.setAttribute('src', projectExplorerIcon);
    projectExplorerButton.title = "Project Explorer";
    projectExplorerButton.className = 'projectExplorerButton';
    projectExplorerButton.onclick = function () { document.getElementById('projectPath').click(); };
    document.body.appendChild(projectExplorerButton);

    var namespaceExplorerButton = document.createElement('img');
    namespaceExplorerButton.setAttribute('src', '/content/icons/NamespaceExplorer.png');
    namespaceExplorerButton.title = "Namespace Explorer";
    namespaceExplorerButton.className = 'namespaceExplorerButton';
    namespaceExplorerButton.onclick = showNamespaceExplorer;
    document.body.appendChild(namespaceExplorerButton);
}

function showDocumentOutline() {
    top.n.location = "/documentoutline.html";
}

function showNamespaceExplorer() {
    var assemblyName = getAssemblyName();
    var namespacesUrl = "/" + assemblyName + "/namespaces.html";
    top.n.location = namespacesUrl;
    setHash(assemblyName + ",namespaces");
}

// Firefox doesn't support innerText, but it supports textContent
// See http://blog.coderlab.us/2005/09/22/using-the-innertext-property-with-firefox/
function setInnerText(element, text) {
    if (typeof element.innerText !== "undefined") {
        element.innerText = text;
    } else {
        element.textContent = text;
    }
}

function getInnerText(element) {
    if (typeof element.innerText !== "undefined") {
        return element.innerText;
    } else {
        return element.textContent;
    }
}

function getKeywordsFromGlyph(glyph) {
    switch (glyph - (glyph % 6)) {
        case 0:
            return "class";
        case 6:
            return "constant";
        case 12:
            return "delegate";
        case 18:
            return "enum";
        case 24:
            return "enum member";
        case 30:
            return "event";
        case 36:
            return "exception";
        case 42:
            return "field";
        case 48:
            return "interface";
        case 54:
            return "macro";
        case 60:
            return "map";
        case 66:
            return "map item";
        case 72:
            return "method";
        case 78:
            return "overload";
        case 84:
            return "module";
        case 90:
            return "namespace";
        case 96:
            return "operator";
        case 102:
            return "property";
        case 108:
            return "struct";
        case 114:
            return "type parameter";
        case 150:
            return "module";
        case 220:
            return "extension method";
        default:
            return "symbol";
    }
}

function trackActiveItemInSolutionExplorer() {
    if (top.n) {
        var doc = top.n.document;
        if (doc) {
            var rootFolderDiv = doc.getElementById('rootFolder');
            if (rootFolderDiv && (rootFolderDiv.className == "projectCS" || rootFolderDiv.className == "projectVB")) {
                rootFolderDiv = rootFolderDiv.nextElementSibling;
                if (rootFolderDiv) {
                    var filePath = getFilePath();
                    if (filePath) {
                        selectItem(rootFolderDiv, filePath.split("\\"));
                    }
                }
            }
        }
    }
}

function selectItem(div, parts) {
    var text = parts[0];
    var found = null;
    for (var i = 0; i < div.children.length; i++) {
        var child = div.children[i];
        if (getInnerText(child) == text) {
            found = child;
            break;
        }
    }

    if (!found) {
        return;
    }

    if (parts.length == 1 && found.tagName == "A") {
        selectFile(found);
    }
    else if (parts.length > 1 && found.tagName == "DIV") {
        found = found.nextElementSibling;
        expandFolderIfNeeded(found);
        selectItem(found, parts.slice(1));
    }
}

function selectFile(a) {
    var selected = top.n.document.selectedFile;
    if (selected === a) {
        return;
    }

    if (selected && selected.classList) {
        selected.classList.remove("selectedFilename");
    }

    top.n.document.selectedFile = a;
    if (a) {
        if (a.classList) {
            a.classList.add("selectedFilename");
        }

        scrollIntoViewIfNeeded(a);
    }
}

function scrollIntoViewIfNeeded(element) {
    var topOfPage = window.pageYOffset || document.documentElement.scrollTop || document.body.scrollTop;
    var heightOfPage = window.innerHeight || document.documentElement.clientHeight || document.body.clientHeight;
    var elY = 0;
    var elH = 0;

    if (document.layers) {
        elY = element.y;
        elH = element.height;
    }
    else {
        for (var p = element; p && p.tagName != 'BODY'; p = p.offsetParent) {
            elY += p.offsetTop;
        }

        elH = element.offsetHeight;
    }

    if ((topOfPage + heightOfPage) < (elY + elH)) {
        element.scrollIntoView(false);
    }
    else if (elY < topOfPage) {
        element.scrollIntoView(true);
    }
}

function expandFolderIfNeeded(folder) {
    if (folder.style.display != "block" && folder && folder.previousSibling && folder.previousSibling.onclick) {
        folder.previousSibling.onclick();
    }
}

function getFilePath() {
    var a = top.s.document.getElementById("filePath");
    if (a) {
        return getInnerText(a);
    }

    return null;
}

function getAssemblyName() {
    var a = top.s.document.getElementById("projectPath");
    if (a) {
        var url = a.hash;
        return url.slice(1);
    }

    return null;
}

// this is called when clicking on the project link, redirecting from project\index.html to project\ProjectExplorer.html
function redirectToIndex() {
    var scriptPath = this.document.scripts[0].src;
    var rootPath = scriptPath.slice(0, scriptPath.length - 10);
    var sourcePath = this.document.location.href;
    var relativePath = sourcePath.slice(rootPath.length);
    var destination = rootPath + "#" + relativePath.replace("#", anchorSplitChar);
    redirectLocation(document, destination);
}

// this is called when the references file (/R/id.html) is loaded in the top frame
function redirectToSymbolReferences() {
    var referencesFilePath = this.document.location.href;
    var destination = referencesFilePath.replace("/R/", "/a.html" + "#");

    // strip off the ".html" suffix
    destination = destination.slice(0, destination.length - 5);
    destination = destination + anchorSplitChar + "references";
    redirectLocation(top, destination);
}

function toggle(header, id) {
    var element = document.getElementById(id);
    if (element.style.display == 'none') {
        header.style.backgroundImage = "url(content/icons/minus.png)";
        element.style.display = 'block';
    }
    else {
        header.style.backgroundImage = "url(content/icons/plus.png)";
        element.style.display = 'none';
    }
}

function isTopFrame() {
    return top === self;
}

function initializeProjectIndex(url) {
    if (!isTopFrame()) {
        url = "ProjectExplorer.html";
        redirectLocation(document, url);
    } else {
        redirectLocation(top, url);
    }
}

function initializeProjectExplorer() {
    makeFoldersCollapsible(/* closed folder */"202.png", "201.png", "../content/icons/", initializeSolutionExplorerFolder);
    initializeProjectExplorerRootFolder();
    trackActiveItemInSolutionExplorer();
}

function initializeProjectExplorerRootFolder() {
    var rootFolder = document.getElementById("rootFolder");
    if (rootFolder) {
        rootFolder = rootFolder.nextElementSibling;
        if (rootFolder) {
            initializeSolutionExplorerFolder(rootFolder);
        }
    }
}

function onSolutionExplorerLoad() {
    loadSolutionExplorer();
}

function loadSolutionExplorer() {
    makeFoldersCollapsible(/* closed folder */"202.png", "201.png", "content/icons/", initializeSolutionExplorerFolder);
    document.getElementById("rootFolder").style.display = "block";
}

function initializeNamespaceExplorer() {
    makeFoldersCollapsible(/* namespace */"90.png", "90.png", "../content/icons/", /*initializeSolutionExplorerFolder:*/ null);
}

function initializeSolutionExplorerFolder(folder) {
    for (var i = 0; i < folder.children.length; i++) {
        var child = folder.children[i];
        if (isLink(child)) {
            rewriteSolutionExplorerLink(child);
        }
    }
}

function makeFoldersCollapsible(folderIcon, openFolderIcon, pathToIcons, initializeHandler) {
    var elements = document.querySelectorAll(".folder");
    var length = elements.length;
    for (var i = 0; i < length; i++) {
        var folder = elements[i];
        folder.style.display = 'none';
        folder.initialize = initializeHandler;
        var div = folder.previousSibling;
        var firstChild = div.firstChild;

        var imagePlusMinus = document.createElement("img");
        imagePlusMinus.src = pathToIcons + "plus.png";
        imagePlusMinus.className = "imagePlusMinus";

        var imageFolder = document.createElement("img");
        imageFolder.src = pathToIcons + folderIcon;
        imageFolder.className = "imageFolder";
        setFolderImage(imageFolder, div, firstChild, pathToIcons, folderIcon);

        var handler = expandCollapseFolder(folder, imagePlusMinus, imageFolder, div, firstChild, pathToIcons, folderIcon, openFolderIcon);

        var skipImage = isLink(firstChild);
        if (skipImage) {
            div.insertBefore(imagePlusMinus, firstChild);
            imagePlusMinus.onclick = handler;
        } else {
            div.insertBefore(imageFolder, firstChild);
            div.insertBefore(imagePlusMinus, imageFolder);
            div.onclick = handler;
        }
    }
}

function isLink(element) {
    return element && element.tagName && element.tagName == "A";
}

function expandCollapseFolder(capturedFolder, capturedPlusMinus, capturedFolderImage, capturedDiv, capturedFirstChild, pathToIcons, folderIcon, openFolderIcon) {
    return function () {
        if (capturedFolder.style.display == 'none') {
            capturedPlusMinus.src = pathToIcons + "minus.png";
            if (capturedDiv.className != "projectCSInSolution" && capturedDiv.className != "projectVBInSolution") {
                capturedFolderImage.src = pathToIcons + openFolderIcon;
            }

            if (capturedFolder.initialize) {
                capturedFolder.initialize(capturedFolder);
                capturedFolder.initialize = null;
            }

            capturedFolder.style.display = 'block';
        }
        else {
            capturedPlusMinus.src = pathToIcons + "plus.png";
            setFolderImage(capturedFolderImage, capturedDiv, capturedFirstChild, pathToIcons, folderIcon);
            capturedFolder.style.display = 'none';
        }
    }
}

function setFolderImage(folder, div, firstChild, pathToIcons, folderIcon) {
    var text = firstChild.textContent;
    if (text === 'References' || text === "Used By") {
        folder.src = pathToIcons + "192.png";
    } else if (text === 'Properties') {
        folder.src = pathToIcons + "102.png";
    } else if (div.className == "projectCSInSolution") {
        folder.src = pathToIcons + "196.png";
    } else if (div.className == "projectVBInSolution") {
        folder.src = pathToIcons + "195.png";
    }
    else {
        folder.src = pathToIcons + folderIcon;
    }
}

function setPageTitle(title) {
    if (!title) {
        title = "Source Browser";
    }

    if (top && top.document) {
        top.document.title = title;
    }
}

function getDisplayableLineNumber(text) {
    if (text == "#") {
        return "";
    }

    if (text.slice(0, 2) == "#l") {
        text = anchorSplitChar + text.slice(2);
    }

    return text;
}

function getDisplayableFileName(text) {
    if (endsWithIgnoreCase(text, ".html")) {
        text = text.slice(0, text.length - 5);
    }

    text = encodeURIComponent(text);
    while (text.indexOf("%2F") > -1) {
        // don't escape slashes since they actually look nice in the URL unescaped
        text = text.replace("%2F", "/");
    }

    return text;
}

function createSafeLineNumber(text) {
    if (isNumber(text) && text.length != 16) {
        text = "l" + text;
    }

    return text;
}

function isNumber(n) {
    return !isNaN(parseFloat(n)) && isFinite(n);
}

function startsWith(text, prefix) {
    if (!text || !prefix) {
        return false;
    }

    if (prefix.length > text.length) {
        return false;
    }

    var slice = text.slice(0, prefix.length);
    return slice == prefix;
}

function startsWithIgnoreCase(text, prefix) {
    if (!text || !prefix) {
        return false;
    }

    if (prefix.length > text.length) {
        return false;
    }

    var slice = text.slice(0, prefix.length);
    return slice.toLowerCase() == prefix.toLowerCase();
}

function endsWith(text, suffix) {
    if (!text || !suffix) {
        return false;
    }

    if (suffix.length > text.length) {
        return false;
    }

    var slice = text.slice(text.length - suffix.length, text.length);
    return slice == suffix;
}

function endsWithIgnoreCase(text, suffix) {
    if (!text || !suffix) {
        return false;
    }

    if (suffix.length > text.length) {
        return false;
    }

    var slice = text.slice(text.length - suffix.length, text.length);
    return slice && (slice.toLowerCase() == suffix.toLowerCase());
}

function trimFromEnd(text, suffixToTrim) {
    if (!text || !suffixToTrim) {
        return text;
    }

    if (endsWithIgnoreCase(text, suffixToTrim)) {
        text = text.slice(0, text.length - suffixToTrim.length);
    }

    return text;
}

function getExtension(filePath) {
    if (!filePath) {
        return "";
    }

    var dot = filePath.lastIndexOf(".");
    if (dot == filePath.length - 1) {
        return "";
    }

    return filePath.slice(dot + 1).toLowerCase();
}

function isSupportedExtension(extension) {
    return supportedFileExtensions.indexOf(extension) != -1;
}
