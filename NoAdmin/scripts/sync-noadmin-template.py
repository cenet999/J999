#!/usr/bin/env python3
"""将 NoAdmin 同步到 NoAdmin.Templates/content/NoAdminApp（应用模板命名规则）。"""

from __future__ import annotations

import os
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "NoAdmin"
DST = ROOT / "NoAdmin.Templates" / "content" / "NoAdminApp"
TESTS_SRC = ROOT / "NoAdmin.Tests"
TESTS_DST = ROOT / "NoAdmin.Templates" / "content" / "NoAdminApp.Tests"
SLN_DST = ROOT / "NoAdmin.Templates" / "content" / "NoAdminApp.sln"

SKIP_DIRS = {"bin", "obj", ".template.config"}
SKIP_FILES = {".DS_Store", "README.md", "NoAdmin.csproj"}
SKIP_GLOBS = ("noadmin.db",)

# 模板独立 Docker 构建，不覆盖
KEEP_TEMPLATE_FILES = {
    DST / "Dockerfile",
    DST / "docker-compose.yaml",
    DST / "NoAdminApp.csproj",
    DST / "Api" / "APITest.cs",
}


def to_template_text(text: str) -> str:
    text = text.replace("NoAdmin.Blazor", "\x00BLAZOR\x00")
    text = re.sub(r"\bNoAdmin\.(API|SeedData|Components)\b", r"NoAdminApp.\1", text)
    text = re.sub(r"\bnamespace NoAdmin\.", "namespace NoAdminApp.", text)
    text = re.sub(r"\busing NoAdmin\.", "using NoAdminApp.", text)
    text = re.sub(r"@using NoAdmin\.", "@using NoAdminApp.", text)
    text = re.sub(r"@using NoAdmin\b", "@using NoAdminApp", text)
    text = re.sub(r"\busing NoAdmin;", "using NoAdminApp;", text)
    text = re.sub(r"\bNoAdmin\.csproj\b", "NoAdminApp.csproj", text)
    text = re.sub(r"释放 NoAdmin 开发", "释放 NoAdminApp 开发", text)
    return text.replace("\x00BLAZOR\x00", "NoAdmin.Blazor")


def should_skip(rel: Path) -> bool:
    parts = rel.parts
    if any(p in SKIP_DIRS for p in parts):
        return True
    if rel.name in SKIP_FILES:
        return True
    if rel.name.startswith("noadmin.db"):
        return True
    if "wwwroot" in parts and "uploads" in parts:
        return True
    return False


def sync_tree(src_root: Path, dst_root: Path, transform: bool = True) -> list[str]:
    copied: list[str] = []
    for dirpath, dirnames, filenames in os.walk(src_root):
        dirnames[:] = sorted(d for d in dirnames if d not in SKIP_DIRS)
        for name in sorted(filenames):
            rel = Path(dirpath).relative_to(src_root) / name
            if should_skip(rel):
                continue
            src = src_root / rel
            dst = dst_root / rel
            if dst.resolve() in {p.resolve() for p in KEEP_TEMPLATE_FILES}:
                continue
            dst.parent.mkdir(parents=True, exist_ok=True)
            if transform and src.suffix in {
                ".cs",
                ".razor",
                ".cshtml",
                ".json",
                ".sh",
                ".yaml",
                ".yml",
                ".css",
                ".js",
                ".md",
            }:
                raw = src.read_text(encoding="utf-8-sig")
                content = to_template_text(raw).replace("\r\n", "\n").replace("\r", "\n")
                dst.write_text(content, encoding="utf-8", newline="\n")
            else:
                shutil.copy2(src, dst)
            copied.append(str(rel))
    return copied


def patch_imports(dst_root: Path) -> None:
    imports = dst_root / "Components" / "_Imports.razor"
    text = imports.read_text(encoding="utf-8")
    if "@namespace NoAdminApp.Components" not in text:
        text = "@namespace NoAdminApp.Components\n" + text
    imports.write_text(text, encoding="utf-8")


