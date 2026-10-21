param(
    [string]$Mode = 'EditMode',
    [string]$Name = 'editmode',
    [string]$Filter = '',
    [string]$ExecuteMethod = '',
    [switch]$Graphics
)
$ErrorActionPreference = 'Stop'
$taskProject = Split-Path $PSScriptRoot -Parent
$taskUnity = 'C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Unity.com'
$taskArtifacts = Join-Path $taskProject 'Artifacts'
New-Item -ItemType Directory -Path $taskArtifacts -Force | Out-Null
$taskArguments = @('-batchmode', '-projectPath', $taskProject, '-logFile', (Join-Path $taskArtifacts ($Name + '.log')))
if (-not $Graphics) { $taskArguments += '-nographics' }
if ($ExecuteMethod) {
    $taskArguments += @('-executeMethod', $ExecuteMethod, '-quit')
} else {
    $taskArguments += @('-runTests', '-testPlatform', $Mode, '-testResults', (Join-Path $taskArtifacts ($Name + '.xml')))
    if ($Filter) { $taskArguments += @('-testFilter', $Filter) }
}
& $taskUnity @taskArguments
$taskExit = $LASTEXITCODE
Select-String -Path (Join-Path $taskArtifacts ($Name + '.log')) -Pattern 'error CS|Aborting batchmode|Test run finished|Application will terminate|executeMethod.*failed' | Select-Object -Last 10 | ForEach-Object { Write-Output $_.Line }
$taskResultPath = Join-Path $taskArtifacts ($Name + '.xml')
if (Test-Path $taskResultPath) {
    [xml]$taskResult = Get-Content -Raw $taskResultPath
    $taskResult.'test-run' | Select-Object result,total,passed,failed,skipped
    $taskResult.SelectNodes('//test-case[@result="Failed"]') | ForEach-Object { Write-Output $_.fullname; Write-Output $_.failure.message }
}
exit $taskExit
