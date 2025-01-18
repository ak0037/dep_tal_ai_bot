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

# Verify location and get prefix
try {
    $LocationLookup = Get-Content -Path $PSScriptRoot\..\bicep\global\region.json | ConvertFrom-Json
    if (!$LocationLookup.$Location) {
        throw "Location '$Location' not found in region.json"
    }
    $Prefix = $LocationLookup.$Location.Prefix
}
catch {
    Write-Error "Failed to process region.json: $_"
    return
}

# Get branch name with fallback to main
$BranchName = git branch --show-current 2>$null
if (!$BranchName) { $BranchName = "main" }

# Create workflows directory if it doesn't exist
$workflowsPath = "$PSScriptRoot\..\..\..\..\..\.github\workflows"
if (!(Test-Path -Path $workflowsPath)) {
    try {
        New-Item -Path $workflowsPath -ItemType Directory -Force | Out-Null
        Write-Verbose "Created workflows directory: $workflowsPath" -Verbose
    }
    catch {
        Write-Error "Failed to create workflows directory: $_"
        return
    }
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
    },
    @{
        SourcePath      = "$PSScriptRoot\..\templates\PsiBot-build-OrgName.yml"
        DestinationPath = "$workflowsPath\PsiBot-build-${OrgName}.yml"
        TokenstoReplace = @(
            @{ Name = '{OrgName}'; Value = $OrgName },
            @{ Name = '{Location}'; Value = $Location },
            @{ Name = '{BranchName}'; Value = $BranchName }
        )
    },
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

foreach ($file in $filestocopy) {
    try {
        # Verify source file exists
        if (!(Test-Path -Path $file.SourcePath)) {
            Write-Error "Source file not found: $($file.SourcePath)"
            continue
        }

        # Copy file if it doesn't exist
        if (!(Test-Path -Path $file.DestinationPath)) {
            Copy-Item -Path $file.SourcePath -Destination $file.DestinationPath -Force
            Write-Verbose "Copied file to: $($file.DestinationPath)" -Verbose
        }

        # Replace tokens
        $content = Get-Content -Path $file.DestinationPath -Raw
        foreach ($token in $file.TokenstoReplace) {
            if ($token.Name -and $content -match [regex]::Escape($token.Name)) {
                $content = $content -replace [regex]::Escape($token.Name), $token.Value
            }
        }
        Set-Content -Path $file.DestinationPath -Value $content -Force
    }
    catch {
        Write-Error "Failed processing file $($file.SourcePath): $_"
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

    # Create container if it doesn't exist
    if (!(Get-AzStorageContainer @StorageContainerParams -EA 0)) {
        New-AzStorageContainer @StorageContainerParams -ErrorAction Stop
        Write-Verbose "Created storage container: $ContainerName" -Verbose
    }

    # Copy metadata file if it doesn't exist
    if (!(Get-AzStorageBlob @StorageContainerParams -Blob $ComponentName/$MetaDataFileName -EA 0)) {
        $Item = Get-Item -Path $PSScriptRoot\..\templates\$MetaDataFileName
        Set-AzStorageBlobContent @StorageContainerParams -File $item.FullName -Blob $ComponentName/$MetaDataFileName -Force -Verbose
    }
}
catch {
    Write-Error "Failed storage operation: $_"
}