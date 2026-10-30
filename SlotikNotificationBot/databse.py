import asyncpg

_pool: asyncpg.Pool | None = None


async def init_db(
    host: str,
    port: int,
    database: str,
    user: str,
    password: str,
):
    global _pool

    _pool = await asyncpg.create_pool(
        host=host,
        port=port,
        database=database,
        user=user,
        password=password,
        min_size=1,
        max_size=5,
    )


def get_pool() -> asyncpg.Pool:
    if _pool is None:
        raise RuntimeError(
            "Database pool is not initialized."
        )

    return _pool


async def close_db():
    global _pool

    if _pool is not None:
        await _pool.close()
        _pool = None