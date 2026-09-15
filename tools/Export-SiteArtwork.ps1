<#
.SYNOPSIS
    Exports the favicon and sharing card from the original SVG artwork and canonical UI copy.
.PARAMETER BrowserPath
    Path to an installed Edge, Chrome, or Chromium executable. Defaults to Edge on Windows.
.EXAMPLE
    pwsh .\tools\Export-SiteArtwork.ps1
#>
#requires -Version 7.0
[CmdletBinding()]
param([string] $BrowserPath)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if (-not $BrowserPath -and $IsWindows) {
    $BrowserPath = Join-Path ${env:ProgramFiles(x86)} 'Microsoft' 'Edge' 'Application' 'msedge.exe'
}
if (-not $BrowserPath -or -not (Test-Path -LiteralPath $BrowserPath -PathType Leaf)) {
    throw 'Pass -BrowserPath with the path to an installed Edge, Chrome, or Chromium executable.'
}
$BrowserPath = (Resolve-Path -LiteralPath $BrowserPath).Path
$webRoot = Join-Path (Split-Path -Parent $PSScriptRoot) 'src' 'MausritterTools.Web' 'wwwroot'
$sourcePath = Join-Path $webRoot 'images' 'open-graph.svg'
$ui = Get-Content -LiteralPath (Join-Path $webRoot 'data' 'ui.json') -Raw | ConvertFrom-Json -AsHashtable
foreach ($key in 'title', 'description', 'socialImageAlt') {
    if ([string]::IsNullOrWhiteSpace($ui.app[$key])) {
        throw "The canonical UI text is missing app.$key."
    }
}
$svgNamespace = 'http://www.w3.org/2000/svg'
[xml] $card = Get-Content -LiteralPath $sourcePath -Raw

function Get-ArtworkElement([string] $Id) {
    $element = $card.SelectSingleNode("//*[@id='$Id']")
    if ($null -eq $element) {
        throw "The card SVG is missing the '$Id' element."
    }
    return $element
}

function Set-WrappedText([string] $Id, [string] $Text, [int] $Columns, [int] $LineHeight) {
    $element = Get-ArtworkElement $Id
    $lines = [System.Collections.Generic.List[string]]::new()
    $line = ''
    foreach ($word in ($Text -split '\s+')) {
        if ($word.Length -gt $Columns) {
            throw "The word '$word' does not fit in '$Id'; adjust the artwork layout."
        }
        if ($line -and ($line.Length + 1 + $word.Length) -gt $Columns) {
            $lines.Add($line)
            $line = ''
        }
        $line = if ($line) { "$line $word" } else { $word }
    }
    $lines.Add($line)
    if ($lines.Count -gt 2) {
        throw "The copy for '$Id' needs more than two lines; adjust the artwork layout."
    }

    $element.InnerText = ''
    for ($i = 0; $i -lt $lines.Count; $i++) {
        $span = $card.CreateElement('tspan', $svgNamespace)
        $span.SetAttribute('x', $element.GetAttribute('x'))
        $span.SetAttribute('dy', $(if ($i -eq 0) { '0' } else { "$LineHeight" }))
        $span.InnerText = $lines[$i]
        [void] $element.AppendChild($span)
    }
}

function Save-Svg([xml] $Document, [string] $Path) {
    $settings = [System.Xml.XmlWriterSettings]::new()
    $settings.Encoding = [System.Text.UTF8Encoding]::new($false)
    $settings.Indent = $true
    $settings.OmitXmlDeclaration = $true
    $settings.NewLineChars = "`n"
    $writer = [System.Xml.XmlWriter]::Create($Path, $settings)
    try { $Document.Save($writer) } finally { $writer.Dispose() }
}

(Get-ArtworkElement 'card-accessible-title').InnerText = $ui.app.title
(Get-ArtworkElement 'card-alt').InnerText = $ui.app.socialImageAlt
Set-WrappedText 'card-title' $ui.app.title 12 88
Set-WrappedText 'card-description' $ui.app.description 40 36

[xml] $favicon = "<svg xmlns='$svgNamespace' width='64' height='64' viewBox='0 0 64 64' role='img' aria-labelledby='favicon-title' />"
$title = $favicon.CreateElement('title', $svgNamespace)
$title.SetAttribute('id', 'favicon-title')
$title.InnerText = $ui.app.title
[void] $favicon.DocumentElement.AppendChild($title)
$mark = $favicon.CreateElement('g', $svgNamespace)
$mark.SetAttribute('id', 'site-mark')
foreach ($child in (Get-ArtworkElement 'site-mark').ChildNodes) {
    [void] $mark.AppendChild($favicon.ImportNode($child, $true))
}
[void] $favicon.DocumentElement.AppendChild($mark)

$jobs = @(
    @{ width = 32; height = 32; svg = $favicon.OuterXml; file = 'favicon.png' }
    @{ width = 1200; height = 630; svg = $card.OuterXml; file = (Join-Path 'images' 'open-graph.png') }
)
$payload = @($jobs | ForEach-Object {
    @{ width = $_.width; height = $_.height
       source = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($_.svg)) }
}) | ConvertTo-Json -Compress

