"""Call the local Unity MCP from a shell when a client has not reloaded its tool catalog."""
import argparse
import asyncio
import json
from pathlib import Path
from fastmcp import Client

async def run(args):
    async with Client('http://127.0.0.1:8765/mcp', timeout=90) as client:
        await client.call_tool('set_active_instance', {'instance': 'MiniWar'})
        if args.schema:
            for tool in await client.list_tools():
                if tool.name == args.schema:
                    print(json.dumps(tool.model_dump(), ensure_ascii=False))
                    return
            raise SystemExit('Tool not found: ' + args.schema)
        if args.resource:
            for block in await client.read_resource(args.resource):
                print(getattr(block, 'text', str(block)))
            return
        arguments = json.loads(Path(args.json_file).read_text(encoding='utf-8-sig')) if args.json_file else {}
        result = await client.call_tool(args.tool, arguments)
        for block in result.content:
            if hasattr(block, 'text'):
                print(block.text)
        if result.is_error:
            raise SystemExit(1)

if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--tool')
    parser.add_argument('--json-file')
    parser.add_argument('--resource')
    parser.add_argument('--schema')
    options = parser.parse_args()
    if not any((options.tool, options.resource, options.schema)):
        parser.error('Specify --tool, --resource or --schema')
    asyncio.run(run(options))
