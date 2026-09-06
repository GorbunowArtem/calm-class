# Contract: Azure Cosmos DB Documents & Queries

**Feature**: `specs/001-poll-automator-monitor`  
**Container**: `Polls`  
**Partition Key**: `/chatId`

---

## 1. Document Representations

### 1.1 `TrackedPoll` Document
```json
{
  "id": "poll_5987654321098765432",
  "chatId": "-1001234567890",
  "type": "TrackedPoll",
  "pollId": "5987654321098765432",
  "messageId": 450,
  "question": "Екскурсія восени",
  "options": ["Зоопарк", "Музей", "Планетарій"],
  "allowsMultipleAnswers": false,
  "createdAtUtc": "2026-09-04T10:00:00Z",
  "expiresAtUtc": "2026-09-05T10:00:00Z",
  "remindedAtUtc": null,
  "closedAtUtc": null,
  "status": "Open",
  "_etag": "\"00000000-0000-0000-0000-000000000000\""
}
```

### 1.2 `PollVote` Document
```json
{
  "id": "vote_5987654321098765432_987654321",
  "chatId": "-1001234567890",
  "type": "PollVote",
  "pollId": "5987654321098765432",
  "userId": 987654321,
  "displayName": "Taras Shevchenko",
  "username": "taras_sh",
  "selectedOptionIndices": [1],
  "votedAtUtc": "2026-09-04T11:15:00Z",
  "isRevoked": false
}
```

### 1.3 `GroupMember` Document
```json
{
  "id": "member_-1001234567890_987654321",
  "chatId": "-1001234567890",
  "type": "GroupMember",
  "userId": 987654321,
  "displayName": "Taras Shevchenko",
  "username": "taras_sh",
  "role": "Member",
  "isActive": true,
  "joinedAtUtc": "2026-09-01T08:00:00Z"
}
```

---

## 2. Core Partition-Scoped Queries

### 2.1 Get Active Poll for Chat
```sql
SELECT * FROM c 
WHERE c.chatId = @chatId 
  AND c.type = "TrackedPoll" 
  AND c.status IN ("Open", "Reminded")
```

### 2.2 Get All Active Roster Members
```sql
SELECT * FROM c 
WHERE c.chatId = @chatId 
  AND c.type = "GroupMember" 
  AND c.isActive = true
```

### 2.3 Get Non-Revoked Votes for Poll
```sql
SELECT * FROM c 
WHERE c.chatId = @chatId 
  AND c.type = "PollVote" 
  AND c.pollId = @pollId 
  AND c.isRevoked = false
```

### 2.4 Cross-Partition Monitor Query (for TimerTrigger)
```sql
SELECT * FROM c 
WHERE c.type = "TrackedPoll" 
  AND c.status IN ("Open", "Reminded")
```
*Note: Evaluated across partitions once every 5 minutes by the timer function.*