# A canvas gives exact pixel sizes, including icons smaller than a headless browser's minimum viewport.
$html = @"
<!DOCTYPE html>
<html><body><pre id="result">Rendering</pre><script>
async function render() {
    const results = [];
    for (const job of $payload) {
        const image = new Image();
        image.src = "data:image/svg+xml;base64," + job.source;
        await image.decode();
        const canvas = document.createElement("canvas");
        canvas.width = job.width;
        canvas.height = job.height;
        const context = canvas.getContext("2d");
        if (!context) throw new Error("A 2D canvas is unavailable.");
        context.drawImage(image, 0, 0, job.width, job.height);
        results.push(canvas.toDataURL("image/png"));
    }
    document.getElementById("result").textContent = JSON.stringify(results);
}
render().catch(error => {
    document.getElementById("result").textContent = "Export failed: " + error.message;
});
</script></body></html>
"@
$temporaryRoot = Join-Path ([System.IO.Path]::GetTempPath()) "mausritter-artwork-$([guid]::NewGuid().ToString('N'))"
[void] [System.IO.Directory]::CreateDirectory($temporaryRoot)
$browser = [System.Diagnostics.Process]::new()
$started = $false
try {
    $pagePath = Join-Path $temporaryRoot 'render.html'
    [System.IO.File]::WriteAllText($pagePath, $html)
    $browser.StartInfo.FileName = $BrowserPath
    $browser.StartInfo.UseShellExecute = $false
    $browser.StartInfo.RedirectStandardOutput = $true
    $browser.StartInfo.RedirectStandardError = $true
    foreach ($argument in @(
        '--headless=new', '--disable-background-networking', '--disable-component-update',
        '--disable-sync', '--no-first-run', '--no-default-browser-check',
        "--user-data-dir=$(Join-Path $temporaryRoot 'profile')",
        '--dump-dom', '--virtual-time-budget=5000', ([uri] $pagePath).AbsoluteUri
    )) {
        $browser.StartInfo.ArgumentList.Add($argument)
    }
    if (-not $browser.Start()) { throw 'The artwork browser could not be started.' }
    $started = $true
    $stdout = $browser.StandardOutput.ReadToEndAsync()
    $stderr = $browser.StandardError.ReadToEndAsync()
    if (-not $browser.WaitForExit(30000)) {
        Stop-Process -Id $browser.Id -Force
        throw 'The artwork browser did not finish within 30 seconds.'
    }
    $output = $stdout.GetAwaiter().GetResult()
    $errors = $stderr.GetAwaiter().GetResult()
    if ($browser.ExitCode -ne 0) { throw "The artwork browser failed: $errors" }
    $match = [regex]::Match($output, '<pre id="result">([^<]*)</pre>')
    if (-not $match.Success) { throw "The artwork browser returned no render result. $errors" }
    $result = [System.Net.WebUtility]::HtmlDecode($match.Groups[1].Value)
    if (-not $result.StartsWith('["data:image/png;base64,')) {
        throw "The artwork browser did not produce PNGs: $result. $errors"
    }
    $images = @($result | ConvertFrom-Json)
    if ($images.Count -ne $jobs.Count) { throw 'The artwork export is incomplete.' }
    $bytesByImage = @($images | ForEach-Object {
        if (-not $_.StartsWith('data:image/png;base64,')) { throw 'An exported image is not a PNG.' }
        ,([Convert]::FromBase64String($_.Substring('data:image/png;base64,'.Length)))
    })
    for ($i = 0; $i -lt $jobs.Count; $i++) {
        $bytes = $bytesByImage[$i]
        if ($bytes.Length -lt 24 -or [Convert]::ToHexString($bytes[0..7]) -ne '89504E470D0A1A0A') {
            throw "The exported $($jobs[$i].file) has an invalid PNG header."
        }
        $width = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 16))
        $height = [System.Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, 20))
        if ($width -ne $jobs[$i].width -or $height -ne $jobs[$i].height) {
            throw "The exported $($jobs[$i].file) has the wrong dimensions: ${width}x${height}."
        }
    }
    Save-Svg $card $sourcePath
    Save-Svg $favicon (Join-Path $webRoot 'favicon.svg')
    for ($i = 0; $i -lt $jobs.Count; $i++) {
        [System.IO.File]::WriteAllBytes((Join-Path $webRoot $jobs[$i].file), $bytesByImage[$i])
        Write-Host "Exported $($jobs[$i].file) ($($jobs[$i].width)x$($jobs[$i].height))."
    }
}
finally {
    if ($started -and -not $browser.HasExited) {
        Stop-Process -Id $browser.Id -Force
        $browser.WaitForExit()
    }
    $browser.Dispose()
    Remove-Item -LiteralPath $temporaryRoot -Recurse -Force
}
