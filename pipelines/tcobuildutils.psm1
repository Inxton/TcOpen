# [START] ensure_gitversion.ps1 

function DeGZip-File
{
    Param(
        $infile,
        $outfile = ($infile -replace '\.gz$','')
    )
    $input = New-Object System.IO.FileStream $inFile, ([IO.FileMode]::Open), ([IO.FileAccess]::Read), ([IO.FileShare]::Read)
    $output = New-Object System.IO.FileStream $outFile, ([IO.FileMode]::Create), ([IO.FileAccess]::Write), ([IO.FileShare]::None)
    $gzipStream = New-Object System.IO.Compression.GzipStream $input, ([IO.Compression.CompressionMode]::Decompress)
    $buffer = New-Object byte[](1024)
    while($true){
        $read = $gzipstream.Read($buffer, 0, 1024)
        if ($read -le 0){break}
        $output.Write($buffer, 0, $read)
        }
    $gzipStream.Close()
    $output.Close()
    $input.Close()
}

function EnsureGitVersion {
    param (
        [string] $pathToGitVersion,
        [string] $gitversionUrl = "https://github.com/GitTools/GitVersion/releases/download/5.5.0/gitversion-win-x64-5.5.0.tar.gz"        
    )

   
    
    $gitVersionFolder = Split-Path $pathToGitVersion
    Push-Location $gitVersionFolder
    $gitVersionExecutableFullName = ((Join-Path (pwd) "gitversion.exe"))
    $gitVersionPwd = Get-Location
    Pop-Location

    DownloadIfMissing -folder $gitVersionFolder -url $gitversionUrl 
    
    
    if(Test-Path $gitVersionExecutableFullName)
    {
        Pop-Location
        return "GitVersion exists"
    }
   
    
    $gitVersionFile = Join-Path ($gitVersionPwd) (Split-Path $gitversionUrl -Leaf)
    $tar = $gitVersionFile.Replace(".gz","")

    DeGZip-File -infile $gitVersionFile -outfile $tar
    if (-not (Get-Command Expand-7Zip -ErrorAction Ignore)) {
        Install-Package -Scope CurrentUser -Force 7Zip4PowerShell > $null -ProviderName PowerShellGet
    }      
      Expand-7Zip $tar .\_toolz
      Remove-Item $gitVersionFile -ErrorAction Ignore
      Remove-Item $tar -ErrorAction Ignore
      Pop-Location
      return "Downloaded GitVersion"

}

if($gitVersion.Exists)
{
    Write-Host 'Git version is already retrieved.'
    return(0);
}
# [END] ensure_gitversion.ps1 

# [START] download_file.ps1 

function DownloadIfMissing{
    param
    (
        [string] $folder,
        [string] $url 
    )
    $fileName = (Split-Path ($url) -Leaf)
    $absolutePath =  Resolve-Path $folder
    $outFilePath= (Join-Path $absolutePath $fileName)
    if (!(Test-Path $outFilePath))
    {
        Write-Host "Couldn't find $outFilePath"
        Write-Host "Downloading from $url to $outFilePath"
        try 
        {
            (New-Object System.Net.WebClient).DownloadFile($url,  $outFilePath)
            return  "Downloaded from $url to $outFilePath"
        }
        catch {
            return "An error occured while downloading from $url to $outFilePath"
        }
    }
    else
    {
        return "File exists at $folder"
    }
}

function EnsureNuget{
    param
    (
        [string] $pathToNugetFolder = ".\.nuget",
        [string] $nugetExeUrl = "https://dist.nuget.org/win-x86-commandline/latest/nuget.exe"
    )
    DownloadIfMissing -folder $pathToNugetFolder -url $nugetExeUrl
}


function PushNugets
{
    param
    (
       [Parameter(Mandatory)]
       [string] $folderWithNugets,
       [Parameter(Mandatory)]
       [string] $token,
       [Parameter(Mandatory)]
       [string] $source 
    )
    Push-Location $folderWithNugets
    $names = Get-ChildItem -n -file

    foreach($name in $names){
        Write-Output  "Try to push $name"
       try{
          & dotnet nuget push $name -k $token  -s $source  --skip-duplicate
          Write-Output  "OK $name"
       } catch {
          Write-Output $_
          Write-Output "Error while $name"
       } 
    }
    Pop-Location
}
# [END] push_nugets_in_folder.ps1 

# [START] clean.ps1 

