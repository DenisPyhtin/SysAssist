# SysAssist Telegram launcher bot

`scripts/sysassist-telegram-bot.ps1` is an external local control bot for presentation and workstation operation. It is intentionally outside the API process, so it can start SysAssist even when the API is not running.

## Configuration

Set these values in `.env` or secret storage:

```env
SYSASSIST_MODULE_TELEGRAM_BOT_BOTTOKEN=replace-with-botfather-token
SYSASSIST_MODULE_TELEGRAM_BOT_DEFAULTCHATID=
TELEGRAM_BOT_AUTO_REGISTER_FIRST_CHAT=true
TELEGRAM_BOT_ALLOWED_CHAT_IDS=
```

For a local presentation, leave `TELEGRAM_BOT_AUTO_REGISTER_FIRST_CHAT=true`, send `/start` to the bot, and the first chat is saved to `.run-logs/telegram-bot-state.json`. The bot also writes the chat id to `.env` and, when the API is ready, syncs the built-in `telegram-bot` module settings through the API so the token is encrypted by SysAssist secret protection.

For anything beyond a local demo, set `TELEGRAM_BOT_AUTO_REGISTER_FIRST_CHAT=false` and provide explicit `TELEGRAM_BOT_ALLOWED_CHAT_IDS`.

## Run

```powershell
powershell -ExecutionPolicy Bypass -File scripts\sysassist-telegram-bot.ps1
```

## Commands

- `/up` or `/start_sysassist` starts the API and Web UI if they are not already listening.
- `/status` shows API/Web readiness, database, and license status.
- `/diagnostics` starts SysAssist if needed, runs diagnostics, and returns current warnings.
- `/modules` returns module health.
- `/actions` returns the remediation action count.
- `/whoami` returns the Telegram chat id for allowlist setup.

The bot never prints the Telegram token. Logs are written under `.run-logs`.

Telegram API calls are executed through bundled Windows `curl.exe` with strict `max-time` limits. This avoids `Invoke-RestMethod` hangs during long polling or message delivery on some Windows PowerShell hosts.
