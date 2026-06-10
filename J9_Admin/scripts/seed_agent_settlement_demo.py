#!/usr/bin/env python3
"""为代理周结算页插入演示数据：会员、本周/上周投注流水、结算快照。"""

from __future__ import annotations

import sqlite3
from datetime import datetime, timedelta
from pathlib import Path

DB_PATH = Path(__file__).resolve().parents[1] / "buyu.db"

SOURCE_RATE = 0.008
PARENT_RATE = 0.005
GRAND_RATE = 0.002

GAME_MULTIPLIERS = {
    1: 0.2,  # Live
    5: 0.2,  # Sports
}

AGENTS = {
    "A21": 801041164128325,
    "A22": 801470129250373,
    "A23": 801476690260037,
    "A2": 801035647934533,
    "ABCD": 797683928424517,
}

GAMES = {
    "card": (797927555551301, 6),
    "electronic": (798207488364613, 3),
    "live": (798205652271173, 1),
}


def round_money(value: float) -> float:
    return round(value + 1e-9, 2)


def normalize_week_start(day: datetime) -> datetime:
    local = day.replace(hour=0, minute=0, second=0, microsecond=0)
    return local - timedelta(days=local.weekday())


def build_week_key(week_start: datetime) -> str:
    iso = week_start.isocalendar()
    return f"{iso.year}-W{iso.week:02d}"


def local_to_unix(wall: datetime) -> int:
    return int(wall.timestamp())


def next_id(cursor: sqlite3.Cursor, start: int = 910_000_000_000_001) -> int:
    tables = ["SysUser", "ddd_transaction", "ddd_agent_weekly_settlement"]
    max_id = start - 1
    for table in tables:
        row = cursor.execute(f"SELECT MAX(Id) FROM {table}").fetchone()
        if row and row[0] and row[0] > max_id:
            max_id = row[0]
    return max_id + 1


def game_multiplier(game_type: int) -> float:
    return GAME_MULTIPLIERS.get(game_type, 1.0)