# Will clean every bin obj of every csproj in subfolders
function GetRootDirectoryOf{
    param(
        [string] $filter
    )
    return Get-ChildItem -Path .\ -Filter $filter -Recurse -ErrorAction SilentlyContinue | Resolve-path | Split-Path -Parent
}

function CleanObjBin {
    GetRootDirectoryOf "*.csproj" | ForEach-Object { 
        Push-Location ($_)

        $removeFolders = @( ".\obj",".\bin")
        # $removeFolderContent = @(".\_meta\*", ".\_meta\*")

        $folder = $_.Replace("Microsoft.PowerShell.Core\FileSystem::","")

        foreach($item in ($removeFolders)){
            Write-Host "In $folder trying to remove $item"
            Remove-Item -r $item  -ErrorAction SilentlyContinue
        }

        Pop-Location
    }
}

function RemoveTcBins {
    GetRootDirectoryOf "*.tsproj" | ForEach-Object { 
        Push-Location ($_)

        $removeFolders = @( ".\_Boot",".\_Config")
        # $removeFolderContent = @(".\_meta\*", ".\_meta\*")

        $folder = $_.Replace("Microsoft.PowerShell.Core\FileSystem::","")

        foreach($item in ($removeFolders)){
            Write-Host "In $folder trying to remove $item"
            Remove-Item -r $item  -ErrorAction SilentlyContinue
        }

        Pop-Location
    }
}


function RemoveTcProjBins {
    GetRootDirectoryOf "*.plcproj" | ForEach-Object { 
        Push-Location ($_)

        $removeFolders = @( ".\_CompileInfo")
        # $removeFolderContent = @(".\_meta\*", ".\_meta\*")

        $folder = $_.Replace("Microsoft.PowerShell.Core\FileSystem::","")

        foreach($item in ($removeFolders)){
            Write-Host "In $folder trying to remove $item"
            Remove-Item -r $item  -ErrorAction SilentlyContinue
        }

        Pop-Location
    }
}

function RemoveGenerated {
    GetRootDirectoryOf "*.csproj" | ForEach-Object { 
        Push-Location ($_)

        $removeFolders = @( ".\_generated")
        # $removeFolderContent = @(".\_meta\*", ".\_meta\*")

        $folder = $_.Replace("Microsoft.PowerShell.Core\FileSystem::","")

        foreach($item in ($removeFolders)){
            Write-Host "In $folder trying to remove $item"
            Remove-Item -r $item  -ErrorAction SilentlyContinue
        }

        Pop-Location
    }
}

function RemoveMeta {
    GetRootDirectoryOf "*.csproj" | ForEach-Object { 
        Push-Location ($_)

        $removeFolders = @( ".\_meta")
        # $removeFolderContent = @(".\_meta\*", ".\_meta\*")

        $folder = $_.Replace("Microsoft.PowerShell.Core\FileSystem::","")

        foreach($item in ($removeFolders)){
            Write-Host "In $folder trying to remove $item"
            Remove-Item -r $item  -ErrorAction SilentlyContinue
        }

        Pop-Location
    }
}

function GitVersionToPlcVersion{
    param($gitVersion)
    return $gitVersion.Major.ToString() + "." + $gitVersion.Minor.ToString() + "." + $gitVersion.Patch.ToString() + "." + $gitVersion.PreReleaseNumber.ToString()
}

# [END] clean.ps1 

# tsproj files are UTF-8 without BOM. Get-Content would read them with the ANSI code page and each save
# would encode non-ASCII characters (e.g. 'µs' in EtherCAT terminal names) once more, growing the file on every build.
function Read-XmlFile($path)
{
    $xml = New-Object System.Xml.XmlDocument
    $xml.PreserveWhitespace = $true
    $xml.Load((Resolve-Path $path).Path)
    return ,$xml
}

function Save-XmlFile([System.Xml.XmlDocument] $xml, $path)
{
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $writer = [System.Xml.XmlWriter]::Create((Resolve-Path $path).Path, $settings)
    try { $xml.Save($writer) } finally { $writer.Dispose() }
}

# Isolated core functions 

function Has-Isolated-Core($tcproj)
{
    $settingsNode = Select-Xml -Path $tcproj -XPath "TcSmProject/Project/System/Settings"
    return $settingsNode.Node.NonWinCpus -gt 0
}

