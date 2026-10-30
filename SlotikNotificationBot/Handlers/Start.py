from aiogram import Router
from aiogram.filters import CommandStart
from aiogram.filters.command import CommandObject
from aiogram.types import Message

from Services.BackendLink import link_telegram


router = Router()


@router.message(CommandStart())
async def start_handler(
    message: Message,
    command: CommandObject,
):
    token = command.args

    if not token:
        await message.answer(
            "Щоб підключити Telegram, "
            "перейдіть у бот через ваш акаунт Slotik."
        )
        return

    if message.from_user is None:
        return

    success = await link_telegram(
        token=token,
        chat_id=message.chat.id,
        telegram_user_id=message.from_user.id,
        username=message.from_user.username,
    )

    if not success:
        await message.answer(
            "❌ Посилання недійсне, "
            "прострочене або вже було використане."
        )
        return

    await message.answer(
        "✅ Telegram успішно підключено "
        "до вашого акаунта Slotik."
    )