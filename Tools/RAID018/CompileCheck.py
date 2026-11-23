"""Adds every .cs under Assets/ that the generated csproj missed, then compiles.
Unity rewrites *.csproj from the editor; in a fresh worktree new files are absent,
so headless verification would silently skip them. Run from the repo root:
    python Tools/RAID018/CompileCheck.py [Assembly-CSharp|Assembly-CSharp-Editor|Raid.Progression.Tests]
"""
import os, re, subprocess, sys

root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
project = sys.argv[1] if len(sys.argv) > 1 else "Assembly-CSharp"
path = os.path.join(root, project + ".csproj")
text = open(path, encoding="utf-8-sig").read()
included = {m.replace("/", "\\").lower() for m in re.findall(r'<Compile Include="([^"]+)"', text)}
added = []
for base, _, files in os.walk(os.path.join(root, "Assets")):
    for name in files:
        if not name.endswith(".cs"):
            continue
        rel = os.path.relpath(os.path.join(base, name), root)
        if rel.lower() in included:
            continue
        is_editor = "\Editor\\" in rel or "\Tests\\" in rel
        if project == "Assembly-CSharp" and is_editor:
            continue
        if project == "Assembly-CSharp-Editor" and not is_editor:
            continue
        is_tests = project == "Raid.Progression.Tests"
        if is_tests != ("\Tests\\" in rel):
            continue
        added.append(rel)
if added:
    marker = "</ItemGroup>"
    block = "".join(f'    <Compile Include="{a}" />\n' for a in added)
    text = text.replace(marker, block + "  " + marker, 1)
    open(path, "w", encoding="utf-8", newline="\r\n").write(text)
    print(f"{project}: added {len(added)} file(s)")
result = subprocess.run(["dotnet", "build", project + ".csproj", "-c", "Debug", "-nologo", "-v", "q",
                         f"-p:OutputPath=Temp/dotnet-check-{project}/"], cwd=root)
sys.exit(result.returncode)
