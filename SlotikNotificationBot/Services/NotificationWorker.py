import asyncio

from aiogram import Bot

from databse import get_pool

import html

POLL_INTERVAL_SECONDS = 5
MAX_ATTEMPTS = 5
BATCH_SIZE = 50


async def notification_worker(bot: Bot):
    while True:
        try:
            await process_notifications(bot)

        except asyncio.CancelledError:
            raise

        except Exception as ex:
            print(
                f"Notification worker error: {ex}"
            )

        await asyncio.sleep(POLL_INTERVAL_SECONDS)


async def process_notifications(bot: Bot):
    pool = get_pool()

    notifications = await pool.fetch(
        """
        SELECT
            n."Id",
            n."UserId",
            n."Message",
            n."SendAt",
            n."Attempts",
            b."ChatId"
        FROM "TelegramNotification" n
        INNER JOIN "TelegramBindings" b
            ON b."UserId" = n."UserId"
        WHERE
            n."SendAt" <= NOW()
            AND n."Attempts" < $1
        ORDER BY
            n."SendAt",
            n."Id"
        LIMIT $2
        """,
        MAX_ATTEMPTS,
        BATCH_SIZE,
    )

    for notification in notifications:
        try:
            await bot.send_message(
                chat_id=notification["ChatId"],
                text=format_notification( notification["Message"]),
                parse_mode="HTML"
            )


            await pool.execute(
                """
                DELETE FROM "TelegramNotification"
                WHERE "Id" = $1
                """,
                notification["Id"],
            )

            print(
                f"Notification {notification['Id']} sent."
            )

        except Exception as ex:

            await pool.execute(
                """
                UPDATE "TelegramNotification"
                SET "Attempts" = "Attempts" + 1
                WHERE "Id" = $1
                """,
                notification["Id"],
            )

            print(
                f"Failed to send notification "
                f"{notification['Id']}: {ex}"
            )

def format_notification(message: str) -> str:
    safe_message = html.escape(message)

    return (
        "🔔 <b>Нове сповіщення</b>\n\n"
        f"✨ {safe_message}\n\n"
        "────────────\n"
        "💙 <b>Slotik</b>\n"
        "<i>Зручно. Швидко. Поруч.</i>"
    )