function Shared-Cores($tcproj)
{
    $settingsNode = Select-Xml -Path $tcproj -XPath "TcSmProject/Project/System/Settings"
    if($settingsNode -eq $null){ return 1 }
    $isolated = $settingsNode.Node.NonWinCpus
    $max = $settingsNode.Node.MaxCpus
    return $max - $isolated
}

function Has-RT-Isolated-Core($tcproj)
{
    $cpuNodes = Select-Xml -Path $tcproj -XPath "TcSmProject/Project/System/Settings/Cpu"
    if($cpuNodes -eq $null){ return $false }
    $firstIsolatedCoreId = Shared-Cores $tcproj 
    foreach($cpuNode in $cpuNodes)
    {
        if($cpuNode.Node.CpuId -ige $firstIsolatedCoreId){ return $true;}
    }
    return $false

}

function Set-CPU-Cores($tcproj, $shared, $isolated)
{
    $xml = Read-XmlFile $tcproj
    $settingsNode = $xml.SelectSingleNode("TcSmProject/Project/System/Settings")
    if($settingsNode -eq $null)
    {
        $settingsNode = $xml.CreateElement('Settings')
        $settingsNode.SetAttribute("MaxCpus", "-1")
        $settingsNode.SetAttribute("NonWinCpus", "-1")
        $xml.SelectSingleNode("TcSmProject/Project/System").PrependChild($settingsNode)
    }
    $settingsNode.MaxCpus= ($shared + $isolated).ToString()
    if(-Not $settingsNode.HasAttribute("NonWinCpus"))
    {
    $settingsNode.SetAttribute("NonWinCpus", "-1")
    }
    $settingsNode.NonWinCpus= ($isolated).ToString()

    $isolatedCoreXmlNode = $xml.CreateElement('Cpu')
    $isolatedCoreXmlNode.SetAttribute("CpuId", ($shared).ToString())
    
    $settingsCpuNodes= $xml.SelectNodes("TcSmProject/Project/System/Settings/Cpu")
    foreach($cpuNode in $settingsCpuNodes)
    {
        $settingsNode.RemoveChild($cpuNode)
    }
    
    $settingsNode.AppendChild($isolatedCoreXmlNode)
    
    Save-XmlFile $xml $tcproj
}

function Is-AMS-Id-Local($TargetAmsId)
{
    Import-Module "C:\TwinCAT\AdsApi\Powershell\TcXaeMgmt\TcXaeMgmt.psd1" -Scope Local
    $route2local = Get-AdsRoute -Local
    $LocalAmsId = $route2local.NetId.ToString()
    $IsLocal = -Not $TargetAmsId -or ($TargetAmsId -eq "127.0.0.1.1.1") -or ($TargetAmsId -eq $LocalAmsId)
    return $IsLocal
}

function Remove-TargetNetId($tcproj)
{
    $xml = Read-XmlFile $tcproj
    $projectNode = $xml.SelectSingleNode("TcSmProject/Project")
    if($projectNode.HasAttribute("TargetNetId"))
    {
        $projectNode.RemoveAttribute("TargetNetId")
        Save-XmlFile $xml $tcproj
    }
}

function Is-License-Valid($targetAmsId)
{
    $PK_PHOENY = ([System.Environment]::GetEnvironmentVariable('_PK_PHOENY_'))
    $TK_PHOENY = ([System.Environment]::GetEnvironmentVariable('_TK_PHOENY_'))
    $TW_PHOENY = ([System.Environment]::GetEnvironmentVariable('_TW_PHOENY'))
    $TW_USER = ([System.Environment]::GetEnvironmentVariable('_TW_USER_'))
    $TW_TOKEN = ConvertTo-SecureString -String ([System.Environment]::GetEnvironmentVariable('_TW_TOKEN_')) -AsPlainText -Force
    Import-Module "C:\TwinCAT\AdsApi\Powershell\TcXaeMgmt\TcXaeMgmt.psd1" -Scope Local
    $licenses = Get-TcLicense -Address $targetAmsId  -OrderId TC1200
    $result = $false
    $recepients = New-Object Collections.Generic.List[string]
    $recepients.Add($PK_PHOENY) 
    $recepients.Add($TK_PHOENY) 
    $url = "https://api.twilio.com/2010-04-01/Accounts/" + $TW_USER + "/Messages.json"
    $credential = New-Object System.Management.Automation.PSCredential($TW_USER, $TW_TOKEN)

    foreach($licence in $licenses )
    {
        $result = $licence.Valid -or $result 
        $dt2Expire = NEW-TIMESPAN -Start (Get-Date) -End $licence.ExpireTime 
        $hrs2Expire = ($dt2Expire.Days ) * 24 + $dt2Expire.Hours
        if($hrs2Expire -lt 48)
        {
            $details = "TwinCAT runtime licence on target: " + [System.Net.Dns]::GetHostName() + " with TargetAmsNetId: " + $targetAmsId + " is going to expire in " + $hrs2Expire + " hours!"
            foreach($recepient in $recepients )
            {
                $params = @{ To = $recepient; From = $TW_PHOENY; Body = $details }
                Invoke-WebRequest $url -Method Post -Credential $credential -Body $params -UseBasicParsing | ConvertFrom-Json | Select sid, body
            }
        }
    }
    if ($result)
    {
        return $result
    }
    else
    {
        $licenses
        $details = "No valid TwinCAT runtime licence found on target: " + [System.Net.Dns]::GetHostName() + " with TargetAmsNetId: " + $targetAmsId + "!"
        foreach($recepient in $recepients )
        {
            $params = @{ To = $recepient; From = $TW_PHOENY; Body = $details }
            Invoke-WebRequest $url -Method Post -Credential $credential -Body $params -UseBasicParsing | ConvertFrom-Json | Select sid, body
        }
        return $result
    }
}

