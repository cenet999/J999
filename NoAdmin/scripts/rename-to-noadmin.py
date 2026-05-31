#!/usr/bin/env python3
"""浅层重命名：仓库/包/命名空间 NovaAdmin → NoAdmin，保留 NovaAdminTable 等 API 类名。"""

from __future__ import annotations

import os
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKIP_DIRS = {"bin", "obj", ".git", ".cursor"}
TEXT_SUFFIXES = {
    ".cs", ".csproj", ".sln", ".razor", ".json", ".md", ".py", ".sh", ".yaml", ".yml",
    ".css", ".html", ".txt", ".dockerignore", ".gitignore", ".xml",
}

REPLACEMENTS = [
    ("NoAdmin.Blazor", "NoAdmin.Blazor"),
    ("NoAdmin.Templates", "NoAdmin.Templates"),
    ("NoAdminApp", "NoAdminApp"),
    ("NoAdmin.Tests", "NoAdmin.Tests"),
    # 宿主命名空间（在 Blazor 已替换之后，避免误伤 NoAdmin.Blazor）
    ("namespace NoAdmin.", "namespace NoAdmin."),
    ("using NoAdmin.", "using NoAdmin."),
    ("@using NoAdmin.", "@using NoAdmin."),
    ("@using NoAdmin", "@using NoAdmin"),
    ("using NoAdmin;", "using NoAdmin;"),
    # 路径与工程文件
    ("NoAdmin/NoAdmin.csproj", "NoAdmin/NoAdmin.csproj"),
    ("NovaAdmin\\NoAdmin.csproj", "NoAdmin\\NoAdmin.csproj"),
    ("NoAdmin/Dockerfile", "NoAdmin/Dockerfile"),
    ("NovaAdmin\\Dockerfile", "NoAdmin\\Dockerfile"),
    ("../NoAdmin/", "../NoAdmin/"),
    ("../../NoAdmin/", "../../NoAdmin/"),
    ("NoAdmin.csproj", "NoAdmin.csproj"),
    ("sync-noadmin-template", "sync-noadmin-template"),
    ("noadmin.db", "noadmin.db"),
    ("shortName\": \"novaadmin\"", "shortName\": \"noadmin\""),
    ("NoAdmin.Template.CSharp", "NoAdmin.Template.CSharp"),
    # sln / 显示名（在 NoAdminApp 等已替换后）
    ('= "NoAdmin", "NoAdmin', '= "NoAdmin", "NoAdmin'),  # 已部分替换时跳过
    ('= "NoAdmin", "NoAdmin', '= "NoAdmin", "NoAdmin'),
    ('= "NoAdmin.Blazor"', '= "NoAdmin.Blazor"'),  # 若仍存在
    # 包版本重置在 csproj 里单独处理
    # 文案（非 API）
    ('"NoAdmin SaaS', '"NoAdmin SaaS'),
    ('Title = "NoAdmin"', 'Title = "NoAdmin"'),
    ('DocumentTitle = "NoAdmin', 'DocumentTitle = "NoAdmin'),
    ('Title = "NoAdmin WebAPI"', 'Title = "NoAdmin WebAPI"'),
    ("NoAdmin starting.", "NoAdmin starting."),
    ('author": "NoAdmin"', 'author": "NoAdmin"'),
    ('"NoAdmin 后台管理模板"', '"NoAdmin 后台管理模板"'),
    ("NoAdmin 的 dotnet new", "NoAdmin 的 dotnet new"),
    ("NoAdmin 后台", "NoAdmin 后台"),
]


def should_process(path: Path) -> bool:
    if any(part in SKIP_DIRS for part in path.parts):
        return False
    return path.suffix.lower() in TEXT_SUFFIXES or path.name in {
        "Dockerfile", "docker-compose.yaml", "AGENTS.md", "nuget.md",
    }


def apply_replacements(text: str) -> str:
    for old, new in REPLACEMENTS:
        text = text.replace(old, new)
    text = text.replace(
        'Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NoAdmin", "NoAdmin\\NoAdmin.csproj"',
        'Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NoAdmin", "NoAdmin\\NoAdmin.csproj"',
    )
    text = text.replace(
        'Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NoAdmin.Blazor", "NoAdmin.Blazor\\NoAdmin.Blazor.csproj"',
        'Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "NoAdmin.Blazor", "NoAdmin.Blazor\\NoAdmin.Blazor.csproj"',
    )
    return text


def patch_csproj_versions(text: str, path: Path) -> str:
    if path.name == "NoAdmin.Blazor.csproj":
        text = re.sub(r"<Version>[^<]+</Version>", "<Version>1.0.0</Version>", text)
        text = re.sub(r"<AssemblyName>[^<]+</AssemblyName>", "<AssemblyName>NoAdmin.Blazor</AssemblyName>", text)
        text = re.sub(r"<RootNamespace>[^<]+</RootNamespace>", "<RootNamespace>NoAdmin.Blazor</RootNamespace>", text)
        text = re.sub(r"<PackageId>[^<]+</PackageId>", "<PackageId>NoAdmin.Blazor</PackageId>", text)
        text = re.sub(r"<Title>[^<]+</Title>", "<Title>NoAdmin.Blazor</Title>", text)
        text = re.sub(r"<Product>[^<]+</Product>", "<Product>NoAdmin.Blazor</Product>", text)
    if path.name == "NoAdmin.Templates.csproj":
        text = re.sub(r"<Version>[^<]+</Version>", "<Version>1.0.0</Version>", text)
        text = re.sub(r"<PackageId>[^<]+</PackageId>", "<PackageId>NoAdmin.Templates</PackageId>", text)
        text = re.sub(r"<Title>[^<]+</Title>", "<Title>NoAdmin.Templates</Title>", text)
        text = re.sub(r"<Product>[^<]+</Product>", "<Product>NoAdmin.Templates</Product>", text)
    if path.name == "NoAdminApp.csproj":
        text = re.sub(
            r'<PackageReference Include="NovaAdmin\.Blazor" Version="[^"]+"',
            '<PackageReference Include="NoAdmin.Blazor" Version="1.0.0"',
            text,
        )
        text = re.sub(
            r'<PackageReference Include="NoAdmin\.Blazor" Version="[^"]+"',
            '<PackageReference Include="NoAdmin.Blazor" Version="1.0.0"',
            text,
        )
    if path.name == "NoAdminApp.Tests.csproj":
        text = re.sub(
            r'<PackageReference Include="NovaAdmin\.Blazor" Version="[^"]+"',
            '<PackageReference Include="NoAdmin.Blazor" Version="1.0.0"',
            text,
        )
        text = re.sub(
            r'<PackageReference Include="NoAdmin\.Blazor" Version="[^"]+"',
            '<PackageReference Include="NoAdmin.Blazor" Version="1.0.0"',
            text,
        )
    return text


def main() -> None:
    changed = 0
    for path in ROOT.rglob("*"):
        if not path.is_file() or not should_process(path):
            continue
        try:
            original = path.read_text(encoding="utf-8")
        except (UnicodeDecodeError, OSError):
            continue
        updated = apply_replacements(original)
        updated = patch_csproj_versions(updated, path)
        if updated != original:
            path.write_text(updated, encoding="utf-8")
            changed += 1
            print(f"updated: {path.relative_to(ROOT)}")
    print(f"done, {changed} files changed")


if __name__ == "__main__":
    main()
