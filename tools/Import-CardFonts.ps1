<#
.SYNOPSIS
    Imports the web fonts the item cards are set in.

.DESCRIPTION
    The official Mausritter Item Card Studio sets its cards in Texturina for the item name and a
    condensed Open Sans for everything else. Both are free fonts under the SIL Open Font License
    1.1, so this app can set its cards the same way.

    The site is static and must work offline, so the fonts are fetched once and served from
    wwwroot rather than linked from Google's CDN. Only the latin subset is taken: it covers the
    German umlauts and eszett, and the app ships in no other language.

    Note the second family is Open Sans itself, at the narrow end of its width axis, not the
    "Open Sans Condensed" family the Studio names. That family is a deprecated static cut of the
    same design and is no longer in Google's font repository, so its licence cannot be checked;
    Open Sans at wdth 75 is the same shapes from a maintained, verifiable source.

    Writes woff2 files and licence texts to src/MausritterTools.Web/wwwroot/fonts/ and a generated
    stylesheet to src/MausritterTools.Web/wwwroot/css/card-fonts.css. All of those are imported;
    re-run this script rather than editing them.

.PARAMETER Refresh
    Re-download files that are already present.

.EXAMPLE
    pwsh ./tools/Import-CardFonts.ps1
    pwsh ./tools/Import-CardFonts.ps1 -Refresh
#>
#requires -Version 7.0
[CmdletBinding()]
param([switch] $Refresh)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$WebRoot = Join-Path $RepoRoot 'src/MausritterTools.Web/wwwroot'
$FontDir = Join-Path $WebRoot 'fonts'
$StylesheetPath = Join-Path $WebRoot 'css/card-fonts.css'

# Asking for woff2 rather than the older formats an older browser would be offered.
$BrowserAgent = 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 ' +
    '(KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'

# The subset that carries ASCII, the accented latin letters and the punctuation and currency
# marks the app uses. Google names its subsets only in comments, so they are matched by range.
$LatinRange = 'U+0000-00FF'

$Families = @(
    [pscustomobject]@{
        Name        = 'Texturina'
        Slug        = 'texturina'
        # The item name, matching the Studio's own weight.
        Spec        = 'Texturina:wght@800'
        Used        = 'Item names'
        LicencePath = 'ofl/texturina'
        Project     = 'https://github.com/Omnibus-Type/Texturina'
    }
    [pscustomobject]@{
        Name        = 'Open Sans'
        Slug        = 'open-sans'
        # The whole width and weight range, so one variable file serves every use.
        Spec        = 'Open+Sans:wdth,wght@75..100,300..800'
        Used        = 'Usage dots, stat boxes and item classes, at the condensed width'
        LicencePath = 'ofl/opensans'
        Project     = 'https://github.com/googlefonts/opensans'
    }
)

function Get-FontFaceBlock {
    <#
    .SYNOPSIS
        Splits a Google Fonts stylesheet into its @font-face rules.
    .DESCRIPTION
        Returns one object per rule with the declarations as an ordered hashtable, so the
        descriptors Google worked out can be copied over verbatim instead of guessed at.
    #>
    param([Parameter(Mandatory)] [string] $Css)

    $matched = [regex]::Matches($Css, '@font-face\s*\{(?<body>[^}]*)\}')
    if ($matched.Count -eq 0) {
        throw 'The stylesheet held no @font-face rules. Google may have changed its format.'
    }

    foreach ($rule in $matched) {
        $declarations = [ordered]@{}
        foreach ($line in $rule.Groups['body'].Value -split ';') {
            $split = $line.IndexOf(':')
            if ($split -lt 0) { continue }
            $name = $line.Substring(0, $split).Trim()
            if (-not $name) { continue }
            $declarations[$name] = $line.Substring($split + 1).Trim()
        }
        [pscustomobject]@{ Declarations = $declarations }
    }
}

function Get-FontFileName {
    <#
    .SYNOPSIS
        Names a font file after what it holds, so the folder can be read without the stylesheet.
    #>
    param([Parameter(Mandatory)] [string] $Slug, [Parameter(Mandatory)] [hashtable] $Declarations)

    $parts = @($Slug)
    $weight = $Declarations['font-weight']
    if ($weight) { $parts += ($weight -replace '\s+', '-') }
    $stretch = $Declarations['font-stretch']
    if ($stretch) { $parts += ($stretch -replace '%', '' -replace '\s+', '-') + 'w' }
    if ($Declarations['font-style'] -eq 'italic') { $parts += 'italic' }

    ($parts -join '-') + '.woff2'
}

Write-Host 'Importing item card fonts' -ForegroundColor Cyan

New-Item -ItemType Directory -Path $FontDir -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $StylesheetPath) -Force | Out-Null

