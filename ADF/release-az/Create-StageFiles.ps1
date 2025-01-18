param (
    [String]$Environment = 'D1',
    [String]$App = 'BOT',
    [Parameter(Mandatory)]
    [String]$OrgName,
    [String]$Location,
    [String]$ComponentName = 'PsiBot',
    [String]$MetaDataFileName = 'componentBuild.json'
)

Write-Output "$('-'*50)"
Write-Output $MyInvocation.MyCommand.Source

# Get repository root and set up paths
$repoRoot = git rev-parse --show-toplevel
if (!$repoRoot) {
    $repoRoot = Resolve-Path "$PSScriptRoot\..\.."
}
$workflowsPath = Join-Path $repoRoot ".github\workflows"

# Get location and prefix
$LocationLookup = Get-Content -Path $PSScriptRoot\..\bicep\global\region.json | ConvertFrom-Json
$Prefix = $LocationLookup.$Location.Prefix
$BranchName = git branch --show-current ? git branch --show-current : "main"

# Create .github/workflows directory if it doesn't exist
if (!(Test-Path -Path $workflowsPath)) {
    New-Item -Path $workflowsPath -ItemType Directory -Force | Out-Null
    Write-Verbose "Created workflows directory: $workflowsPath" -Verbose
}

$filestocopy = @(
    @{
        SourcePath      = "$PSScriptRoot\..\templates\azuredeploy-OrgName.parameters.json"
        DestinationPath = "$workflowsPath\azuredeploy-${OrgName}.parameters.json"
        TokenstoReplace = @(
            @{ Name = '{OrgName}'; Value = $OrgName },
            @{ Name = '{Location}'; Value = $Location },
            @{ Name = '{BranchName}'; Value = $BranchName }
        )
    }
    @{
        SourcePath      = "$PSScriptRoot\..\templates\PsiBot-build-OrgName.yml"
        DestinationPath = "$workflowsPath\PsiBot-build-${OrgName}.yml"
        TokenstoReplace = @(
            @{ Name = '{OrgName}'; Value = $OrgName },
            @{ Name = '{Location}'; Value = $Location },
            @{ Name = '{BranchName}'; Value = $BranchName }
        )
    }
    @{
        SourcePath      = "$PSScriptRoot\..\templates\PsiBot-infra-OrgName.yml"
        DestinationPath = "$workflowsPath\PsiBot-infra-${OrgName}.yml"
        TokenstoReplace = @(
            @{ Name = '{OrgName}'; Value = $OrgName },
            @{ Name = '{Location}'; Value = $Location },
            @{ Name = '{BranchName}'; Value = $BranchName }
        )
    }
)

$filestocopy | Foreach {
    try {
        # Verify source exists
        if (!(Test-Path -Path $_.SourcePath)) {
            Write-Error "Source file not found: $($_.SourcePath)"
            continue
        }

        # Copy file if it doesn't exist
        if (!(Test-Path -Path $_.DestinationPath)) {
            Copy-Item -Path $_.SourcePath -Destination $_.DestinationPath -Force
            Write-Verbose "Copied file to: $($_.DestinationPath)" -Verbose
        }

        # Replace tokens
        $content = Get-Content -Path $_.DestinationPath -Raw
        $_.TokenstoReplace | ForEach-Object {
            if ($_.Name -and $content -match [regex]::Escape($_.Name)) {
                $content = $content -replace [regex]::Escape($_.Name), $_.Value
            }
        }
        Set-Content -Path $_.DestinationPath -Value $content -Force
    }
    catch {
        Write-Error "Failed processing file $($_.SourcePath): $_"
    }
}

# Stage meta data file on storage
[String]$SAName = "${Prefix}${OrgName}${App}${Environment}saglobal".tolower()
try {
    $Context = New-AzStorageContext -StorageAccountName $SAName -UseConnectedAccount
    [String]$ContainerName = 'builds'
    $StorageContainerParams = @{
        Container = $ContainerName
        Context   = $Context
    }

    Write-Verbose -Message "Global SAName:`t`t [$SAName] Container is: [$ContainerName]" -Verbose

    # Create container if needed
    if (!(Get-AzStorageContainer @StorageContainerParams -EA 0)) {
        New-AzStorageContainer @StorageContainerParams -ErrorAction Stop
        Write-Verbose "Created storage container: $ContainerName" -Verbose
    }

    # Upload metadata file if needed
    if (!(Get-AzStorageBlob @StorageContainerParams -Blob $ComponentName/$MetaDataFileName -EA 0)) {
        $Item = Get-Item -Path $PSScriptRoot\..\templates\$MetaDataFileName
        Set-AzStorageBlobContent @StorageContainerParams -File $item.FullName `
            -Blob $ComponentName/$MetaDataFileName -Verbose -Force
    }
}
catch {
    Write-Error "Failed storage operation: $_"
}