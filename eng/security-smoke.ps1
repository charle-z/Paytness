$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
$bin = Join-Path $root 'src/Paytness/bin/Release/net10.0/paytness.dll'
$tmp = Join-Path ([IO.Path]::GetTempPath()) ("paytness-security-" + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $tmp | Out-Null

function Expect-Invalid([string]$Path, [string]$Expected) {
    $output = (& dotnet $bin validate $Path 2>&1 | Out-String)
    $code = $LASTEXITCODE
    if ($code -ne 2) { throw "Expected exit 2 for $Path, got $code`: $output" }
    if (-not $output.Contains($Expected, [StringComparison]::Ordinal)) { throw "Expected '$Expected' for $Path, got: $output" }
}

try {
    @'
version: 1
id: &scenario anchored
contract: contract.yaml
provider:
  responseModes: [deliver]
'@ | Set-Content -Path (Join-Path $tmp 'anchor.yaml') -NoNewline
    Expect-Invalid (Join-Path $tmp 'anchor.yaml') 'YAML anchors are not allowed.'

    @'
version: 1
id: *missing
contract: contract.yaml
provider:
  responseModes: [deliver]
'@ | Set-Content -Path (Join-Path $tmp 'alias.yaml') -NoNewline
    Expect-Invalid (Join-Path $tmp 'alias.yaml') 'YAML aliases are not allowed.'

    @'
version: 1
id: !custom tagged
contract: contract.yaml
provider:
  responseModes: [deliver]
'@ | Set-Content -Path (Join-Path $tmp 'tag.yaml') -NoNewline
    Expect-Invalid (Join-Path $tmp 'tag.yaml') 'Custom YAML tags are not allowed.'

    @'
version: 1
id: merge
"<<": value
contract: contract.yaml
provider:
  responseModes: [deliver]
'@ | Set-Content -Path (Join-Path $tmp 'merge.yaml') -NoNewline
    Expect-Invalid (Join-Path $tmp 'merge.yaml') 'YAML merge keys are not allowed.'

    '{"version":1,"id":"first","id":"second","contract":"contract.json"}' | Set-Content -Path (Join-Path $tmp 'duplicate.json') -NoNewline
    Expect-Invalid (Join-Path $tmp 'duplicate.json') "JSON contains duplicate property 'id'."

    $nested = 'value'
    1..33 | ForEach-Object { $nested = "[$nested]" }
    "version: 1`nid: $nested`ncontract: contract.yaml`nprovider:`n  responseModes: [deliver]`n" | Set-Content -Path (Join-Path $tmp 'deep.yaml') -NoNewline
    Expect-Invalid (Join-Path $tmp 'deep.yaml') 'YAML nesting exceeds depth 32.'

    ('[' + ((1..10001 | ForEach-Object { '0' }) -join ',') + ']') | Set-Content -Path (Join-Path $tmp 'nodes.json') -NoNewline
    Expect-Invalid (Join-Path $tmp 'nodes.json') 'JSON exceeds 10000 nodes.'

    $rootDir = Join-Path $tmp 'root'
    $outsideDir = Join-Path $tmp 'outside'
    $contractsDir = Join-Path $rootDir 'contracts'
    New-Item -ItemType Directory -Force -Path $contractsDir, $outsideDir | Out-Null
    $outsideContract = Join-Path $outsideDir 'basic.yaml'
    'version: 1' | Set-Content -Path $outsideContract
    try {
        New-Item -ItemType SymbolicLink -Path (Join-Path $contractsDir 'basic.yaml') -Target $outsideContract -ErrorAction Stop | Out-Null
        "version: 1`nid: symlink`ncontract: contracts/basic.yaml`nprovider:`n  responseModes: [deliver]`n" | Set-Content -Path (Join-Path $rootDir 'scenario.yaml') -NoNewline
        Expect-Invalid (Join-Path $rootDir 'scenario.yaml') 'Contract path escapes the scenario root.'
    }
    catch {
        Write-Warning 'Symlink containment smoke skipped because this Windows environment cannot create symbolic links.'
    }

    Write-Host 'Security smoke passed.'
}
finally {
    Remove-Item -Recurse -Force $tmp -ErrorAction SilentlyContinue
}
