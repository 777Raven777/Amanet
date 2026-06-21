# Raven – Backend Documentation

Consolidated documentation for the backend components of the Raven chat application: the database schema and EF Core integration, real-time messaging over SignalR, file storage, and Redis caching.

## Contents

- [Database Schema](#database-schema)
- [Database Integration (EF Core)](#database-integration-ef-core)
- [Real-Time Messaging (SignalR)](#real-time-messaging-signalr)
- [File Storage (FileService)](#file-storage-fileservice)
- [Caching (Redis)](#caching-redis)

---

## Database Schema

This document describes the PostgreSQL schema used by the chat application, accessed via **EF Core**. It covers the core entities, their columns, and relationships.

### Tables

#### `Users`

Application accounts.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `Username` | text | unique, not null | |
| `Email` | text | unique, not null | |
| `PasswordHash` | text | not null | _(verify)_ |
| `ProfilePictureUrl` | text | nullable | |
| `SuspensionReason` | text | nullable | set when account is suspended |
| `SuspendedUntil` | timestamptz | nullable | null = not suspended |
| `CreatedAt` | timestamptz | not null | _(verify)_ |

---

#### `Servers`

A community/guild that contains channels and members.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `Name` | text | not null | |
| `OwnerId` | uuid | FK → `Users.Id` | _(verify)_ |

---

#### `ServerChannels`

A text channel belonging to a server.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `ServerId` | uuid | FK → `Servers.Id`, on delete cascade | |
| `Name` | text | not null | |

---

#### `Roles`

Permission sets defined per server. System roles (`IsSystem = true`) are seeded and not user-deletable.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `ServerId` | uuid | FK → `Servers.Id` | _(verify – roles appear server-scoped)_ |
| `Name` | text | not null | |
| `IsSystem` | bool | not null, default false | |
| `Permissions` | integer[] | | set of permission values; see enum below _(verify storage)_ |

**Permissions enum**

| Value | Name |
|---|---|
| 0 | SendMessages |
| 1 | DeleteMessages |
| 2 | BanUsers |
| 3 | ReadMessages |
| 4 | EditMessages |
| 5 | InviteUsers |
| 6 | EditUsers |
| 7 | CreateChannels |
| 8 | ModifyRoles |

> Permissions may be stored as a Postgres integer array, a bit flag, or a separate `RolePermissions` join table depending on the EF mapping. Adjust this section to match the actual implementation.

---

#### `ServerParticipants`

Join table linking users to servers, with their assigned role and optional per-server display name.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | _(verify – may be composite PK on UserId + ServerId)_ |
| `UserId` | uuid | FK → `Users.Id` | |
| `ServerId` | uuid | FK → `Servers.Id`, on delete cascade | |
| `RoleId` | uuid | FK → `Roles.Id`, nullable | |
| `CustomName` | text | nullable | overrides username within the server |

---

#### `ServerInvites`

Pending invitations for a user to join a server.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `ServerId` | uuid | FK → `Servers.Id`, on delete cascade | |
| `InvitedUserId` | uuid | FK → `Users.Id` | |
| `Status` | text/int | | pending / accepted / declined _(verify – may be deleted on response instead)_ |
| `CreatedAt` | timestamptz | | _(verify)_ |

---

#### `Relationships` (friends & friend requests)

Tracks both pending friend requests and accepted friendships between two users.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `SenderId` | uuid | FK → `Users.Id` | user who initiated |
| `ReceiverId` | uuid | FK → `Users.Id` | user who received |
| `Status` | text/int | not null | pending / accepted _(verify)_ |
| `CreatedAt` | timestamptz | not null | |

> A unique constraint on `(SenderId, ReceiverId)` (or an unordered pair) prevents duplicate requests. _(verify)_

---

#### `Conversations` (direct messages)

A one-to-one DM thread between two users.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `CreatedAt` | timestamptz | not null | _(verify)_ |

---

#### `ConversationParticipants`

Join table linking users to a conversation (two rows per DM).

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `ConversationId` | uuid | FK → `Conversations.Id`, on delete cascade | part of composite PK _(verify)_ |
| `UserId` | uuid | FK → `Users.Id` | part of composite PK _(verify)_ |

---

#### `Messages`

A single message. Belongs to **either** a server channel **or** a conversation, never both.

| Column | Type | Constraints | Notes |
|---|---|---|---|
| `Id` | uuid | PK | |
| `SenderId` | uuid | FK → `Users.Id` | |
| `ChannelId` | uuid | FK → `ServerChannels.Id`, nullable | set for channel messages |
| `ConversationId` | uuid | FK → `Conversations.Id`, nullable | set for DMs |
| `Message` | text | not null | message body |
| `Edited` | bool | not null, default false | flipped true when content is patched |
| `SentAt` | timestamptz | not null | |

---

### Migrations

The schema is managed through EF Core migrations.

```bash
# create a new migration after changing entities
dotnet ef migrations add <Name>

# apply migrations to the database
dotnet ef database update
```

The database runs in Docker (PostgreSQL). If the container fails to initialize after schema or environment changes, reset the volume and bring it back up with aligned environment config:

```bash
docker compose down -v
docker compose up -d
```

---

## Database Integration (EF Core)

Persistence is handled with **Entity Framework Core** against **PostgreSQL**, through a single `AppDbContext`. This document describes the context's entity sets, the global primary-key strategy, and the relationship/constraint configuration applied in `OnModelCreating`.

---

### Entity Sets

| `DbSet` | Entity | Notes |
|---|---|---|
| `Users` | `User` | accounts |
| `Relationships` | `Relationship` | friend requests & friendships (sender/receiver) |
| `Conversations` | `Conversation` | DM threads; stores the user **pair directly** (no participant join table) |
| `PrivateMessages` | `PrivateMessage` | messages within a conversation |
| `ChannelMessages` | `ChannelMessage` | messages within a server channel |
| `Servers` | `Server` | communities |
| `ServerChannels` | `ServerChannel` | channels within a server |
| `ServerParticipants` | `ServerParticipant` | server membership |
| `Roles` | `Role` | per-server permission sets |
| `Tokens` | `Token` | persisted auth/refresh tokens |
| `ServerInvites` | `ServerInvite` | pending server invitations |

> Private and channel messages are **separate tables** (`PrivateMessages` / `ChannelMessages`), not one polymorphic table. A `ConversationParticipants` join table is intentionally absent – a conversation is a fixed pair of users stored on the `Conversation` row itself.

---

### Primary Keys & ID Generation

Every entity derives from `BaseEntity`, which exposes a `Guid Id`. Rather than configuring each entity individually, `OnModelCreating` applies a single convention across the whole model:

```csharp
foreach (var type in modelBuilder.Model.GetEntityTypes())
{
    modelBuilder.Entity(type.ClrType)
        .Property(nameof(BaseEntity.Id))
        .HasValueGenerator<GuidV7ValueGenerator>()
        .ValueGeneratedOnAdd();
}
```

So every table's `Id` is a **GUID generated on insert** by `GuidV7ValueGenerator`.

#### `GuidV7ValueGenerator`

A custom EF Core `ValueGenerator<Guid>` that produces **UUID version 7** values:

```csharp
public override bool GeneratesTemporaryValues => false;
public override Guid Next(EntityEntry entry) => Guid.CreateVersion7();
```

- `GeneratesTemporaryValues => false` means the generated value is the real key, used directly – EF doesn't swap it for a database-generated one later.
- Values are produced client-side at add time, so the entity's `Id` is known before `SaveChanges`.

**Why v7 instead of a random GUID:** UUID v7 is time-ordered (its leading bits are a timestamp), so newly inserted keys are roughly sequential. That gives much better B-tree index locality than random v4 GUIDs – less page fragmentation, less write amplification – while keeping globally unique, non-guessable identifiers. Rows are also implicitly sortable by creation time via their primary key.

---

### Relationship & Constraint Configuration

Most relationships rely on EF conventions. The following are configured explicitly because they involve multiple references to the same entity, custom delete behavior, or constraints.

#### `Relationship` (friends / friend requests)

Two foreign keys into `Users` – `SenderId` and `ReceiverId` – each a one-to-many with no inverse navigation (`WithMany()`). Both use `OnDelete(Restrict)`, so a user who is part of any relationship cannot be deleted until those rows are removed first.

#### `Conversation`

Two foreign keys into `Users` – `UserLowId` and `UserHighId` – both `WithMany()` with `OnDelete(Restrict)`.

Two constraints keep DMs canonical and de-duplicated:

| Constraint | Definition | Purpose |
|---|---|---|
| Unique index | `(UserLowId, UserHighId)` | at most one conversation per user pair |
| Check constraint `CK_Conversation_UserOrder` | `"UserLowId" < "UserHighId"` | forces the lower user id into `UserLowId` |

Together these guarantee a single conversation row per pair regardless of who initiates it: the participants are always stored in a fixed (ordered) order, so the unique index can't be sidestepped by swapping sides.

#### `ServerInvite`

Three foreign keys:

| Navigation | FK | On delete |
|---|---|---|
| `Server` | `ServerId` | **Cascade** – deleting a server removes its invites |
| `Inviter` | `InviterId` → `Users` | Restrict |
| `InvitedUser` | `InvitedUserId` → `Users` | Restrict |

---

### Delete Behavior Summary

- **Restrict** is used on every `User` reference (relationships, conversations, invite inviter/invited). A user cannot be deleted while referenced; dependent rows must be cleaned up explicitly first. This avoids accidental cascade-deletes of users and the orphaning of message history.
- **Cascade** is used for `ServerInvite → Server`, so tearing down a server cleans up its outstanding invites automatically.

---

### Migrations

Schema changes are applied via EF Core migrations:

```bash
dotnet ef migrations add <Name>
dotnet ef database update
```

The database runs in Docker (PostgreSQL). If the container fails to initialize after a schema or environment change, reset the volume and bring it back up with aligned environment config:

```bash
docker compose down -v
docker compose up -d
```

---

## Real-Time Messaging (SignalR)

Real-time delivery is handled by a single **SignalR hub** (`ChatHub`), not raw WebSockets. SignalR negotiates the best available transport (WebSockets where supported) and gives us groups, typed events, and reconnection handling on top of the raw connection.

The hub backs three features: private (DM) messaging, server-channel messaging, and presence/typing indicators.


---

### Connection Lifecycle

On connect (`OnConnectedAsync`):

1. The connection is registered against the user in the connection tracker.
2. The connection is added to the user's personal group `user-{userId}`.
3. The presence service is notified that the user came online.

On disconnect (`OnDisconnectedAsync`):

1. The connection is removed from the tracker.
2. The presence service is notified the user may have gone offline.

---

### Groups

Events are scoped by placing connections into groups (`RealtimeGroups`):

| Group | Format | Membership |
|---|---|---|
| User | `user-{userId}` | joined automatically on connect; targets all of one user's connections |
| Conversation | `conv-{conversationId}` | joined via `JoinConversation`; DM participants |
| Channel | `channel-{channelId}` | joined via `JoinChannel`; members of the channel's server |

Joining a conversation or channel is **authorized server-side**: `JoinConversation` verifies the caller is one of the two conversation participants, and `JoinChannel` verifies the caller is a participant of the channel's server. Unauthorized joins throw a `HubException`.

---

### Server → Client Events

Event names are defined in `RealtimeEvents`.

| Event | Payload | Fired when |
|---|---|---|
| `ReceivePrivateMessage` | `MessageDTO` | a DM is sent (to the conversation group, and to the sender's user group for `SendMessageToUser`) |
| `ReceiveChannelMessage` | `MessageDTO` | a channel message is sent (to the channel group) |
| `NewMessageNotification` | `MessageDTO` | a DM arrives for a recipient who hasn't joined the conversation group yet (sent to their user group) |
| `MessageEdited` | see below | a message's text is edited |
| `MessageDeleted` | see below | a message is deleted |
| `UserTyping` | see below | a participant is typing |
| `PresenceChanged` | _(verify)_ | a user comes online / goes offline |

`MessageDTO` shape: `Id`, `Sender` (`UserDTO`), `Message`, `SentAt`, `Edited`.

#### Shared event payloads (DM vs. channel)

`MessageEdited`, `MessageDeleted`, and `UserTyping` use the **same event name** for both DMs and channels but differ by which scope field is present. Clients distinguish the context by checking for `ConversationId` vs. `ChannelId`.

**`MessageEdited`**
```jsonc
// DM
{ "MessageId": "...", "ConversationId": "...", "NewText": "...", "EditedBy": "..." }
// Channel
{ "MessageId": "...", "ChannelId": "...", "NewText": "...", "EditedBy": "..." }
```

**`MessageDeleted`**
```jsonc
// DM
{ "MessageId": "...", "ConversationId": "...", "DeletedBy": "..." }
// Channel
{ "MessageId": "...", "ChannelId": "...", "DeletedBy": "..." }
```

**`UserTyping`** (sent to *others* in the group, not the typer)
```jsonc
// DM
{ "UserId": "...", "ConversationId": "..." }
// Channel
{ "UserId": "...", "ChannelId": "..." }
```

---

### Client → Server Methods

All methods require an authenticated connection. On failure (unauthorized, not a participant, or a service-level error) the method throws a `HubException` whose message is delivered to the calling client; the client should handle the rejected invocation.

#### Private conversations
 
| Method | Parameters | Effect |
|---|---|---|
| `JoinConversation` | `conversationId` | authorize + join `conv-{id}` |
| `LeaveConversation` | `conversationId` | leave `conv-{id}` |
| `SendPrivateMessage` | `conversationId, text` | persist message, deliver `ReceivePrivateMessage` to the conversation's participants – a DM is always a user pair, so this reaches the one other user (plus the sender's own joined connections) via the `conv-{id}` group |
| `SendMessageToUser` | `recipientId, text` | persist message, then deliver `ReceivePrivateMessage` to the **sender** and `NewMessageNotification` to the **recipient** – each addressed to that one user (fanned out across their own connections), used when the recipient hasn't joined the `conv-{id}` group yet |
| `EditPrivateMessage` | `conversationId, messageId, newText` | persist edit, deliver `MessageEdited` to the conversation pair via the `conv-{id}` group |
| `DeletePrivateMessage` | `conversationId, messageId` | persist delete, deliver `MessageDeleted` to the conversation pair via the `conv-{id}` group |
| `TypingInConversation` | `conversationId` | send `UserTyping` to the other participant (others in the `conv-{id}` group) |
 
> **Why two send methods?** A conversation is always a user pair, so both methods send a private message to a single other user – they differ only in routing. `SendPrivateMessage` delivers through the `conv-{id}` group, which reaches the other participant only if they've joined it. `SendMessageToUser` covers the case where the recipient hasn't joined yet: it addresses each user directly (echoing `ReceivePrivateMessage` to the sender's own connections and pushing `NewMessageNotification` to the recipient), so the recipient is reached regardless. Neither is a multi-user broadcast – that's what channel sends are for.
---

### Server-Initiated Events (outside the hub)

Code that isn't running inside a hub invocation (background services, REST controllers, presence) pushes events through `IRealtimeNotifier`, implemented by `SignalRRealtimeNotifier` over `IHubContext<ChatHub>`:

- `NotifyGroupAsync(groupName, eventName, payload)` – send to any group.
- `NotifyUserAsync(userId, eventName, payload)` – send to a user's `user-{userId}` group (all their connections).

This is the path used to deliver events like `PresenceChanged` without a client invocation.

---

### Client Integration Notes

- Open the connection with the access token, then call `JoinConversation` / `JoinChannel` for each open thread to start receiving its messages.
- Subscribe to `ReceivePrivateMessage` / `ReceiveChannelMessage` and render from the event payload rather than the send call's return value, so the sender and all other clients stay consistent.
- Also subscribe to `NewMessageNotification` to surface DMs for conversations the user hasn't actively opened.
- Treat `MessageEdited` / `MessageDeleted` as scope-agnostic: branch on whether `ConversationId` or `ChannelId` is present.
- Handle invocation errors – a rejected `Join*` or `Send*` call surfaces as a `HubException`.

---

## File Storage (FileService)

`FileService` (interface `IFileService`) handles saving and deleting uploaded files on the local filesystem. It is used for user-supplied assets such as profile pictures.

Files are stored under the web root so they can be served as static content:

```
{ContentRootPath}/wwwroot/Uploads/
```

Saved files are named `{GUID}{ext}` – a random GUID plus the original extension. This guarantees unique names and avoids exposing the uploader's original filename. The method returns only the generated file name (not a full path); callers persist that name and resolve it to a public URL when serving. _(verify how the public URL is built – e.g. `/Uploads/{fileName}` via the static files middleware)_

---

### Methods

| Method | Signature | Returns |
|---|---|---|
| `SaveFileAsync` | `Task<string> SaveFileAsync(IFormFile image, string[] allowedExtensions)` | the generated file name (`{GUID}{ext}`) |
| `DeleteFile` | `void DeleteFile(string fileNameWithExtension)` | – |

---

#### `SaveFileAsync`

Validates and writes an uploaded file to the uploads directory.

Behavior, in order:

1. Throws `ArgumentNullException` if `image` is null.
2. Resolves the uploads path (`wwwroot/Uploads`) and creates the directory if it doesn't exist.
3. Reads the file extension from `image.FileName`; if it isn't in `allowedExtensions`, throws `ArgumentException` listing the allowed extensions.
4. Generates `{GUID}{ext}`, streams the upload to disk, and returns the generated name.

The caller supplies the permitted extensions per call (e.g. `[".png", ".jpg", ".jpeg"]`), so the same service can enforce different rules in different contexts.

#### `DeleteFile`

Removes a previously saved file by name.

1. Throws `ArgumentNullException` if `fileNameWithExtension` is null or empty.
2. Resolves the file under `wwwroot/Uploads`; if it doesn't exist, throws `FileNotFoundException`.
3. Deletes the file.

---

### Errors

| Condition | Exception |
|---|---|
| `SaveFileAsync` called with null file | `ArgumentNullException` |
| Extension not in the allowed list | `ArgumentException` |
| `DeleteFile` called with null/empty name | `ArgumentNullException` |
| File to delete does not exist | `FileNotFoundException` |

Callers (controllers/services) are responsible for catching these and translating them into appropriate HTTP responses.

---

## Caching (Redis)

Caching is provided by `ICacheService`, implemented by `RedisCacheService` over **StackExchange.Redis**. It's a small, generic key/value cache used to avoid repeated database reads for frequently accessed entities (users, servers, roles, channels, relationships, tokens).

`ICacheService` is registered as a **singleton** in `Program.cs`:

```csharp
builder.Services.AddSingleton<ICacheService, RedisCacheService>();
```

> This is the same Redis instance used by the SignalR connection tracker (`RedisConnectionTracker`). Both share the `Redis:InstanceName` prefix, so the cache's entity keys and the realtime `conn:*` keys live in one namespace without colliding.

---

### Cache Keys

Keys are built through the `CacheKeys` helper rather than hand-written strings, keeping them consistent across services:

| Helper | Key format |
|---|---|
| `CacheKeys.User(id)` | `user:{id}` |
| `CacheKeys.Server(id)` | `server:{id}` |
| `CacheKeys.Roles(serverId)` | `roles:{serverId}` |
| `CacheKeys.ServerParticipant(id)` | `serverparticipant:{id}` |
| `CacheKeys.ServerChannels(serverId)` | `serverchannels:{serverId}` |
| `CacheKeys.Relationship(id)` | `relationship:{id}` |
| `CacheKeys.Token(tokenValue)` | `token:{tokenValue}` |

At runtime every key is additionally prefixed with the configured `Redis:InstanceName`, so the physical Redis key is `{InstanceName}{logicalKey}` (e.g. `{prefix}user:{id}`).

---

### Interface

| Method | Signature | Behavior |
|---|---|---|
| `SetAsync` | `Task SetAsync<T>(string key, T value, TimeSpan? ttl = null)` | JSON-serializes `value` and stores it; expires after `ttl`, or **5 minutes** by default |
| `GetAsync` | `Task<T?> GetAsync<T>(string key)` | returns the deserialized value, or `default(T)` (e.g. `null`) on a miss |
| `RemoveAsync` | `Task RemoveAsync(string key)` | deletes the key – call after writing the underlying entity to avoid stale reads |

#### Behavior details

- **Serialization:** values are stored as JSON via `System.Text.Json`. The cached type must be JSON-serializable.
- **TTL:** `RedisCacheService` uses a 5-minute default (`DefaultTtl`). Pass an explicit `TimeSpan` to override per call.
- **Misses:** `GetAsync` returns `default` when the key is absent or empty; callers branch on that to fall back to the database.

---

### Usage Pattern (cache-aside)

Read paths follow the cache-aside pattern: check the cache, fall back to the database on a miss, then populate the cache.

```csharp
var key = CacheKeys.Server(serverId);

var cached = await _cache.GetAsync<ServerDto>(key);
if (cached != null) return cached;

var server = await _db.Servers.FindAsync(serverId);
if (server != null)
    await _cache.SetAsync(key, server, TimeSpan.FromMinutes(15));

return server;
```

On any write (create / update / delete) to an entity, invalidate its key so the next read repopulates from the database:

```csharp
await _cache.RemoveAsync(CacheKeys.Server(serverId));
```
