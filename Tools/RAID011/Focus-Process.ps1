param([Parameter(Mandatory=$true)][int]$ProcessId,[int]$HoldSeconds=0)
$ErrorActionPreference='Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class Raid011FocusWindow {
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr handle,out uint process);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint first,uint second,bool attach);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr handle);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr handle);
 public static bool Focus(IntPtr target) {
   uint process; uint foreground=GetWindowThreadProcessId(GetForegroundWindow(),out process);
   uint current=GetCurrentThreadId(); AttachThreadInput(current,foreground,true);
   try { BringWindowToTop(target); return SetForegroundWindow(target); }
   finally { AttachThreadInput(current,foreground,false); }
 }
}
'@
$raid011process=Get-Process -Id $ProcessId
if($raid011process.MainWindowHandle -eq [IntPtr]::Zero) { throw 'Process has no interactive window' }
$raid011result=[Raid011FocusWindow]::Focus($raid011process.MainWindowHandle)
if($HoldSeconds -gt 0) {
  $raid011until=[DateTime]::UtcNow.AddSeconds([Math]::Min($HoldSeconds,60))
  while([DateTime]::UtcNow -lt $raid011until) {
    $null=[Raid011FocusWindow]::Focus($raid011process.MainWindowHandle)
    Start-Sleep -Milliseconds 100
  }
}
[PSCustomObject]@{ProcessId=$ProcessId;Focused=$raid011result;Foreground=([Raid011FocusWindow]::GetForegroundWindow() -eq $raid011process.MainWindowHandle)}
