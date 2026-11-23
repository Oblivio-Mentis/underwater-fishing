"""Adds .cs files that Unity's generated csproj missed, then compiles.
Unity rewrites *.csproj from the editor and drops files created outside it, so a fresh
worktree would silently skip new code. Files under a folder that has its own .asmdef are
never added: they belong to that assembly (Raid.Progression, tests), not Assembly-CSharp.
Run from the repo root:  python Tools/RAID018/CompileCheck.py [Assembly-CSharp|Assembly-CSharp-Editor]
"""
import os, re, subprocess, sys

root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
project = sys.argv[1] if len(sys.argv) > 1 else "Assembly-CSharp"
path = os.path.join(root, project + ".csproj")
text = open(path, encoding="utf-8-sig").read()
included = {m.replace("/", "\\").lower() for m in re.findall(r'<Compile Include="([^"]+)"', text)}
# Folders owned by an asmdef belong to a different assembly.
owned = []
for base, _, files in os.walk(os.path.join(root, "Assets")):
    if any(f.endswith(".asmdef") for f in files):
        owned.append(os.path.relpath(base, root).lower() + "\\")
added = []
for base, _, files in os.walk(os.path.join(root, "Assets")):
    for name in files:
        if not name.endswith(".cs"):
            continue
        rel = os.path.relpath(os.path.join(base, name), root)
        low = rel.lower()
        if low in included or any(low.startswith(o) for o in owned):
            continue
        is_editor = "\editor\\" in low or "\tests\\" in low
        if project == "Assembly-CSharp" and is_editor:
            continue
        if project == "Assembly-CSharp-Editor" and not is_editor:
            continue
        added.append(rel)
if added:
    marker = "</ItemGroup>"
    block = "".join(f'    <Compile Include="{a}" />\n' for a in added)
    text = text.replace(marker, block + "  " + marker, 1)
    open(path, "w", encoding="utf-8", newline="\r\n").write(text)
    print(f"{project}: added {len(added)} file(s)")
sys.exit(subprocess.run(["dotnet", "build", project + ".csproj", "-c", "Debug", "-nologo", "-v", "q",
                         f"-p:OutputPath=Temp/dotnet-check-{project}/"], cwd=root).returncode)
