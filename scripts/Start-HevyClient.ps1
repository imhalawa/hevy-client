[CmdletBinding()]
param(
  [Parameter(Mandatory = $true)]
  [ValidateScript({ Test-Path -LiteralPath $_ -PathType Leaf })]
  [string] $ClientPath
)

$secureKey = Read-Host -Prompt 'Hevy API key' -AsSecureString
$credential = [System.Management.Automation.PSCredential]::new('hevy-mcp', $secureKey)
$plainKey = $credential.GetNetworkCredential().Password
if ([string]::IsNullOrWhiteSpace($plainKey)) {
  throw 'A non-empty Hevy API key is required.'
}

$previousKey = [Environment]::GetEnvironmentVariable('HEVY_API_KEY', 'Process')
$childExitCode = 0
try {
  [Environment]::SetEnvironmentVariable('HEVY_API_KEY', $plainKey, 'Process')
  & $ClientPath
  if ($null -ne $LASTEXITCODE) {
    $childExitCode = $LASTEXITCODE
  }
}
finally {
  [Environment]::SetEnvironmentVariable('HEVY_API_KEY', $previousKey, 'Process')
  $plainKey = $null
  $credential = $null
  $secureKey = $null
}

exit $childExitCode
