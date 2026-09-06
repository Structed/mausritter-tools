<#
.SYNOPSIS
    Imports the Mausritter SRD tables into the JSON data files the web app ships.

.DESCRIPTION
    The Mausritter SRD is published as clean GitHub-flavoured markdown, with the dice notation
    carried in each table's header cell. That makes it a far better source than hand-transcription:
    we get the authoritative wording, a version we can cite, and a repeatable path to re-import when
    the SRD is updated.

    The SRD text is licensed CC BY 4.0, so the imported tables may be redistributed as long as the
    attribution recorded in each output file is displayed prominently in the app.

    Output is written to src/MausritterTools.Web/wwwroot/data/srd/. Those files are generated; edit
    this script and re-run it rather than editing them by hand.

.PARAMETER SrdVersion
    The SRD version to record in the provenance block of each output file.

.PARAMETER CachePath
    Directory used to cache the downloaded markdown. Re-runs reuse the cache unless -Refresh is set.

.PARAMETER Refresh
    Force a re-download even when cached markdown is present.

.EXAMPLE
    pwsh ./tools/Import-SrdTables.ps1
    pwsh ./tools/Import-SrdTables.ps1 -Refresh
#>
[CmdletBinding()]
param(
    [string] $SrdVersion = '2.3.1',
    [string] $CachePath = (Join-Path ([System.IO.Path]::GetTempPath()) 'mausritter-srd-cache'),
    [switch] $Refresh
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$OutputDir = Join-Path $RepoRoot 'src/MausritterTools.Web/wwwroot/data/srd'
$SourceBase = 'https://raw.githubusercontent.com/isaacwilliams/mausritter-web/master/content/srd-markdown'

$SourceFiles = @(
    '11-useful-tables.md'
    '14-magic.md'
    '15-recruiting-help.md'
    '16-gear-and-prices.md'
    '22-hexcrawl-toolbox.md'
)

#region Markdown parsing

function Get-SrdMarkdown {
    <#  Downloads a SRD markdown file, caching it between runs.  #>
    param([Parameter(Mandatory)] [string] $FileName)

    $cached = Join-Path $CachePath $FileName
    if ($Refresh -or -not (Test-Path $cached)) {
        New-Item -ItemType Directory -Path $CachePath -Force | Out-Null
        Write-Verbose "Downloading $FileName"
        Invoke-WebRequest -Uri "$SourceBase/$FileName" -OutFile $cached -UseBasicParsing
    }

    return Get-Content -LiteralPath $cached -Encoding utf8
}

function Clear-Markdown {
    <#  Strips the bold markers the gear tables use around item names.  #>
    param([string] $Text)

    if ($null -eq $Text) { return '' }
    return ($Text -replace '\*\*', '').Trim()
}

function Split-TableRow {
    <#  Splits a GFM table row into trimmed cells, dropping the leading and trailing pipes.  #>
    param([Parameter(Mandatory)] [AllowEmptyString()] [string] $Line)

    $trimmed = $Line.Trim()
    if ($trimmed.StartsWith('|')) { $trimmed = $trimmed.Substring(1) }
    if ($trimmed.EndsWith('|')) { $trimmed = $trimmed.Substring(0, $trimmed.Length - 1) }

    return @($trimmed -split '\|' | ForEach-Object { $_.Trim() })
}

function Get-MarkdownTables {
    <#
        Extracts every GFM table from a markdown document, tagging each with the nearest preceding
        heading. Tables are matched by heading rather than by column name because the SRD reuses
        column labels: the governance table's second column is confusingly headed "Size".
    #>
    # AllowEmptyString is required because Mandatory otherwise rejects the blank elements that
    # every empty line in the markdown produces.
    param([Parameter(Mandatory)] [AllowEmptyString()] [string[]] $Lines)

    $tables = @()
    $heading = ''

    for ($i = 0; $i -lt $Lines.Count; $i++) {
        $line = $Lines[$i]

        if ($line -match '^#{1,6}\s+(.*)$') {
            $heading = $Matches[1].Trim()
            continue
        }

        $isHeaderRow = $line -match '^\s*\|' -and
                       ($i + 1) -lt $Lines.Count -and
                       $Lines[$i + 1] -match '^\s*\|[\s\-:|]+\|\s*$'

        if (-not $isHeaderRow) { continue }

        $headers = Split-TableRow -Line $line
        $rows = @()
        $r = $i + 2

        while ($r -lt $Lines.Count -and $Lines[$r] -match '^\s*\|') {
            $rows += , (Split-TableRow -Line $Lines[$r])
            $r++
        }

        $tables += [pscustomobject]@{
            Heading = $heading
            Headers = $headers
            Rows    = $rows
        }

        $i = $r - 1
    }

    return $tables
}

function Get-TableByHeading {
    <#
        Finds a table by the heading it sits under. Several candidate headings may be supplied,
        because the SRD nests some tables one level deeper than others: the hireling table lives
        under "Recruiting hirelings" rather than the "Hirelings" section that introduces it.
    #>
    param(
        [Parameter(Mandatory)] [object[]] $Tables,
        [Parameter(Mandatory)] [string[]] $Heading,
        [int] $Index = 0
    )

    foreach ($candidate in $Heading) {
        $matching = @($Tables | Where-Object { $_.Heading -eq $candidate })
        if ($matching.Count -gt $Index) {
            return $matching[$Index]
        }
    }

    throw "Expected a table under heading '$($Heading -join "' or '")' (index $Index) but found none. The SRD layout may have changed."
}

function Get-Column {
    <#  Returns one column of a table as a plain string array, skipping blank cells.  #>
    param(
        [Parameter(Mandatory)] [object] $Table,
        [Parameter(Mandatory)] [int] $ColumnIndex
    )

    return @(
        $Table.Rows |
            Where-Object { $_.Count -gt $ColumnIndex -and $_[$ColumnIndex] -ne '' } |
            ForEach-Object { Clear-Markdown $_[$ColumnIndex] }
    )
}

#endregion

#region Value parsing

function ConvertTo-RollRange {
    <#
        Parses the first column of a SRD table, which may be a single value ("7"), an inclusive
        range ("10-11") or an open upper bound ("12+").
    #>
    param([Parameter(Mandatory)] [AllowEmptyString()] [string] $Text, [int] $OpenEndedMax = 99)

    $value = $Text.Trim()

    if ($value -match '^(\d+)\s*-\s*(\d+)$') {
        return [pscustomobject]@{ Min = [int]$Matches[1]; Max = [int]$Matches[2] }
    }

    if ($value -match '^(\d+)\s*\+$') {
        return [pscustomobject]@{ Min = [int]$Matches[1]; Max = $OpenEndedMax }
    }

    if ($value -match '^(\d+)$') {
        return [pscustomobject]@{ Min = [int]$Matches[1]; Max = [int]$Matches[1] }
    }

    throw "Could not parse '$Text' as a roll or roll range."
}

function ConvertTo-GearItem {
    <#
        Splits a gear row into its parts. Names are bolded, with an optional trailing qualifier
        after a comma ("**Book**, blank") or a parenthetical note ("**Light** (dagger, needle)").

        Prices are mostly plain pips ("10p") but the SRD also uses multipliers ("x10p" for silvered
        weapons), percentages ("10%" for repairs) and per-unit rates ("1p/night"). Only plain pip
        values get a numeric `pips`; everything else keeps its text so the UI can show it verbatim
        rather than inventing a number.
    #>
    param(
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $ItemText,
        [Parameter(Mandatory)] [AllowEmptyString()] [string] $PriceText
    )

    $raw = $ItemText.Trim()
    $note = $null

    if ($raw -match '^(.*?)\s*\(([^)]*)\)\s*$') {
        $raw = $Matches[1].Trim()
        $note = $Matches[2].Trim()
    }

    $name = Clear-Markdown $raw
    $qualifier = $null

    if ($name -match '^(.*?),\s*(.*)$') {
        $name = $Matches[1].Trim()
        $qualifier = $Matches[2].Trim()
    }

    $price = $PriceText.Trim()
    $pips = $null
    $unit = $null

    if ($price -match '^(\d+)p$') {
        $pips = [int]$Matches[1]
    }
    elseif ($price -match '^(\d+)p\s*/\s*(.+)$') {
        $pips = [int]$Matches[1]
        $unit = $Matches[2].Trim()
    }

    $item = [ordered]@{ name = $name }
    if ($qualifier) { $item.qualifier = $qualifier }
    if ($note) { $item.note = $note }
    $item.priceText = $price
    if ($null -ne $pips) { $item.pips = $pips }
    if ($unit) { $item.perUnit = $unit }

    return $item
}

#endregion

function New-Provenance {
    param([Parameter(Mandatory)] [string] $Describes, [Parameter(Mandatory)] [string[]] $Files)

    return [ordered]@{
        describes   = $Describes
        work        = 'Mausritter System Reference Document'
        version     = $SrdVersion
        url         = 'https://mausritter.com/srd/'
        sourceFiles = $Files
        licence     = 'CC BY 4.0'
        licenceUrl  = 'https://creativecommons.org/licenses/by/4.0/'
        attribution = 'This work is based on Mausritter (https://mausritter.com), a product of Losing Games and Isaac Williams, and is licensed for use under the Creative Commons Attribution 4.0 International (CC BY 4.0) licence.'
        generatedBy = 'tools/Import-SrdTables.ps1'
        warning     = 'Generated file. Do not edit by hand; re-run the importer instead.'
    }
}

function Write-DataFile {
    param([Parameter(Mandatory)] [string] $Name, [Parameter(Mandatory)] [object] $Data)

    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    $path = Join-Path $OutputDir $Name

    # -Depth is generous because the nested table structures are several levels deep.
    $json = $Data | ConvertTo-Json -Depth 12

    # UTF-8 without BOM: the app fetches these over HTTP and a BOM upsets strict JSON parsers.
    [System.IO.File]::WriteAllText($path, $json + "`n", [System.Text.UTF8Encoding]::new($false))
    Write-Host "  wrote $Name" -ForegroundColor DarkGray
}

Write-Host 'Importing Mausritter SRD tables' -ForegroundColor Cyan
Write-Host "  SRD version $SrdVersion" -ForegroundColor DarkGray

$documents = @{}
foreach ($file in $SourceFiles) {
    $documents[$file] = Get-MarkdownTables -Lines (Get-SrdMarkdown -FileName $file)
}

$hexcrawl = $documents['22-hexcrawl-toolbox.md']
$useful = $documents['11-useful-tables.md']
$gear = $documents['16-gear-and-prices.md']
$recruiting = $documents['15-recruiting-help.md']
$magic = $documents['14-magic.md']

#region settlement.json

$sizeTable = Get-TableByHeading -Tables $hexcrawl -Heading 'Settlement size'
$sizes = @()
foreach ($row in $sizeTable.Rows) {
    $roll = [int]($row[0].Trim())
    $text = Clear-Markdown $row[1]

    # Entries read "Farm/manor (1-3 families)": split the label from the population note.
    $name = $text
    $population = $null
    if ($text -match '^(.*?)\s*\(([^)]*)\)\s*$') {
        $name = $Matches[1].Trim()
        $population = $Matches[2].Trim()
    }

    # These three flags encode SRD prose as data:
    #   "Hamlets and larger settlements will furnish a friendly tavern or inn"  -> roll >= 3
    #   "What feature sets this settlement apart? Cities have two."             -> roll 6 gets 2
    #   "What trade do the mice work? Towns and cities have two."               -> roll >= 5 gets 2
    $sizes += [ordered]@{
        roll          = $roll
        sizeValue     = $roll
        name          = $name
        population    = $population
        hasTavern     = ($roll -ge 3)
        featureCount  = $(if ($roll -ge 6) { 2 } else { 1 })
        industryCount = $(if ($roll -ge 5) { 2 } else { 1 })
    }
}

$governanceTable = Get-TableByHeading -Tables $hexcrawl -Heading 'Governance'
$governance = @()
foreach ($row in $governanceTable.Rows) {
    $range = ConvertTo-RollRange -Text $row[0] -OpenEndedMax 12
    $governance += [ordered]@{
        rollMin = $range.Min
        rollMax = $range.Max
        text    = Clear-Markdown $row[1]
    }
}

$detailTables = @($hexcrawl | Where-Object { $_.Heading -eq 'Settlement details' })
if ($detailTables.Count -ne 4) {
    throw "Expected 4 tables under 'Settlement details' (inhabitants, notable feature, industry, event) but found $($detailTables.Count)."
}

$nameSeedTable = Get-TableByHeading -Tables $hexcrawl -Heading 'Settlement name seeds'
$tavernTable = Get-TableByHeading -Tables $hexcrawl -Heading 'Taverns and inns'

Write-DataFile -Name 'settlement.json' -Data ([ordered]@{
    _source         = New-Provenance -Describes 'Mouse settlement tables' -Files @('22-hexcrawl-toolbox.md')
    sizeRoll        = '2d6, use the lowest value'
    sizes           = $sizes
    governanceRoll  = 'd6 + settlement size'
    governance      = $governance
    inhabitants     = Get-Column -Table $detailTables[0] -ColumnIndex 1
    notableFeatures = Get-Column -Table $detailTables[1] -ColumnIndex 1
    industries      = Get-Column -Table $detailTables[2] -ColumnIndex 1
    events          = Get-Column -Table $detailTables[3] -ColumnIndex 1
    nameSeeds       = [ordered]@{
        note   = 'Roll d12 twice. Choose a start and an end. Massage until it sounds nice.'
        startA = Get-Column -Table $nameSeedTable -ColumnIndex 1
        startB = Get-Column -Table $nameSeedTable -ColumnIndex 2
        endA   = Get-Column -Table $nameSeedTable -ColumnIndex 3
        endB   = Get-Column -Table $nameSeedTable -ColumnIndex 4
    }
    taverns         = [ordered]@{
        note           = 'Hamlets and larger settlements will furnish a friendly tavern or inn.'
        nameA          = Get-Column -Table $tavernTable -ColumnIndex 1
        nameB          = Get-Column -Table $tavernTable -ColumnIndex 2
        specialtyMeals = Get-Column -Table $tavernTable -ColumnIndex 3
    }
})

#endregion

#region npc.json

$socialTable = Get-TableByHeading -Tables $useful -Heading 'Social position'
$socialPositions = @()
foreach ($row in $socialTable.Rows) {
    $socialPositions += [ordered]@{
        roll    = [int]($row[0].Trim())
        name    = Clear-Markdown $row[1]
        payment = Clear-Markdown $row[2]
    }
}

$birthsignTable = Get-TableByHeading -Tables $useful -Heading 'Birthsign'
$birthsigns = @()
foreach ($row in $birthsignTable.Rows) {
    $disposition = Clear-Markdown $row[2]
    $parts = @($disposition -split '/' | ForEach-Object { $_.Trim() })

    $birthsigns += [ordered]@{
        roll        = [int]($row[0].Trim())
        name        = Clear-Markdown $row[1]
        disposition = $disposition
        virtue      = $parts[0]
        vice        = $(if ($parts.Count -gt 1) { $parts[1] } else { $null })
    }
}

$detailsTable = Get-TableByHeading -Tables $useful -Heading 'Details'

Write-DataFile -Name 'npc.json' -Data ([ordered]@{
    _source         = New-Provenance -Describes 'Non-player mice tables' -Files @('11-useful-tables.md')
    socialPositions = $socialPositions
    birthsigns      = $birthsigns
    appearance      = Get-Column -Table $detailsTable -ColumnIndex 1
    quirk           = Get-Column -Table $detailsTable -ColumnIndex 2
    wants           = Get-Column -Table $detailsTable -ColumnIndex 3
    relationship    = Get-Column -Table $detailsTable -ColumnIndex 4
})

#endregion

#region gear.json

# Heading -> availability note, taken from the prose directly beneath each heading in the SRD.
$gearCategories = [ordered]@{
    'Tools, mouse made'  = 'Available in most mouse settlements. These items are mouse-sized.'
    'Tools, human made'  = 'Available in mouse settlements near human populations.'
    'Weapons and armour' = ''
    'Light sources'      = ''
    'Clothing'           = ''
    'Lodging and food'   = ''
    'Transport hire'     = 'Prices are per mouse, per hex.'
    'Hired help'         = 'Prices are per day, and do not include food and shelter.'
}

$categories = @()
foreach ($heading in $gearCategories.Keys) {
    $table = Get-TableByHeading -Tables $gear -Heading $heading

    $items = @()
    foreach ($row in $table.Rows) {
        $items += ConvertTo-GearItem -ItemText $row[0] -PriceText $row[1]
    }

    $category = [ordered]@{
        id    = ($heading.ToLowerInvariant() -replace '[^a-z0-9]+', '-').Trim('-')
        name  = $heading
        items = $items
    }
    if ($gearCategories[$heading] -ne '') { $category.availability = $gearCategories[$heading] }

    $categories += $category
}

Write-DataFile -Name 'gear.json' -Data ([ordered]@{
    _source    = New-Provenance -Describes 'Gear and prices' -Files @('16-gear-and-prices.md')
    currency   = [ordered]@{
        name         = 'pip'
        abbreviation = 'p'
        note         = 'Pips are the only currency in Mausritter. A pip purse holds 250 pips; that is a carrying limit, not a denomination.'
    }
    categories = $categories
})

#endregion

#region hirelings.json

$hirelingTable = Get-TableByHeading -Tables $recruiting -Heading @('Recruiting hirelings', 'Hirelings')
$hirelings = @()
foreach ($row in $hirelingTable.Rows) {
    $wages = $row[2].Trim()
    $pips = $null
    if ($wages -match '^(\d+)p$') { $pips = [int]$Matches[1] }

    $entry = [ordered]@{
        name      = Clear-Markdown $row[0]
        number    = Clear-Markdown $row[1]
        wagesText = $wages
    }
    if ($null -ne $pips) { $entry.wagesPips = $pips }

    $hirelings += $entry
}

Write-DataFile -Name 'hirelings.json' -Data ([ordered]@{
    _source    = New-Provenance -Describes 'Hirelings' -Files @('15-recruiting-help.md')
    recruiting = 'Spend a day asking around. Make a WIL save or pay 20p. If successful, roll the Number for that type of hireling.'
    note       = 'The SRD states that settlement size determines which hirelings are available, but never gives the mapping. The gating used by this app is a house rule.'
    hirelings  = $hirelings
})

#endregion

#region spells.json

$spellTable = Get-TableByHeading -Tables $magic -Heading 'List of spells'
$spells = @()
foreach ($row in $spellTable.Rows) {
    $range = ConvertTo-RollRange -Text $row[0] -OpenEndedMax 16
    $spells += [ordered]@{
        rollMin  = $range.Min
        rollMax  = $range.Max
        name     = Clear-Markdown $row[1]
        effect   = Clear-Markdown $row[2]
        recharge = Clear-Markdown $row[3]
    }
}

Write-DataFile -Name 'spells.json' -Data ([ordered]@{
    _source   = New-Provenance -Describes 'Spells' -Files @('14-magic.md')
    roll      = '2d8'
    saleValue = 'A fully charged spell can usually be sold for d6 x 100p in a settlement. A depleted spell is worth half.'
    spells    = $spells
})

#endregion

Write-Host "Done. Output in $OutputDir" -ForegroundColor Green