def patch_csproj() -> None:
    path = DST / "NoAdminApp.csproj"
    text = path.read_text(encoding="utf-8")
    if "GenerateDocumentationFile" not in text:
        text = text.replace(
            "<ImplicitUsings>enable</ImplicitUsings>",
            "<ImplicitUsings>enable</ImplicitUsings>\n"
            "    <GenerateDocumentationFile>true</GenerateDocumentationFile>\n"
            "    <DefaultItemExcludes>$(DefaultItemExcludes);noadmin.db*;**/noadmin.db*;$(MSBuildProjectName).Tests/**</DefaultItemExcludes>",
        )
    if "Rougamo.Fody" not in text:
        text = text.replace(
            "<ItemGroup>\n",
            "<ItemGroup>\n"
            '    <PackageReference Include="Rougamo.Fody" Version="5.0.2" />\n',
            1,
        )
    path.write_text(text, encoding="utf-8")


def remove_stale(dst_root: Path, src_root: Path) -> list[str]:
    removed: list[str] = []
    for dirpath, dirnames, filenames in os.walk(dst_root):
        if ".template.config" in Path(dirpath).parts:
            continue
        dirnames[:] = [d for d in dirnames if d not in SKIP_DIRS]
        for name in filenames:
            rel = Path(dirpath).relative_to(dst_root) / name
            if should_skip(rel):
                continue
            dst = dst_root / rel
            if dst.resolve() in {p.resolve() for p in KEEP_TEMPLATE_FILES}:
                continue
            src = src_root / rel
            if not src.exists():
                dst.unlink()
                removed.append(str(rel))
    stale_dir = dst_root / "Components" / "ComponentDemo"
    if stale_dir.is_dir() and not any(stale_dir.iterdir()):
        stale_dir.rmdir()
        removed.append("Components/ComponentDemo/")
    return removed


def sync_tests() -> None:
    for name in ("ApiFlowTests.cs", "FileCacheAttributeTests.cs", "FodyWeavers.xml", "FodyWeavers.xsd"):
        src = TESTS_SRC / name
        if src.exists():
            shutil.copy2(src, TESTS_DST / name)


def sync_solution() -> None:
    text = """\
Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NoAdminApp", "NoAdminApp\\NoAdminApp.csproj", "{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
EndProject
Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NoAdminApp.Tests", "NoAdminApp.Tests\\NoAdminApp.Tests.csproj", "{B2C3D4E5-F6A7-8901-BCDE-F12345678901}"
EndProject
Global
\tGlobalSection(SolutionConfigurationPlatforms) = preSolution
\t\tDebug|Any CPU = Debug|Any CPU
\t\tRelease|Any CPU = Release|Any CPU
\tEndGlobalSection
\tGlobalSection(ProjectConfigurationPlatforms) = postSolution
\t\t{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
\t\t{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Debug|Any CPU.Build.0 = Debug|Any CPU
\t\t{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.ActiveCfg = Release|Any CPU
\t\t{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}.Release|Any CPU.Build.0 = Release|Any CPU
\t\t{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
\t\t{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Debug|Any CPU.Build.0 = Debug|Any CPU
\t\t{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Release|Any CPU.ActiveCfg = Release|Any CPU
\t\t{B2C3D4E5-F6A7-8901-BCDE-F12345678901}.Release|Any CPU.Build.0 = Release|Any CPU
\tEndGlobalSection
\tGlobalSection(SolutionProperties) = preSolution
\t\tHideSolutionNode = FALSE
\tEndGlobalSection
EndGlobal
"""
    SLN_DST.write_text(text, encoding="utf-8", newline="\n")


def main() -> None:
    copied = sync_tree(SRC, DST)
    patch_imports(DST)
    patch_csproj()
    removed = remove_stale(DST, SRC)
    sync_tests()
    sync_solution()
    print(f"已同步 {len(copied)} 个文件到模板")
    if removed:
        print(f"已删除模板中过时文件 {len(removed)} 个: {', '.join(removed[:10])}")


if __name__ == "__main__":
    main()