def main() -> None:
    if not DB_PATH.exists():
        raise SystemExit(f"数据库不存在: {DB_PATH}")

    now = datetime.now()
    this_week = normalize_week_start(now)
    last_week = this_week - timedelta(days=7)
    weeks = [("本周", this_week), ("上周", last_week)]

    conn = sqlite3.connect(DB_PATH)
    conn.row_factory = sqlite3.Row
    cursor = conn.cursor()

    agent_rows = {
        row["Id"]: row
        for row in cursor.execute("SELECT Id, AgentName, ParentId FROM ddd_agent")
    }

    demo_members = [
        ("13900000011", "A21", [3200, 1800, 950]),
        ("13900000012", "A21", [2100, 760]),
        ("13900000021", "A22", [5400, 1200, 880, 430]),
        ("13900000022", "A22", [1500]),
        ("13900000031", "A23", [2800, 1650]),
        ("13900000032", "A23", [990, 660, 420]),
    ]

    member_ids: dict[str, int] = {}
    next_member_id = next_id(cursor)

    for username, agent_key, _ in demo_members:
        existing = cursor.execute(
            "SELECT Id FROM SysUser WHERE Username = ?", (username,)
        ).fetchone()
        agent_id = AGENTS[agent_key]
        if existing:
            member_ids[username] = existing["Id"]
            cursor.execute(
                "UPDATE SysUser SET DAgentId = ?, IsEnabled = 1 WHERE Id = ?",
                (agent_id, existing["Id"]),
            )
            continue

        member_id = next_member_id
        next_member_id += 1
        member_ids[username] = member_id
        created_at = now.strftime("%Y-%m-%d %H:%M:%S")
        cursor.execute(
            """
            INSERT INTO SysUser (
                Id, Username, Nickname, Password, IsEnabled, LoginTime, OrgId, IsSystem,
                SyncTime, ParentId, InviteCode, CreditAmount, IsRebateSwitch, UpdatedTime,
                ContinuousCheckInDays, ActivityPoint, DAgentId, RegisterIp, CreatedTime
            ) VALUES (
                ?, ?, ?, ?, 1, '0001-01-01 00:00:00', 0, 0,
                '0001-01-01 00:00:00', 0, ?, 1000, 1, '0001-01-01 00:00:00',
                0, 0, ?, '127.0.0.1', ?
            )
            """,
            (
                member_id,
                username,
                f"演示{username[-2:]}",
                "123456",
                f"DEMO{username[-4:]}",
                agent_id,
                created_at,
            ),
        )

    next_tx_id = next_id(cursor)
    next_settlement_id = next_id(cursor)
    tx_count = 0
    settlement_count = 0

    game_cycle = list(GAMES.values())

    for week_label, week_start in weeks:
        week_end = week_start + timedelta(days=7)
        week_key = build_week_key(week_start)
        from_unix = local_to_unix(week_start)
        to_unix = local_to_unix(week_end)

        cursor.execute(
            "DELETE FROM ddd_agent_weekly_settlement WHERE WeekKey = ? AND CreatedUserName = 'seed_agent_settlement_demo'",
            (week_key,),
        )

        for member_index, (username, agent_key, bet_amounts) in enumerate(demo_members):
            member_id = member_ids[username]
            source_agent_id = AGENTS[agent_key]
            source_agent = agent_rows[source_agent_id]
            parent_agent = (
                agent_rows.get(source_agent["ParentId"])
                if source_agent["ParentId"] > 0
                else None
            )
            grand_agent = (
                agent_rows.get(parent_agent["ParentId"])
                if parent_agent and parent_agent["ParentId"] > 0
                else None
            )

            group_rows = []
            for bet_index, bet_amount in enumerate(bet_amounts):
                game_id, game_type = game_cycle[(member_index + bet_index) % len(game_cycle)]
                bet_time = week_start + timedelta(
                    days=1 + bet_index,
                    hours=10 + member_index,
                    minutes=15 * bet_index,
                )
                valid_bet = round_money(bet_amount * 0.95)
                tx_id = next_tx_id
                next_tx_id += 1
                bill_no = f"DEMO-{week_key}-{username}-{bet_index}"
                cursor.execute(
                    """
                    INSERT INTO ddd_transaction (
                        Id, TransactionType, BeforeAmount, AfterAmount, BetAmount, ActualAmount,
                        ValidBetAmount, CurrencyCode, SerialNumber, BillNo, PlayName, GameRound,
                        Data, TransactionTime, Status, Description, IsRebate,
                        DMemberId, DGameId, DAgentId, RelatedTransActionId,
                        CreatedUserName, CreatedTime
                    ) VALUES (
                        ?, 2, 1000, ?, ?, ?, ?, 'CNY', ?, ?, ?, '', '', ?, 0,
                        '代理结算演示数据', 0, ?, ?, ?, 0, 'seed_agent_settlement_demo', ?
                    )
                    """,
                    (
                        tx_id,
                        1000 - bet_amount,
                        bet_amount,
                        -bet_amount,
                        valid_bet,
                        bill_no,
                        bill_no,
                        username,
                        local_to_unix(bet_time),
                        member_id,
                        game_id,
                        source_agent_id,
                        bet_time.strftime("%Y-%m-%d %H:%M:%S"),
                    ),
                )
                group_rows.append((bet_amount, valid_bet, game_type))
                tx_count += 1

            turnover = round_money(sum(x[0] for x in group_rows))
            valid_total = round_money(sum(x[1] for x in group_rows))
            source_rebate = round_money(
                sum(x[0] * SOURCE_RATE * game_multiplier(x[2]) for x in group_rows)
            )
            parent_rebate = (
                round_money(
                    sum(x[0] * PARENT_RATE * game_multiplier(x[2]) for x in group_rows)
                )
                if parent_agent
                else 0
            )
            grand_rebate = (
                round_money(
                    sum(x[0] * GRAND_RATE * game_multiplier(x[2]) for x in group_rows)
                )
                if grand_agent
                else 0
            )
            total_rebate = round_money(source_rebate + parent_rebate + grand_rebate)

            status = 0
            if week_label == "上周" and username.endswith("1"):
                status = 1
            if week_label == "上周" and username.endswith("2"):
                status = 2

            settlement_id = next_settlement_id
            next_settlement_id += 1
            cursor.execute(
                """
                INSERT INTO ddd_agent_weekly_settlement (
                    Id, WeekStartDate, WeekEndDate, WeekKey, DMemberId, MemberName,
                    SourceAgentId, SourceAgentName, ParentAgentId, ParentAgentName,
                    GrandAgentId, GrandAgentName, TurnoverAmount, ValidBetAmount,
                    BetTransactionCount, SourceRate, ParentRate, GrandRate,
                    SourceRebateAmount, ParentRebateAmount, GrandRebateAmount,
                    TotalRebateAmount, FromUnixTime, ToUnixTime, RuleVersion, Status,
                    CreatedUserName, CreatedTime
                ) VALUES (
                    ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?
                )
                """,
                (
                    settlement_id,
                    week_start.strftime("%Y-%m-%d %H:%M:%S"),
                    week_end.strftime("%Y-%m-%d %H:%M:%S"),
                    week_key,
                    member_id,
                    f"演示{username[-2:]}",
                    source_agent_id,
                    source_agent["AgentName"],
                    parent_agent["Id"] if parent_agent else 0,
                    parent_agent["AgentName"] if parent_agent else "",
                    grand_agent["Id"] if grand_agent else 0,
                    grand_agent["AgentName"] if grand_agent else "",
                    turnover,
                    valid_total,
                    len(group_rows),
                    SOURCE_RATE,
                    PARENT_RATE,
                    GRAND_RATE,
                    source_rebate,
                    parent_rebate,
                    grand_rebate,
                    total_rebate,
                    from_unix,
                    to_unix,
                    "weekly-agent-rebate-v2",
                    status,
                    "seed_agent_settlement_demo",
                    now.strftime("%Y-%m-%d %H:%M:%S"),
                ),
            )
            settlement_count += 1

    conn.commit()
    conn.close()

    print(f"数据库: {DB_PATH}")
    print(f"已写入演示会员 {len(demo_members)} 个")
    print(f"已写入投注流水 {tx_count} 笔")
    print(f"已写入周结算 {settlement_count} 条（本周 + 上周）")
    print(f"本周: {build_week_key(this_week)}，上周: {build_week_key(last_week)}")
    print("刷新后台「代理周结算」页面即可查看。")


if __name__ == "__main__":
    main()
