# Contract: Telegram Bot Commands

**Feature**: `specs/001-poll-automator-monitor`  
**Language**: Ukrainian (`uk-UA`)  
**Authorization**: Committee Admin (`GroupMember.role == Admin && isActive == true`).

---

## 1. Command Specifications

### 1.0 `/start` and `/help`
Greets the user and outputs instructions on available commands.

- **Syntax**: `/start` or `/help`
- **Authorization**: Public (any chat participant or direct message user).
- **Outbound Telegram Call**:
  - Method: `sendMessage`
  - Parameters:
    - `chat_id`: Current chat ID.
    - `text`: Ukrainian welcome text explaining `/create_poll`, `/close_poll`, and `/cancel_poll`.

### 1.1 `/create_poll`
Creates a transparent, non-anonymous poll in the group chat.

- **Syntax**: `/create_poll "<question>" "<option1>" "<option2>" ... [hours]`
- **Arguments**:
  - `question` (quoted string, required): Title of the poll.
  - `options` (quoted strings, required): 2 to 10 choices.
  - `hours` (integer, optional): Duration until automatic closure. Defaults to `24`. Must be between `1` and `168`.
- **Preconditions**:
  - Sender must be an authorized committee admin in the group.
  - No poll currently in `Open` or `Reminded` status for this chat.
- **Outbound Telegram Call**:
  - Method: `sendPoll`
  - Parameters:
    - `chat_id`: Current chat ID.
    - `question`: Provided question.
    - `options`: Provided choices serialized as JSON array.
    - `is_anonymous`: `false` (MANDATORY).
    - `allows_multiple_answers`: `false` (default).
- **Error Responses (Ukrainian)**:
  - Unauthorized: `"⛔ Тільки члени батьківського комітету можуть створювати голосування."`
  - Active poll exists: `"⚠️ У групі вже є активне голосування: «{Question}». Завершіть його перед створенням нового."`
  - Invalid arguments: `"ℹ️ Використання: /create_poll \"Питання\" \"Варіант 1\" \"Варіант 2\" [години]"`

---

### 1.2 `/close_poll`
Stops voting immediately ahead of schedule and publishes final aggregated results.

- **Syntax**: `/close_poll`
- **Preconditions**:
  - Sender must be an authorized committee admin in the group.
  - An active poll (`Open` or `Reminded`) must exist in the chat.
- **Outbound Telegram Calls**:
  1. `stopPoll(chat_id, message_id)`: Finalizes poll voting in Telegram.
  2. `sendMessage(chat_id, text, parse_mode: "MarkdownV2")`: Publishes aggregated results summary.
- **Group Summary Message Format**:
  ```text
  📊 *Результати голосування:* «{Question}»
  
  • {Option 1}: {Count} голосів ({Percentage}%)
  • {Option 2}: {Count} голосів ({Percentage}%)
  
  🏆 *Переможець:* {WinningOption}
  👥 *Явка:* {VotedCount} з {TotalRosterCount} ({TurnoutPercentage}%)
  ```

---

### 1.3 `/cancel_poll`
Voids the active poll immediately without publishing winning options or statistics.

- **Syntax**: `/cancel_poll`
- **Preconditions**:
  - Sender must be an authorized committee admin in the group.
  - An active poll (`Open` or `Reminded`) must exist in the chat.
- **Outbound Telegram Calls**:
  1. `stopPoll(chat_id, message_id)`: Stops voting in Telegram.
  2. `sendMessage(chat_id, text)`: Publishes cancellation announcement.
- **Cancellation Message**:
  ```text
  ❌ Голосування «{Question}» було скасовано адміністратором. Результати не зараховано.
  ```

---

## 2. Automated Follow-Up Reminder Contract

Dispatched automatically by the `TimerTrigger` monitor when `expiresAtUtc - now <= 6 hours` and `status == Open`.

- **Outbound Telegram Call**:
  - Method: `sendMessage`
  - Parameters:
    - `chat_id`: Group chat ID.
    - `reply_to_message_id`: Poll `message_id`.
    - `disable_notification`: `true` if current Kyiv time is in quiet hours (20:00 - 08:00), otherwise `false`.
    - `parse_mode`: `"MarkdownV2"`.
- **Aggregated Message Template**:
  ```text
  ⏰ *Нагадування про голосування!*
  До завершення голосування «{Question}» залишилося менше 6 годин.
  
  Будь ласка, зробіть свій вибір:
  {MentionsList}
  ```
- **Mentions Formatting**:
  - With username: `@username`
  - Without username: `[{DisplayName}](tg://user?id={UserId})`
  - Combined as a comma-separated list in a single message.
