#!/usr/bin/env python3
"""为 A21 的下级代理（A211/A212/A213）添加演示会员与投注流水。"""

from __future__ import annotations

import sqlite3
from datetime import datetime, timedelta
from pathlib import Path

DB_PATH = Path(__file__).resolve().parents[1] / "buyu.db"
SEED_TAG = "seed_sub_agent_members_demo"

SUB_AGENTS = {
    "A211": 920000000000001,
    "A212": 920000000000002,
    "A213": 920000000000003,
}

GAMES = [
    (797927555551301, 6),   # 棋牌
    (798207488364613, 3),   # 电子
    (798205652271173, 1),   # 真人
]

# 每个下级代理 2 名会员，各有多笔本周/上周投注
DEMO_MEMBERS = [
    ("13900002111", "A211", [2600, 1400, 720]),
    ("13900002112", "A211", [1850, 920]),
    ("13900002211", "A212", [4100, 1680, 550]),
    ("13900002212", "A212", [1320, 880]),
    ("13900002311", "A213", [3050, 1920, 640]),
    ("13900002312", "A213", [1180, 760, 390]),
]


def normalize_week_start(day: datetime) -> datetime:
    local = day.replace(hour=0, minute=0, second=0, microsecond=0)
    return local - timedelta(days=local.weekday())


def local_to_unix(wall: datetime) -> int:
    return int(wall.timestamp())


def next_id(cursor: sqlite3.Cursor, start: int = 930_000_000_000_001) -> int:
    max_id = start - 1
    for table in ("SysUser", "ddd_transaction"):
        row = cursor.execute(f"SELECT MAX(Id) FROM {table}").fetchone()
        if row and row[0] and row[0] > max_id:
            max_id = row[0]
    return max_id + 1


def ensure_member(
    cursor: sqlite3.Cursor,
    member_id: int,
    username: str,
    agent_id: int,
    created_at: str,
) -> int:
    existing = cursor.execute(
        "SELECT Id FROM SysUser WHERE Username = ?", (username,)
    ).fetchone()
    if existing:
        cursor.execute(
            "UPDATE SysUser SET DAgentId = ?, IsEnabled = 1, Password = '123456' WHERE Id = ?",
            (agent_id, existing["Id"]),
        )
        return existing["Id"]

    cursor.execute(
        """
        INSERT INTO SysUser (
            Id, Username, Nickname, Password, IsEnabled, LoginTime, OrgId, IsSystem,
            SyncTime, ParentId, InviteCode, CreditAmount, IsRebateSwitch, UpdatedTime,
            ContinuousCheckInDays, ActivityPoint, DAgentId, RegisterIp, CreatedTime
        ) VALUES (
            ?, ?, ?, '123456', 1, '0001-01-01 00:00:00', 0, 0,
            '0001-01-01 00:00:00', 0, ?, 2000, 1, '0001-01-01 00:00:00',
            0, 0, ?, '127.0.0.1', ?
        )
        """,
        (
            member_id,
            username,
            f"会员{username[-2:]}",
            f"M{username[-4:]}",
            agent_id,
            created_at,
        ),
    )
    return member_id


def main() -> None:
    if not DB_PATH.exists():
        raise SystemExit(f"数据库不存在: {DB_PATH}")

    now = datetime.now()
    created_at = now.strftime("%Y-%m-%d %H:%M:%S")
    this_week = normalize_week_start(now)
    last_week = this_week - timedelta(days=7)
    weeks = [this_week, last_week]

    conn = sqlite3.connect(DB_PATH)
    cursor = conn.cursor()

    # 清理本脚本之前写入的演示流水
    cursor.execute(
        f"DELETE FROM ddd_transaction WHERE CreatedUserName = '{SEED_TAG}'"
    )

    next_member_id = next_id(cursor)
    next_tx_id = next_id(cursor) + 1000
    member_count = 0
    tx_count = 0

    for member_index, (username, agent_key, bet_amounts) in enumerate(DEMO_MEMBERS):
        agent_id = SUB_AGENTS[agent_key]
        member_id = ensure_member(
            cursor, next_member_id, username, agent_id, created_at
        )
        if member_id == next_member_id:
            next_member_id += 1
        member_count += 1

        for week_index, week_start in enumerate(weeks):
            for bet_index, bet_amount in enumerate(bet_amounts):
                game_id, _game_type = GAMES[(member_index + bet_index) % len(GAMES)]
                bet_time = week_start + timedelta(
                    days=1 + bet_index,
                    hours=9 + member_index,
                    minutes=20 * bet_index,
                )
                valid_bet = round(bet_amount * 0.95, 2)
                bill_no = f"SUB-{agent_key}-{username}-{week_index}-{bet_index}"
                cursor.execute(
                    """
                    INSERT INTO ddd_transaction (
                        Id, TransactionType, BeforeAmount, AfterAmount, BetAmount, ActualAmount,
                        ValidBetAmount, CurrencyCode, SerialNumber, BillNo, PlayName, GameRound,
                        Data, TransactionTime, Status, Description, IsRebate,
                        DMemberId, DGameId, DAgentId, RelatedTransActionId,
                        CreatedUserName, CreatedTime
                    ) VALUES (
                        ?, 2, 2000, ?, ?, ?, ?, 'CNY', ?, ?, ?, '', '', ?, 0,
                        '下级代理演示投注', 0, ?, ?, ?, 0, ?, ?
                    )
                    """,
                    (
                        next_tx_id,
                        2000 - bet_amount,
                        bet_amount,
                        -bet_amount,
                        valid_bet,
                        bill_no,
                        bill_no,
                        username,
                        local_to_unix(bet_time),
                        member_id,
                        game_id,
                        agent_id,
                        SEED_TAG,
                        bet_time.strftime("%Y-%m-%d %H:%M:%S"),
                    ),
                )
                next_tx_id += 1
                tx_count += 1

    conn.commit()
    conn.close()

    print(f"数据库: {DB_PATH}")
    print(f"演示会员: {member_count} 个（归属 A211 / A212 / A213）")
    print(f"投注流水: {tx_count} 笔（本周 + 上周）")
    print("会员账号密码均为 123456，可在后台会员管理或代理结算中查看。")


if __name__ == "__main__":
    main()
