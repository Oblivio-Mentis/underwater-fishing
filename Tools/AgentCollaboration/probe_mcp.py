"""Read-only discovery of configured development MCP endpoints; no credentials."""
import asyncio
import json
from mcp import ClientSession
from mcp.client.streamable_http import streamablehttp_client


async def main():
    async with streamablehttp_client('http://127.0.0.1:8080/mcp') as (r, w, _):
        async with ClientSession(r, w) as s:
            await s.initialize()
            resources = await s.list_resources()
            print('RESOURCES', resources.model_dump_json())
            for resource in resources.resources:
                if 'instances' in str(resource.uri) or 'editor_state' in str(resource.uri):
                    result = await s.read_resource(resource.uri)
                    print('RESOURCE', str(resource.uri), result.model_dump_json())
            ts = await s.list_tools()
            print('TOOLS', json.dumps([t.name for t in ts.tools]))
            for t in ts.tools:
                if t.name in ('manage_scene', 'read_console', 'manage_prefabs', 'set_active_instance', 'run_tests'):
                    print('SCHEMA', t.name, json.dumps(t.inputSchema))


if __name__ == '__main__':
    asyncio.run(main())
