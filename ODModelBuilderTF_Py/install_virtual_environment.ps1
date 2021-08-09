[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias("custom-tf-src")]$custom_tf_src = "$Env:USERPROFILE\Packages",
    [string][Alias("custom-tf-dst")]$custom_tf_dst = $null,
    [switch][Alias('h')] $help,
    [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

function Print-Usage() {
    Write-Host "  -custom-tf-src The directory containing the custom tensorflow packages"
    Write-Host "  -custom-tf-dst Only the custom TensorFlow package will be involved in the installation in this directory if specified"
    Write-Host "  -help          Print help and exit"
}

if ($help -or (($null -ne $properties) -and ($properties.Contains('/help') -or $properties.Contains('/?')))) {
    Print-Usage
    exit 0
}

# Directory of the script
$LASTEXITCODE=0
if (-not($PSScriptRoot) -and $psISE) { $scriptRoot = Split-Path $psISE.CurrentFile.FullPath } else { $scriptRoot = $PSScriptRoot }
# Check for the python environment existence
if (-not(Test-Path -PathType Container $scriptRoot\env)) {
    # Create a temporary setup directory
    Write-Output 'Creating Python environment.'
    if (Test-Path -PathType Container $scriptRoot\env.setup) {
        Remove-Item -Path $scriptRoot\env.setup -Recurse
    }
    New-Item -Path $scriptRoot\env.setup -ItemType Directory | Out-Null
    if ($LASTEXITCODE) { Exit $LASTEXITCODE }
    # Download Python
    Write-Output 'Downloading Python....'
    (New-Object Net.WebClient).DownloadFile("https://globalcdn.nuget.org/packages/python.3.7.8.nupkg", "$scriptRoot\env.setup\python.zip")
    if ($LASTEXITCODE) { Exit $LASTEXITCODE }
    Write-Output 'Done.'
    # Decompress Python
    Write-Output 'Decompressing Python...'
    Expand-Archive -Path $scriptRoot\env.setup\python.zip -DestinationPath $scriptRoot\env.setup
    if ($LASTEXITCODE) { Exit $LASTEXITCODE }
    # Move the python environment to the environment directory
    Move-Item $scriptRoot\env.setup\tools $scriptRoot\env
    if ($LASTEXITCODE) { Exit $LASTEXITCODE }
    # Remove the setup temporary directory
    Remove-Item -Path $scriptRoot\env.setup -Recurse
    if ($LASTEXITCODE) { Exit $LASTEXITCODE }
    # Copy the aenvironment activation scripts
    Copy-Item .\Activate.cmd.txt $scriptRoot\env\Activate.cmd
    Copy-Item .\Activate.ps1.txt $scriptRoot\env\Activate.ps1
    Write-Output 'Done.'
}
# Set the path to the python environment
$Env:Path = "$scriptRoot\env;$scriptRoot\env\Scripts;$scriptRoot\env\DLLs;$scriptRoot\env\Lib;$scriptRoot\env\Lib\site-packages;$Env:Path"
# Install the environment
try {
    Push-Location $scriptRoot
    if ($custom_tf_dst) {
        python.exe install_virtual_environment.py custom-tf --requirements "$scriptRoot\requirements.txt" --custom-tf-dir $custom_tf_src --dest-dir $custom_tf_dst 
    }
    else {
        python.exe install_virtual_environment.py --requirements "$scriptRoot\requirements.txt" --no-custom-tf
    }
    $installedCount = $LASTEXITCODE
    if (($installedCount -ne 0) -or (($installedCount -eq 0) -and -not(Test-Path $scriptRoot\env\env.info))) {
        if ($installedCount -ge 0) {
            if (-not($custom_tf_dst)) {
                python -m pip freeze >$scriptRoot\env\env.info
            }
            $installedCount = 0
        }
        else {
            if (-not($custom_tf_dst)) {
                Remove-Item $scriptRoot\env\env.info
            }
        }
    }
    Exit $installedCount
}
catch {
    Pop-Location
}
if ($LASTEXITCODE) { Exit $LASTEXITCODE }
Exit 0
