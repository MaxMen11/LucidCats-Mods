from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET

root = Path(__file__).resolve().parents[2]
errors = []
projects = sorted((root / "mods").glob("*/*.csproj")) + sorted((root / "libs/runtime").glob("*/*.csproj"))
project_names = [project.stem for project in projects]

if len(project_names) != len(set(project_names)):
    errors.append("Project names must be unique across mods and runtime libraries.")

guids = []
for project in projects:
    try:
        xml = ET.parse(project).getroot()
    except ET.ParseError as error:
        errors.append(f"{project.relative_to(root)}: invalid XML: {error}")
        continue

    properties = {element.tag: (element.text or "").strip() for group in xml.findall("PropertyGroup") for element in group}
    guid = properties.get("Guid") or f"com.lucidcats.{project.stem.lower()}"
    if not re.fullmatch(r"[A-Za-z0-9._-]+", guid):
        errors.append(f"{project.relative_to(root)}: invalid Guid '{guid}'.")
    if guid in guids:
        errors.append(f"Duplicate Guid '{guid}'.")
    guids.append(guid)

    version = properties.get("Version", "1.0.0")
    if not re.fullmatch(r"\d+\.\d+(?:\.\d+){0,2}", version):
        errors.append(f"{project.relative_to(root)}: invalid BepInEx 5 Version '{version}'.")

    for property_name, directory in (("SourceLibs", root / "libs/source"), ("RuntimeLibs", root / "libs/runtime")):
        names = [name for name in properties.get(property_name, "").split(";") if name]
        if len(names) != len(set(names)):
            errors.append(f"{project.relative_to(root)}: {property_name} contains duplicates.")
        for name in names:
            if not (directory / name).is_dir():
                errors.append(f"{project.relative_to(root)}: unknown {property_name} entry '{name}'.")

for path in root.rglob("*"):
    if not path.is_file() or any(part in {".git", "bin", "obj", "dist"} for part in path.parts):
        continue
    if path.suffix not in {".cs", ".csproj", ".props", ".targets", ".proj", ".json", ".cpp", ".h", ".nix", ".yml", ".yaml", ".md"}:
        continue
    try:
        text = path.read_text(encoding="utf-8")
    except UnicodeDecodeError:
        errors.append(f"{path.relative_to(root)}: not UTF-8.")
        continue
    if text and not text.endswith("\n"):
        errors.append(f"{path.relative_to(root)}: missing final newline.")
    if any(line.rstrip("\t ") != line for line in text.splitlines()):
        errors.append(f"{path.relative_to(root)}: trailing whitespace.")
    if path.suffix in {".cs", ".csproj", ".props", ".targets", ".proj", ".json", ".cpp", ".h", ".nix"}:
        if any(line.startswith(" ") for line in text.splitlines() if line.strip()):
            errors.append(f"{path.relative_to(root)}: indentation must use tabs.")

for required in ("README.md", "Directory.Build.props", "Directory.Build.targets", "build.proj"):
    if not (root / required).exists():
        errors.append(f"Missing {required}.")

if errors:
    print("Workspace check failed:")
    for error in errors:
        print(f"- {error}")
    sys.exit(1)

print(f"Workspace check passed for {len(projects)} projects.")
