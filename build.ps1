[CmdletBinding(PositionalBinding=$false)]
Param(
    [string][Alias('c')]$configuration = "Release",
    [string]$platform = $null,
    [string] $projects,
    [string][Alias('v')]$verbosity = "minimal",
    [bool] $warnAsError = $false,
    [bool] $nodeReuse = $true,
    [switch][Alias('r')]$restore,
    [switch][Alias('b')]$build,
    [switch] $rebuild,
    [switch] $deploy,
    [switch] $sign,
    [switch] $clean,
    [string] $runtimeSourceFeed = '',
    [string] $runtimeSourceFeedKey = '',
    [switch] $help,
    [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

# Unset 'Platform' environment variable to avoid unwanted collision in InstallDotNetCore.targets file
# some computer has this env var defined (e.g. Some HP)
if($env:Platform) { $env:Platform="" }

function Print-Usage() {
    Write-Host "Common settings:"
    Write-Host "  -configuration <value>  Build configuration: 'Debug' or 'Release' (short: -c)"
    Write-Host "  -platform <value>       Platform configuration: 'x86', 'x64' or any valid Platform value to pass to msbuild"
    Write-Host "  -verbosity <value>      Msbuild verbosity: q[uiet], m[inimal], n[ormal], d[etailed], and diag[nostic] (short: -v)"
    Write-Host "  -help                   Print help and exit"
    Write-Host ""

    Write-Host "Actions:"
    Write-Host "  -restore                Restore dependencies (short: -r)"
    Write-Host "  -build                  Build solution (short: -b)"
    Write-Host "  -rebuild                Rebuild solution"
    Write-Host "  -deploy                 Deploy built VSIXes"
    Write-Host "  -sign                   Sign build outputs"
    Write-Host "  -clean                  Clean the solution"
    Write-Host ""

    Write-Host "Advanced settings:"
    Write-Host "  -projects <value>       Semi-colon delimited list of sln/proj's to build. Globbing is supported (*.sln)"
    Write-Host "  -warnAsError <value>    Sets warnaserror msbuild parameter ('true' or 'false')"
    Write-Host ""

    Write-Host "Command line arguments not listed above are passed thru to msbuild."
    Write-Host "The above arguments can be shortened as much as to be unambiguous (e.g. -co for configuration, -t for test, etc.)."
}


function Build {
    $bl = if ($binaryLog) { '/bl:' + (Join-Path $LogDir 'Build.binlog') } else { '' }
    $platformArg = if ($platform) { "/p:Platform=$platform" } else { '/p:Platform=Any CPU' }

    if ($projects) {
        # Re-assign properties to a new variable because PowerShell doesn't let us append properties directly for unclear reasons.
        # Explicitly set the type as string[] because otherwise PowerShell would make this char[] if $properties is empty.
        [string[]] $msbuildArgs = $properties
        
        # Resolve relative project paths into full paths 
        $projects = ($projects.Split(';').ForEach({Resolve-Path $_}) -join ';')
        
        $msbuildArgs += "/p:Projects=$projects"
        $properties = $msbuildArgs
    }

    if (-not($PSScriptRoot) -and $psISE) { $scriptRoot = Split-Path $psISE.CurrentFile.FullPath } else { $scriptRoot = $PSScriptRoot }

    if ($clean -or $rebuild) {
        msbuild `
            $bl `
            $platformArg `
            /t:Clean `
            /p:Configuration=$configuration `
            /p:RepoRoot=$RepoRoot `
            @properties
    }
    if (-not $clean) {
        if ($restore) {
            if ($projects) {
                dotnet restore $projects
            }
            else {
                dotnet restore
            }
        }
        msbuild `
            $bl `
            $platformArg `
            /p:Configuration=$configuration `
            /p:RepoRoot=$RepoRoot `
            /p:Restore=$restore `
            /p:Build=$build `
            /p:Rebuild=$rebuild `
            /p:Deploy=$deploy `
            /p:Sign=$sign `
            @properties
    }
}

function SetVars {
    [CmdletBinding()]
    param(
        ## The path to the script to run
        [Parameter(Mandatory = $true)]
        [string] $Path,

        ## The arguments to the script
        [string] $ArgumentList
    )

    Set-StrictMode -Version 3

    $tempFile = [IO.Path]::GetTempFileName()

    ## Store the output of cmd.exe.  We also ask cmd.exe to output
    ## the environment table after the batch file completes
    #cmd /c " `"$Path`" $argumentList && set > `"$tempFile`" "
    cmd.exe /c "`"$Path`" $argumentList & set > `"$tempFile`" "

    ## Go through the environment variables in the temp file.
    ## For each of them, set the variable in our local environment.
    Get-Content $tempFile | Foreach-Object {
        if($_ -match "^(.*?)=(.*)$")
        {
            Set-Content "env:\$($matches[1])" $matches[2]
        }
    }
}

function SetMSBuild {
    # Find Visual Studio
    if (-not $env:VisualStudioVersion) {
        $vsWhere = ${env:ProgramFiles(x86)} + "\Microsoft Visual Studio\Installer\vswhere.exe"
        if (Test-Path -Path $vsWhere) {
            $vsCommonTools = & $vsWhere -latest -prerelease -property installationPath
            $vsMSBuild = $vsCommonTools + "\MSBuild"
            Set-Content "env:\MSBuildExtensionsPath32" $vsMSBuild
            $vsCommonTools = $vsCommonTools + "\Common7\Tools"
        }
        if (-not (Test-Path -Path $vsCommonTools)) {
            $vsCommonTools = $env:VS140COMMONTOOLS
        }
        if (-not (Test-Path -Path $vsCommonTools)) {
            Write-Error "Can't find VS 2015, 2017 or 2019"
            Write-Error "Error: Visual Studio 2015, 2017 or 2019 required"
            exit 1
        }
        $env:VSCMD_START_DIR=$PSScriptRoot
        $VsDevCmd = $vsCommonTools + "\VsDevCmd.bat"
        SetVars($VsDevCmd)
    }
}

try {

    # Print usage
    if ($help -or (($null -ne $properties) -and ($properties.Contains('/help') -or $properties.Contains('/?')))) {
        Print-Usage
        exit 0
    }

    # Set MSBuild environment
    SetMSBuild

    # Clean or rebuild target
    if ($clean -or $rebuild) {
        Get-ChildItem * -Include *.nupkg -Recurse | Remove-Item
    }

    # Build the solution
    Build
    exit $LASTEXITCODE
}
catch {
    Write-Host $_.ScriptStackTrace
    exit 1
}