function GetTwincat3Dir{
    try 
    {
        $TC3DIR = Get-ChildItem -Path Env:\TWINCAT3DIR
    }
    catch 
    {
        Write-Host "Twincat 3 directory not discovered."
    }
    if($TC3DIR.Value)
    {
        return $TC3DIR.Value
    }
    else
    {
        Write-Host "Twincat 3 directory not discovered."
    }
}


function GetInstalledBuilds{
    $TC3DIR = GetTwincat3Dir
    $MajorVersion = -join((get-item $TC3DIR ).Name,".")
    $TC3BASEDIR = -join($TC3DIR ,"Components\Base")
    $sBuilds = (Get-ChildItem -Path $TC3BASEDIR -Filter "Build_*" -Recurse -Directory).Name.Replace("Build_",$MajorVersion)
    $Builds = New-Object Collections.Generic.List[Version]
    foreach ($sBuild in $sBuilds)
    {
        $Build = [System.Version]::Parse($sBuild)
        $Builds.Add($Build)
    }
    return $Builds
}


function GetInstalledTwincatVersion{
    return Get-ItemProperty HKLM:\Software\Wow6432Node\Beckhoff\TwinCAT3\System | Select-Object TcVersion
}


function RemovePinVersion($tsproj)
{
    $xml = Read-XmlFile $tsproj
    $TcSmProjectNode = $xml.SelectSingleNode("TcSmProject")
    if($TcSmProjectNode.HasAttribute("TcVersionFixed"))
    {
        $TcSmProjectNode.RemoveAttribute("TcVersionFixed")
        Save-XmlFile $xml $tsproj
        Write-Host "Pin version removed in`t" $tsproj
    }
}


function DisableDevices($tsproj)
{
    $xml = Read-XmlFile $tsproj
    $ioNode = $xml.SelectSingleNode("TcSmProject/Project/Io")
    if($ioNode -ne $null)
    {
        $changed = $false
        $devicesNodes= $xml.SelectNodes("TcSmProject/Project/Io/Device")
        foreach($deviceNode in $devicesNodes)
        {
            if(-not $deviceNode.HasAttribute("Disabled"))
            {
                $deviceNode.SetAttribute("Disabled","true")
                Write-Host "Disabled device`t" $deviceNode.RemoteName " in`t" $tsproj 
                $changed = $true
            }
        }
        if($changed) { Save-XmlFile $xml $tsproj }
    }
}


