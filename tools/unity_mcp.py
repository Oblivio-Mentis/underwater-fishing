"""Small stdlib client for the project's existing local Unity MCP server."""
import argparse
import json
import sys
import urllib.request


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("method")
    parser.add_argument("params", nargs="?", default="{}")
    parser.add_argument("--url", default="http://127.0.0.1:8080/mcp")
    args = parser.parse_args()
    session = None

    def request(method, params, request_id):
        nonlocal session
        headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}
        if session:
            headers["Mcp-Session-Id"] = session
        payload = {"jsonrpc": "2.0", "method": method, "params": params}
        if request_id is not None:
            payload["id"] = request_id
        req = urllib.request.Request(args.url, json.dumps(payload).encode(), headers)
        with urllib.request.urlopen(req, timeout=180) as response:
            session = response.headers.get("Mcp-Session-Id", session)
            if response.status == 202:
                return None
            if "text/event-stream" in response.headers.get("Content-Type", ""):
                for line in response:
                    if line.startswith(b"data: "):
                        result = json.loads(line[6:])
                        if result.get("id") == request_id:
                            return result
            raw = response.read()
            return json.loads(raw) if raw else None

    request("initialize", {"protocolVersion": "2024-11-05", "capabilities": {},
                           "clientInfo": {"name": "raid-local-tools", "version": "1"}}, 1)
    request("notifications/initialized", {}, None)
    if args.method == "schema":
        result = request("tools/list", {}, 2)
        result = next(t for t in result["result"]["tools"] if t["name"] == args.params)["inputSchema"]
    else:
        params = json.loads(sys.stdin.read() if args.params == "-" else args.params)
        method = args.method
        if "/" not in method:
            params = {"name": method, "arguments": params}
            method = "tools/call"
        result = request(method, params, 2)
    print(json.dumps(result, ensure_ascii=True))
    if isinstance(result, dict):
        payload = result.get("result", {})
        details = payload.get("structuredContent", {})
        details = details.get("result", details)
        if result.get("error") or payload.get("isError") or details.get("success") is False:
            raise SystemExit(1)


if __name__ == "__main__":
    main()