$faces = [System.Collections.Generic.List[object]]::new()
$provenanceFamilies = [System.Collections.Generic.List[object]]::new()

foreach ($family in $Families) {
    Write-Host "  $($family.Name)" -ForegroundColor DarkGray

    $cssUrl = "https://fonts.googleapis.com/css2?family=$($family.Spec)&display=swap"
    $css = (Invoke-WebRequest -Uri $cssUrl -Headers @{ 'User-Agent' = $BrowserAgent }).Content

    $taken = 0
    foreach ($block in Get-FontFaceBlock -Css $css) {
        $declarations = $block.Declarations
        if (-not $declarations['unicode-range']) { continue }
        if (-not $declarations['unicode-range'].StartsWith($LatinRange)) { continue }

        $source = [regex]::Match($declarations['src'], "url\((?<url>[^)]+)\)")
        if (-not $source.Success) { throw "No font URL in the $($family.Name) rule." }

        $fileName = Get-FontFileName -Slug $family.Slug -Declarations $declarations
        $target = Join-Path $FontDir $fileName

        if ($Refresh -or -not (Test-Path -LiteralPath $target)) {
            Invoke-WebRequest -Uri $source.Groups['url'].Value -OutFile $target
            $signature = [System.Text.Encoding]::ASCII.GetString(
                [System.IO.File]::ReadAllBytes($target)[0..3])
            if ($signature -ne 'wOF2') {
                Remove-Item -LiteralPath $target -Force
                throw "$fileName did not download as woff2."
            }
            Write-Host "    wrote $fileName" -ForegroundColor DarkGray
        }
        else {
            Write-Host "    have $fileName" -ForegroundColor DarkGray
        }

        $declarations['src'] = "url(`"../fonts/$fileName`") format(`"woff2`")"
        $faces.Add($declarations)
        $taken++
    }

    if ($taken -eq 0) { throw "No latin subset came back for $($family.Name)." }

    $licenceFile = "$($family.Slug)-OFL.txt"
    $licenceTarget = Join-Path $FontDir $licenceFile
    if ($Refresh -or -not (Test-Path -LiteralPath $licenceTarget)) {
        Invoke-WebRequest `
            -Uri "https://raw.githubusercontent.com/google/fonts/main/$($family.LicencePath)/OFL.txt" `
            -OutFile $licenceTarget
        Write-Host "    wrote $licenceFile" -ForegroundColor DarkGray
    }

    $copyright = (Get-Content -LiteralPath $licenceTarget -TotalCount 1).Trim()

    $provenanceFamilies.Add([ordered]@{
        family      = $family.Name
        usedFor     = $family.Used
        copyright   = $copyright
        licence     = 'SIL Open Font License 1.1'
        licenceFile = $licenceFile
        licenceUrl  = "https://github.com/google/fonts/blob/main/$($family.LicencePath)/OFL.txt"
        project     = $family.Project
        sourceUrl   = $cssUrl
    })
}

$builder = [System.Text.StringBuilder]::new()
[void]$builder.AppendLine('/*')
[void]$builder.AppendLine(' * Imported file. Generated by tools/Import-CardFonts.ps1; do not edit.')
[void]$builder.AppendLine(' *')
[void]$builder.AppendLine(' * The fonts the item cards are set in, served from this site so that no page view')
[void]$builder.AppendLine(' * reaches a third party and the cards still print with no network at all.')
foreach ($family in $provenanceFamilies) {
    [void]$builder.AppendLine(' *')
    [void]$builder.AppendLine(" * $($family.family) - $($family.licence)")
    [void]$builder.AppendLine(" *   $($family.copyright)")
    [void]$builder.AppendLine(" *   Licence text: fonts/$($family.licenceFile)")
}
[void]$builder.AppendLine(' */')

foreach ($face in $faces) {
    [void]$builder.AppendLine()
    [void]$builder.AppendLine('@font-face {')
    foreach ($name in $face.Keys) {
        [void]$builder.AppendLine("    ${name}: $($face[$name]);")
    }
    [void]$builder.AppendLine('}')
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
[System.IO.File]::WriteAllText($StylesheetPath, $builder.ToString(), $utf8)

$provenance = [ordered]@{
    _source = [ordered]@{
        describes   = 'Web fonts for the item cards'
        generatedBy = 'tools/Import-CardFonts.ps1'
        stylesheet  = 'css/card-fonts.css'
        subset      = 'latin'
        warning     = 'Imported files. Do not edit by hand; re-run the importer instead.'
    }
    families = $provenanceFamilies
}

[System.IO.File]::WriteAllText(
    (Join-Path $FontDir '_source.json'),
    ($provenance | ConvertTo-Json -Depth 6) + "`n",
    $utf8)

Write-Host "Done. Fonts in $FontDir, stylesheet at $StylesheetPath" -ForegroundColor Cyan
