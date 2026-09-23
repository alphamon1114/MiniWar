"""Read-only MCP connection check. Run with Tools/McpSetup/venv/Scripts/python.exe."""
import asyncio
import json
from fastmcp import Client

async def main():
    async with Client('http://127.0.0.1:8765/mcp') as client:
        tool_list = await client.list_tools()
        resources = await client.list_resources()
        print(json.dumps({'tools': [x.name for x in tool_list], 'resources': [str(x.uri) for x in resources]}, ensure_ascii=False))
        for resource in resources:
            if 'instances' in str(resource.uri):
                result = await client.read_resource(resource.uri)
                for block in result:
                    print(getattr(block, 'text', str(block)))
                    try:
                        instances = json.loads(block.text).get('instances', [])
                    except (ValueError, AttributeError):
                        instances = []
                    target = next((x for x in instances if x.get('name') == 'MiniWar'), None)
                    if target:
                        selected = await client.call_tool('set_active_instance', {'instance': target['id']})
                        print(selected)
                        project = await client.read_resource('mcpforunity://project/info')
                        for info in project:
                            print(getattr(info, 'text', str(info)))

if __name__ == '__main__':
    asyncio.run(main())
