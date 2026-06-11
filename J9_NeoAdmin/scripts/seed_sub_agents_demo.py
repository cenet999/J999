#!/usr/bin/env python3
"""为指定代理添加下级代理及可登录后台的演示账号。"""

from __future__ import annotations

import sqlite3
from datetime import datetime
from pathlib import Path

DB_PATH = Path(__file__).resolve().parents[1] / "buyu.db"

# 当前后台登录账号 13900000011 对应代理 A21
DEFAULT_PARENT_AGENT_ID = 801041164128325
AGENT_ROLE_ID = 801038584258629

SUB_AGENTS = [
    ("A211", "13900000111"),
    ("A212", "13900000112"),
    ("A213", "13900000113"),
]


def next_id(cursor: sqlite3.Cursor, start: int = 920_000_000_000_001) -> int:
    tables = ["ddd_agent", "SysUser"]
    max_id = start - 1
    for table in tables:
        row = cursor.execute(f"SELECT MAX(Id) FROM {table}").fetchone()
        if row and row[0] and row[0] > max_id:
            max_id = row[0]
    return max_id + 1


def insert_sys_user(
    cursor: sqlite3.Cursor,
    user_id: int,
    username: str,
    agent_id: int,
    created_at: str,
) -> None:
    cursor.execute(
        """
        INSERT INTO SysUser (
            Id, Username, Nickname, Password, IsEnabled, LoginTime, OrgId, IsSystem,
            SyncTime, ParentId, InviteCode, CreditAmount, IsRebateSwitch, UpdatedTime,
            ContinuousCheckInDays, ActivityPoint, DAgentId, RegisterIp, CreatedTime
        ) VALUES (
            ?, ?, ?, '123456', 1, '0001-01-01 00:00:00', 0, 0,
            '0001-01-01 00:00:00', 0, ?, 0, 1, '0001-01-01 00:00:00',
            0, 0, ?, '127.0.0.1', ?
        )
        """,
        (user_id, username, username, f"AG{username[-4:]}", agent_id, created_at),
    )
    cursor.execute(
        "INSERT OR IGNORE INTO SysRoleUser (RoleId, UserId) VALUES (?, ?)",
        (AGENT_ROLE_ID, user_id),
    )


def main() -> None:
    if not DB_PATH.exists():
        raise SystemExit(f"数据库不存在: {DB_PATH}")

    now = datetime.now()
    created_at = now.strftime("%Y-%m-%d %H:%M:%S")

    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()

    parent = cursor.execute(
        "SELECT Id, AgentName FROM ddd_agent WHERE Id = ?",
        (DEFAULT_PARENT_AGENT_ID,),
    ).fetchone()
    if not parent:
        raise SystemExit(f"上级代理不存在: {DEFAULT_PARENT_AGENT_ID}")

    next_agent_id = next_id(cursor)
    next_user_id = next_id(cursor) + 100
    created_agents: list[tuple[str, int, str]] = []

    for agent_name, login_username in SUB_AGENTS:
        existing = cursor.execute(
            "SELECT Id FROM ddd_agent WHERE AgentName = ?", (agent_name,)
        ).fetchone()
        if existing:
            agent_id = existing["Id"]
            cursor.execute(
                "UPDATE ddd_agent SET ParentId = ?, IsEnabled = 1 WHERE Id = ?",
                (parent["Id"], agent_id),
            )
        else:
            agent_id = next_agent_id
            next_agent_id += 1
            cursor.execute(
                """
                INSERT INTO ddd_agent (
                    Id, AgentName, ParentId, AgentType, IsEnabled, TelegramChatId,
                    RebateRate, Remark, CreatedUserName, CreatedTime
                ) VALUES (?, ?, ?, 0, 1, '', 0.008, ?, 'seed_sub_agents_demo', ?)
                """,
                (
                    agent_id,
                    agent_name,
                    parent["Id"],
                    f"{parent['AgentName']} 的下级演示代理",
                    created_at,
                ),
            )

        user = cursor.execute(
            "SELECT Id FROM SysUser WHERE Username = ?", (login_username,)
        ).fetchone()
        if user:
            cursor.execute(
                "UPDATE SysUser SET DAgentId = ?, IsEnabled = 1, Password = '123456' WHERE Id = ?",
                (agent_id, user["Id"]),
            )
            cursor.execute(
                "INSERT OR IGNORE INTO SysRoleUser (RoleId, UserId) VALUES (?, ?)",
                (AGENT_ROLE_ID, user["Id"]),
            )
        else:
            user_id = next_user_id
            next_user_id += 1
            insert_sys_user(cursor, user_id, login_username, agent_id, created_at)

        created_agents.append((agent_name, agent_id, login_username))

    conn.commit()
    conn.close()

    print(f"数据库: {DB_PATH}")
    print(f"上级代理: {parent['AgentName']} ({parent['Id']})")
    print("已添加/更新下级代理:")
    for name, agent_id, username in created_agents:
        print(f"  - {name} (Id={agent_id})，后台账号 {username} / 123456")
    print("用 A21 账号刷新「代理管理」即可看到下级代理。")


if __name__ == "__main__":
    main()