# COM message filter: retries calls rejected by a busy Visual Studio (RPC_E_CALL_REJECTED)
function Register-ComMessageFilter
{
    if (-not ("TcoBuild.MessageFilter" -as [type]))
    {
        Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;

namespace TcoBuild
{
    [ComImport, Guid("00000016-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IOleMessageFilter
    {
        [PreserveSig] int HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo);
        [PreserveSig] int RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType);
        [PreserveSig] int MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType);
    }

    public class MessageFilter : IOleMessageFilter
    {
        [DllImport("Ole32.dll")]
        private static extern int CoRegisterMessageFilter(IOleMessageFilter newFilter, out IOleMessageFilter oldFilter);

        public static void Register() { IOleMessageFilter old; CoRegisterMessageFilter(new MessageFilter(), out old); }
        public static void Revoke() { IOleMessageFilter old; CoRegisterMessageFilter(null, out old); }

        int IOleMessageFilter.HandleInComingCall(int dwCallType, IntPtr hTaskCaller, int dwTickCount, IntPtr lpInterfaceInfo) { return 0; }
        // SERVERCALL_RETRYLATER -> retry after 200 ms
        int IOleMessageFilter.RetryRejectedCall(IntPtr hTaskCallee, int dwTickCount, int dwRejectType) { return dwRejectType == 2 ? 200 : -1; }
        int IOleMessageFilter.MessagePending(IntPtr hTaskCallee, int dwTickCount, int dwPendingType) { return 2; }
    }
}
"@
    }
    [TcoBuild.MessageFilter]::Register()
}

function Unregister-ComMessageFilter
{
    if ("TcoBuild.MessageFilter" -as [type])
    {
        [TcoBuild.MessageFilter]::Revoke()
    }
}

# Starts Visual Studio without UI and opens the solution, so tools attaching over EnvDTE (Vortex builder) can use it.
# Returns handle for Stop-HiddenVs.
function Start-HiddenVs
{
    param(
        [string] $solutionPath,
        [string] $progId = "VisualStudio.DTE.16.0"
    )

    Register-ComMessageFilter

    $before = @(Get-Process devenv -ErrorAction SilentlyContinue | ForEach-Object { $_.Id })
    $dte = New-Object -ComObject $progId
    $vsProcessIds = @(Get-Process devenv -ErrorAction SilentlyContinue | Where-Object { $before -notcontains $_.Id } | ForEach-Object { $_.Id })

    $dte.SuppressUI = $true
    $dte.UserControl = $false
    try { $dte.MainWindow.Visible = $false } catch {}

    Write-Host "Opening $solutionPath in hidden $progId (pid $vsProcessIds)"
    $dte.Solution.Open($solutionPath)

    return [PSCustomObject]@{ Dte = $dte; ProcessIds = $vsProcessIds }
}

function Stop-HiddenVs($hiddenVs)
{
    if ($hiddenVs -eq $null) { return }

    try { $hiddenVs.Dte.Quit() } catch { Write-Host "DTE.Quit failed: $_" }
    Unregister-ComMessageFilter

    foreach ($id in $hiddenVs.ProcessIds)
    {
        try { Wait-Process -Id $id -Timeout 60 -ErrorAction Stop } catch { Stop-Process -Id $id -Force -ErrorAction SilentlyContinue }
    }
}

function Stop-AllVs
{
    try { Get-Process devenv -ErrorAction SilentlyContinue | Stop-Process -ErrorAction SilentlyContinue } catch {}
}

# Build checkpoints: successfully completed steps are recorded in a state file, so a build started with resume skips them.
function Read-BuildState($stateFile)
{
    if (Test-Path $stateFile)
    {
        try { return Get-Content $stateFile -Raw | ConvertFrom-Json } catch { Write-Host "Unreadable build state '$stateFile', ignoring: $_" }
    }
    return $null
}

function Save-BuildState($stateFile, $state)
{
    New-Item -ItemType Directory -Force (Split-Path $stateFile) | Out-Null
    ConvertTo-Json -InputObject $state | Set-Content $stateFile -Encoding UTF8
}

# Keeps the previous state when resuming the same configuration, otherwise starts a new one.
function Initialize-BuildState($stateFile, [bool] $resume, [string] $buildConfig)
{
    $state = Read-BuildState $stateFile
    if ($resume -and $state -ne $null -and $state.buildConfig -eq $buildConfig)
    {
        Write-Host "Resuming build, completed steps will be skipped: $(@($state.completed) -join ', ')" -ForegroundColor Yellow
        return
    }

    if ($resume)
    {
        Write-Host "Nothing to resume (no previous state or different buildConfig), running full build" -ForegroundColor Yellow
    }
    Save-BuildState $stateFile ([PSCustomObject]@{ buildConfig = $buildConfig; completed = @() })
}

function Test-BuildStepPending($stateFile, [string] $step)
{
    $state = Read-BuildState $stateFile
    if ($state -ne $null -and @($state.completed) -contains $step)
    {
        Write-Host "Skipping '$step', completed in previous run" -ForegroundColor DarkGray
        return $false
    }
    return $true
}

