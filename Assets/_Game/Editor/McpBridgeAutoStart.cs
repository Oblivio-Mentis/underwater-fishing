using UnityEditor;

namespace Raid.Editor
{
    // Local convenience: start the MCP bridge on every domain load so a relaunched editor is reachable
    // without opening the MCP window. Harmless when the bridge is already running or absent.
    [InitializeOnLoad]
    static class McpBridgeAutoStart
    {
        static McpBridgeAutoStart()
        {
            EditorApplication.delayCall += () =>
            {
                try
                {
                    var bridge = MCPForUnity.Editor.Services.MCPServiceLocator.Bridge;
                    if (bridge != null && !bridge.IsRunning) _ = bridge.StartAsync();
                }
                catch (System.Exception) { }
            };
        }
    }
}
