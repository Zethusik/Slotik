from databse import get_pool
from datetime import datetime, timezone
import hashlib

async def link_telegram(
    token: str,
    chat_id: int,
    telegram_user_id: int,
    username: str | None,
) -> bool:



    pool = get_pool()

    async with pool.acquire() as connection:
        async with connection.transaction():

            token_hash = hashlib.sha256(
                token.encode("utf-8")
            ).hexdigest()

            link_token = await connection.fetchrow(
                """
                SELECT
                    "Id",
                    "UserId",
                    "ExpiresAt",
                    "UsedAt"
                FROM "TelegramLinkTokens"
                WHERE "Token" = $1
                FOR UPDATE
                """,
                token_hash,
            )

            if link_token is None:
                return False

            if link_token["UsedAt"] is not None:
                return False

            now = datetime.now(timezone.utc)

            if link_token["ExpiresAt"] <= now:
                return False

            user_id = link_token["UserId"]

            await connection.execute(
                """
                INSERT INTO "TelegramBindings"
                (
                    "UserId",
                    "ChatId",
                    "TelegramUserId",
                    "Username",
                    "LinkedAt"
                )
                VALUES
                (
                    $1,
                    $2,
                    $3,
                    $4,
                    NOW()
                )
                ON CONFLICT ("UserId")
                DO UPDATE SET
                    "ChatId" = EXCLUDED."ChatId",
                    "TelegramUserId" = EXCLUDED."TelegramUserId",
                    "Username" = EXCLUDED."Username",
                    "LinkedAt" = NOW()
                """,
                user_id,
                chat_id,
                telegram_user_id,
                username,
            )

            await connection.execute(
                """
                UPDATE "TelegramLinkTokens"
                SET "UsedAt" = NOW()
                WHERE "Id" = $1
                """,
                link_token["Id"],
            )

            return True