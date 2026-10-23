$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $PSScriptRoot
$window=Get-Process Unity | Where-Object {$_.MainWindowTitle -like ('*'+(Split-Path $projectRoot -Leaf)+'*')} | Select-Object -First 1
if(!$window -or $window.MainWindowHandle -eq 0){throw 'Project Unity window unavailable.'}
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class RecorderUnityRefreshWindow {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h,IntPtr id);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a,uint b,bool attach);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindowAsync(IntPtr h,int command);
}
'@
$thread=[RecorderUnityRefreshWindow]::GetWindowThreadProcessId([RecorderUnityRefreshWindow]::GetForegroundWindow(),[IntPtr]::Zero)
$caller=[RecorderUnityRefreshWindow]::GetCurrentThreadId()
[RecorderUnityRefreshWindow]::AttachThreadInput($caller,$thread,$true)|Out-Null
try{
 [RecorderUnityRefreshWindow]::ShowWindowAsync($window.MainWindowHandle,9)|Out-Null
 [RecorderUnityRefreshWindow]::BringWindowToTop($window.MainWindowHandle)|Out-Null
 [RecorderUnityRefreshWindow]::SetForegroundWindow($window.MainWindowHandle)|Out-Null
}finally{[RecorderUnityRefreshWindow]::AttachThreadInput($caller,$thread,$false)|Out-Null}
if([RecorderUnityRefreshWindow]::GetForegroundWindow() -ne $window.MainWindowHandle){throw 'Unity could not receive focus; no keys sent.'}
$keys=New-Object -ComObject WScript.Shell
$keys.SendKeys('^r')
Write-Output 'Refreshed the verified Unity project window.'
