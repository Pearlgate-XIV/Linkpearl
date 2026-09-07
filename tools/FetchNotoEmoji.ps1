param(
    [string]$OutDir = ""
)

$ErrorActionPreference = "Continue"
$root = Split-Path -Parent $PSScriptRoot
if ($OutDir.Length -eq 0) {
    $OutDir = Join-Path $root "src\Linkpearl.Host\Icons\emoji\noto"
}

New-Item -ItemType Directory -Force -Path $OutDir | Out-Null

function Test-Emoji([string]$glyph) {
    if ([string]::IsNullOrEmpty($glyph)) { return $false }
    $index = 0
    while ($index -lt $glyph.Length) {
        $cp = [char]::ConvertToUtf32($glyph, $index)
        if ($cp -eq 0x20E3 -or $cp -eq 0x200D -or $cp -eq 0xFE0F) { }
        elseif ($cp -ge 0x1F1E6 -and $cp -le 0x1FAFF) { return $true }
        elseif ($cp -ge 0x2300 -and $cp -le 0x27BF) { return $true }
        elseif ($cp -ge 0x2B00 -and $cp -le 0x2BFF) { return $true }
        elseif ($cp -eq 0x3030 -or $cp -eq 0x303D -or $cp -eq 0x3297 -or $cp -eq 0x3299) { return $true }
        elseif ($cp -eq 0x00A9 -or $cp -eq 0x00AE -or $cp -eq 0x2122) { return $true }
        if ($cp -gt 0xFFFF) { $index += 2 } else { $index += 1 }
    }
    return $false
}

function Get-Stems([string]$glyph) {
    $skip = New-Object System.Collections.Generic.List[string]
    $keep = New-Object System.Collections.Generic.List[string]
    $index = 0
    while ($index -lt $glyph.Length) {
        $cp = [char]::ConvertToUtf32($glyph, $index)
        $hex = ("{0:x}" -f $cp)
        [void]$keep.Add($hex)
        if ($cp -ne 0xFE0F) { [void]$skip.Add($hex) }
        if ($cp -gt 0xFFFF) { $index += 2 } else { $index += 1 }
    }
    $stems = New-Object System.Collections.Generic.List[string]
    if ($skip.Count -gt 0) { [void]$stems.Add("emoji_u" + ($skip -join "_")) }
    $withVs = "emoji_u" + ($keep -join "_")
    if ($stems -notcontains $withVs) { [void]$stems.Add($withVs) }
    return $stems
}

function Add-Glyph([System.Collections.Generic.HashSet[string]]$set, [string]$glyph) {
    if (Test-Emoji $glyph) { [void]$set.Add($glyph) }
}

$glyphs = New-Object "System.Collections.Generic.HashSet[string]"
$shelfPath = Join-Path $root "src\Linkpearl.Abstractions\Emoji\EmojiShelf.cs"
$shelf = [System.IO.File]::ReadAllText($shelfPath, [System.Text.Encoding]::UTF8)
foreach ($hit in [regex]::Matches($shelf, '"([^"]+)"')) {
    $token = ($hit.Groups[1].Value -split ' ', 2)[0]
    Add-Glyph $glyphs $token
}

$tones = @(
    [char]::ConvertFromUtf32(0x1F3FB),
    [char]::ConvertFromUtf32(0x1F3FC),
    [char]::ConvertFromUtf32(0x1F3FD),
    [char]::ConvertFromUtf32(0x1F3FE),
    [char]::ConvertFromUtf32(0x1F3FF)
)
foreach ($glyph in @($glyphs.ToArray())) {
    if ($glyph.Length -gt 4) { continue }
    $index = 0
    $hand = $false
    while ($index -lt $glyph.Length) {
        $cp = [char]::ConvertToUtf32($glyph, $index)
        if ($cp -ge 0x1F446 -and $cp -le 0x1F450) { $hand = $true }
        if ($cp -ge 0x1F590 -and $cp -le 0x1F64F) { $hand = $true }
        if ($cp -ge 0x270A -and $cp -le 0x270D) { $hand = $true }
        if ($cp -eq 0x261D -or $cp -eq 0x1F91D -or $cp -eq 0x1F44C -or $cp -eq 0x1F90C -or $cp -eq 0x1F90F) { $hand = $true }
        if ($cp -gt 0xFFFF) { $index += 2 } else { $index += 1 }
    }
    if (-not $hand) { continue }
    foreach ($tone in $tones) { Add-Glyph $glyphs ($glyph + $tone) }
}

Add-Glyph $glyphs "😀"
Write-Output "glyphs=$($glyphs.Count)"

$baseUrl = "https://raw.githubusercontent.com/googlefonts/noto-emoji/main/png/72"
$ok = 0
$miss = 0
$skip = 0
$n = 0
foreach ($glyph in $glyphs) {
    $n++
    $stems = @(Get-Stems $glyph)
    $got = $false
    foreach ($stem in $stems) {
        $dest = Join-Path $OutDir ($stem + ".png")
        if (Test-Path $dest) {
            $got = $true
            $skip++
            break
        }
        $url = "$baseUrl/$stem.png"
        try {
            Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing -TimeoutSec 30 | Out-Null
            if ((Test-Path $dest) -and (Get-Item $dest).Length -gt 80) {
                $got = $true
                $ok++
                break
            }
            Remove-Item $dest -Force -ErrorAction SilentlyContinue
        }
        catch {
            Remove-Item $dest -Force -ErrorAction SilentlyContinue
        }
    }
    if (-not $got) {
        $miss++
        if ($miss -le 12) {
            Write-Output ("miss " + ($stems -join ","))
        }
    }
    if ($n % 100 -eq 0) { Write-Output "progress $n/$($glyphs.Count) ok=$ok miss=$miss" }
}

Write-Output "downloaded=$ok cached=$skip missing=$miss dest=$OutDir"
