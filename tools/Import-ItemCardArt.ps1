<#
.SYNOPSIS
    Imports the official Mausritter item card illustrations the app draws on its item cards.

.DESCRIPTION
    The Mausritter Third Party Licence grants, by name, permission to "use, copy and modify the
    item card templates and item card art". These are the drawings shipped with the official Item
    Card Studio, fetched from the Mausritter website's own repository.

    Only the drawings that match an item in the SRD gear list are taken. The list below must stay
    in step with ItemCard.ArtByName in src/MausritterTools.Core/Model/ItemCard.cs; ItemCardTests
    fails if the card rules ask for a file that is not here.

    Output is written to src/MausritterTools.Web/wwwroot/images/items/. Those files are imported;
    re-run this script rather than adding drawings by hand.

.PARAMETER Refresh
    Re-download files that are already present.

.EXAMPLE
    pwsh ./tools/Import-ItemCardArt.ps1
    pwsh ./tools/Import-ItemCardArt.ps1 -Refresh
#>
#requires -Version 7.0
[CmdletBinding()]
param([switch] $Refresh)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RepoRoot = Split-Path -Parent $PSScriptRoot
$OutputDir = Join-Path $RepoRoot 'src/MausritterTools.Web/wwwroot/images/items'
$SourceBase = 'https://raw.githubusercontent.com/isaacwilliams/mausritter-web/master/src/components/generators/customItem/images'

# Drawing -> the gear item it illustrates. The comment is the name the official studio lists it
# under, which is not always the SRD's name for the item class it belongs to.
$Artwork = [ordered]@{
    'item-improvised.png'      = 'Improvised'      # Branch
    'item-light-1.png'         = 'Light'           # Dagger
    'item-light-2.png'         = 'Needle'
    'item-medium-2.png'        = 'Medium'          # Sword
    'item-heavy-2.png'         = 'Heavy'           # Spear
    'item-light-ranged.png'    = 'Light ranged'    # Sling
    'item-heavy-ranged.png'    = 'Heavy ranged'    # Bow
    'item-quiver.png'          = 'Arrows'
    'item-stones.png'          = 'Stones'
    'item-light-armour.png'    = 'Light armour'
    'item-heavy-armour.png'    = 'Heavy armour'
    'item-torch.png'           = 'Torches'
    'item-lantern.png'         = 'Lantern'
    'item-electric-lantern.png' = 'Electric lantern'
    'item-rations.png'         = 'Travel rations'
}

Write-Host 'Importing Mausritter item card art' -ForegroundColor Cyan

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

foreach ($file in $Artwork.Keys) {
    $target = Join-Path $OutputDir $file

    if (-not $Refresh -and (Test-Path -LiteralPath $target)) {
        Write-Host "  have $file" -ForegroundColor DarkGray
        continue
    }

    Invoke-WebRequest -Uri "$SourceBase/$file" -OutFile $target

    $header = [System.IO.File]::ReadAllBytes($target)[0..7]
    if (Compare-Object $header @(137, 80, 78, 71, 13, 10, 26, 10)) {
        Remove-Item -LiteralPath $target -Force
        throw "$file did not download as a PNG. The upstream layout may have changed."
    }

    Write-Host "  wrote $file" -ForegroundColor DarkGray
}

$provenance = [ordered]@{
    _source = [ordered]@{
        describes   = 'Official Mausritter item card illustrations'
        work        = 'Mausritter Item Card Studio'
        url         = 'https://mausritter.com/item-card-studio/'
        sourceUrl   = $SourceBase
        licence     = 'Mausritter Third Party Licence'
        licenceUrl  = 'https://mausritter.com/third-party-licence/'
        permission  = 'The licence permits, by name, use, copying and modification of the item card templates and item card art.'
        attribution = 'Mausritter is copyright Losing Games. Mausritter Tools is an independent production by the mausritter-tools contributors and is not affiliated with Losing Games. It is published under the Mausritter Third Party Licence.'
        generatedBy = 'tools/Import-ItemCardArt.ps1'
        warning     = 'Imported files. Do not add or edit drawings by hand; re-run the importer instead.'
    }
    illustrates = $Artwork
}

$json = $provenance | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText(
    (Join-Path $OutputDir '_source.json'), $json + "`n", [System.Text.UTF8Encoding]::new($false))

Write-Host "Done. Output in $OutputDir" -ForegroundColor Cyan
