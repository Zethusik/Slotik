import asyncio
import os

from aiogram import Bot, Dispatcher
from dotenv import load_dotenv

from databse import init_db, close_db
from Handlers.Start import router

from Services.NotificationWorker import notification_worker

async def main():
    load_dotenv()

    bot = Bot(
        token=os.environ["TELEGRAM_BOT_TOKEN"]
    )

    dp = Dispatcher()

    dp.include_router(router)

    await init_db(
        host=os.environ["DB_HOST"],
        port=int(
            os.environ["DB_PORT"]
        ),
        database=os.environ["DB_NAME"],
        user=os.environ["DB_USER"],
        password=os.environ["DB_PASSWORD"],
    )

    worker_task = asyncio.create_task(notification_worker(bot))

    try:
        await dp.start_polling(bot)

    finally:

        worker_task.cancel()

        try:
            await worker_task
        except asyncio.CancelledError:
            pass
        await close_db()
        await bot.session.close()


if __name__ == "__main__":
    asyncio.run(main())