function Complete-BuildStep($stateFile, [string] $step)
{
    $state = Read-BuildState $stateFile
    if ($state -eq $null) { return }
    if (@($state.completed) -notcontains $step)
    {
        $state.completed = @($state.completed) + $step
        Save-BuildState $stateFile $state
    }
}

function Clear-BuildState($stateFile)
{
    Remove-Item $stateFile -ErrorAction SilentlyContinue
}

# Splits solution filter projects into components (src\<Component>), ordered so that referenced components come first.
# Dependencies come from csproj project references and plcproj library references.
function Get-SlnfComponents($slnfPath)
{
    $slnf = Get-Content $slnfPath -Raw | ConvertFrom-Json
    $solutionDir = Split-Path (Join-Path (Split-Path (Resolve-Path $slnfPath).Path) $slnf.solution.path)

    $components = [ordered]@{}
    foreach ($project in $slnf.solution.projects)
    {
        $name = ($project -split '\\')[0..1] -join '\'
        if (-not $components.Contains($name))
        {
            $components[$name] = [PSCustomObject]@{ Name = ($project -split '\\')[1]; Key = $name; Projects = @(); DependsOn = @() }
        }
        $components[$name].Projects += $project
    }

    # plc library name -> component
    $libraryOwners = @{}
    foreach ($component in $components.Values)
    {
        Get-ChildItem (Join-Path $solutionDir $component.Key) -Recurse -Filter *.plcproj -ErrorAction SilentlyContinue |
            ForEach-Object { $libraryOwners[$_.BaseName] = $component.Key }
    }

    foreach ($component in $components.Values)
    {
        $references = @()
        foreach ($project in $component.Projects | Where-Object { $_ -like '*.csproj' })
        {
            $projectPath = Join-Path $solutionDir $project
            $references += ([xml](Get-Content $projectPath)).Project.ItemGroup.ProjectReference.Include | Where-Object { $_ } | ForEach-Object {
                $full = [System.IO.Path]::GetFullPath((Join-Path (Split-Path $projectPath) $_))
                ($full.Substring($solutionDir.TrimEnd('\').Length + 1) -split '\\')[0..1] -join '\'
            }
        }
        Get-ChildItem (Join-Path $solutionDir $component.Key) -Recurse -Filter *.plcproj -ErrorAction SilentlyContinue | ForEach-Object {
            $plcproj = [xml](Get-Content $_.FullName)
            $references += @($plcproj.Project.ItemGroup.PlaceholderReference.Include) + @($plcproj.Project.ItemGroup.LibraryReference.Include) |
                Where-Object { $_ } | ForEach-Object { $libraryOwners[($_ -split ',')[0].Trim()] }
        }
        $component.DependsOn = @($references | Where-Object { $_ -and $_ -ne $component.Key -and $components.Contains($_) } | Sort-Object -Unique)
    }

    # depth-first ordering, keeps the slnf order where there is no dependency
    $ordered = New-Object System.Collections.ArrayList
    $visiting = @{}
    function Visit($component)
    {
        if ($ordered -contains $component) { return }
        if ($visiting[$component.Key]) { throw "Circular dependency between components at '$($component.Key)'" }
        $visiting[$component.Key] = $true
        foreach ($dependency in $component.DependsOn) { Visit $components[$dependency] }
        $visiting[$component.Key] = $false
        [void]$ordered.Add($component)
    }
    foreach ($component in $components.Values) { Visit $component }

    return $ordered
}

# Writes solution filter with given projects into $outputDir and returns its path.
function New-PartialSlnf($sourceSlnfPath, [string[]] $projects, [string] $name, [string] $outputDir)
{
    $slnf = Get-Content $sourceSlnfPath -Raw | ConvertFrom-Json
    $solutionPath = (Resolve-Path (Join-Path (Split-Path (Resolve-Path $sourceSlnfPath).Path) $slnf.solution.path)).Path

    New-Item -ItemType Directory -Force $outputDir | Out-Null
    $outputDir = (Resolve-Path $outputDir).Path
    $relativeSolutionPath = (New-Object System.Uri("$outputDir\")).MakeRelativeUri((New-Object System.Uri($solutionPath))).ToString().Replace('/', '\')

    $partial = @{ solution = @{ path = $relativeSolutionPath; projects = @($projects) } }
    $path = Join-Path $outputDir "$name.slnf"
    ConvertTo-Json -InputObject $partial -Depth 5 | Set-Content $path -Encoding UTF8
    return $path
}