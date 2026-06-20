# Amanet Backend — Developer Documentation

> Scope: this document covers the **API / auth / service layer** of the Amanet backend
> (the part owned by Artem). It is meant as an onboarding reference for another developer
> continuing the work, and as a structural overview for grading.
>
> **Not covered here** (intentionally):
> - Request/response shapes of endpoints and all DTOs → see **Swagger** (`/swagger`).
> - The infrastructure layer (EF migrations, repository/DB internals, SignalR hub
>   implementation, DB-level constraints) → owned by the partner.
> - `FileService`, `PresenceService`, realtime notifier internals → only referenced where
>   they touch this layer.
> - `Hubs` - websockets implementation → alwo owned by partner

---

## 1. Layering & request flow

```
HTTP request
  → AuthenticationMiddleware        (resolves token → ClaimsPrincipal)
    → [Authorize(Policy = "...")]   (coarse token-permission gate)
      → Controller                  (parses claims, no business logic)
        → Service                   (all business logic + EF Core)
          → AppDbContext / raw SQL
```

Three rules hold everywhere in this layer:

1. **Controllers are thin.** They only: read `callerId` from the JWT-style claim,
   clamp pagination params, call one service method, and map the result to an
   `ActionResult`. No queries, no business rules.
2. **Services own everything.** Validation, authorization checks, persistence, and the
   shape of the returned data all live in the service.
3. **Services never throw to signal expected failures.** They return a tuple.

### Service return contract

Every service method that can fail in an *expected* way returns a named tuple:

```csharp
Task<(bool Success, string Message)>                       // command, no payload
Task<(bool Success, string Message, TDto? Payload)>        // command/query with payload
```

The controller branches on `Success`: `Ok(...)` / `StatusCode(201|204, ...)` on success,
`BadRequest(Message)` on failure. `Message` is a human-readable string and is sometimes
deliberately vague for security (see §4 and the "vagueMessage" pattern in `MessageService`).

Unexpected failures (real bugs / DB outages) are caught inside the service, logged
(because of lack of time ＼（ｏ_ｏ）／placeholder `// log error` for now — **TODO: wire a real logger**), and collapsed into a
generic failure tuple. They are never surfaced as raw exceptions to the client.

---

## 2. Cross-cutting conventions

### 2.1 Identifiers — UUIDv7

All entities derive from `BaseEntity` (`Id : Guid`, `CreatedAt : DateTime`). IDs are
generated **application-side as UUIDv7** via `GuidV7ValueGenerator`, wired globally in
`AppDbContext.OnModelCreating`:

```csharp
modelBuilder.Entity(type.ClrType)
    .Property(nameof(BaseEntity.Id))
    .HasValueGenerator<GuidV7ValueGenerator>()
    .ValueGeneratedOnAdd();
```

Why v7 matters: v7 GUIDs are **time-ordered**, which is what makes the message cursor
pagination (§2.3) work. Do **not** mark `Id` with `[DatabaseGenerated(Identity)]` — that
hands generation to Postgres `gen_random_uuid()` (v4, unordered) and silently breaks
cursor ordering.

### 2.2 Offset pagination (lists)

Used for friends, servers, invites, participants, search. Pattern:

```csharp
.Skip((currentPage - 1) * pageSize)
.Take(pageSize + 1)              // fetch one extra to detect a next page
.ToListAsync();

bool nextPage = list.Count > pageSize;
if (nextPage) list.RemoveAt(list.Count - 1);   // drop the probe element
```

Returned via DTOs extending `PaginatedListDTO` (`PageSize`, `CurrentPage`, `NextPage`).
**Page-size clamping happens in the controller**, not the service
(`Math.Clamp(pageSize, 1, 50)` typically).

### 2.3 Cursor pagination (messages)

`MessageService.PaginateMessageList` uses a **keyset cursor on `Id`** (works because IDs
are time-ordered v7):

```csharp
if (cursor != null) query = query.Where(m => m.Id <= cursor);
query.OrderByDescending(m => m.Id).Take(pageSize + 1)...
```

Returns `PaginatedMessagesDTO { Messages, Next, HasMore }`. `Next` is the `Id` of the probe
element (the next cursor), or `null` at the end. This is preferred over offset for messages
because it's stable under inserts.

### 2.4 Caching (`ICacheService` / Redis)

`ICacheService` is a thin generic wrapper over Redis (`RedisCacheService`), registered as a
singleton. Keys are centralized in `CacheKeys` (`token:{v}`, `roles:{serverId}`, …).

**Invalidation discipline:** any write to a cached entity must `RemoveAsync` the matching
key in the same method. Currently cached: **tokens** (`TokenService`) and **server roles**
(`RoleService`). When you add caching to a new entity, add its key to `CacheKeys` and
invalidate on every mutation path.

---

## 3. Permission model (two tiers)

There are **two independent** permission systems. Don't confuse them.

| Tier | Scope | Stored on | Enforced by |
|------|-------|-----------|-------------|
| **Token permissions** | What this *token* may do at all | `User.Permissions` → copied to `Token.Permissions` (`TokenPermissions` enum) | ASP.NET `[Authorize(Policy=…)]` via claims added in the middleware |
| **Server role permissions** | What this *user* may do *inside a specific server* | `Role.Actions` (`Permissions` enum) on the participant's role | `AppDbContextExtensions.VerifyUserAccessAsync(...)` called inside the service |

- `TokenPermissions`: `CanSendDirectMessages`, `CanUseServers`, `CanAddFriends`. The
  middleware emits one claim per granted permission; policies of the same name gate the
  controller action.
- `Permissions` (server roles): `SendMessages`, `DeleteMessages`, `BanUsers`,
  `ReadMessages`, `EditMessages`, `InviteUsers`, `EditUsers`, `CreateChannels`,
  `ModifyRoles`.

`VerifyUserAccessAsync` loads the caller's `ServerParticipant` (incl. `Role`), returns
`(false, …)` if they aren't in the server **or** lack the permission. A server-scoped
service method should call it before doing anything privileged. Note: returning the same
message for "not a member" and "server doesn't exist" is intentional (don't leak server
existence).

On server creation (`ServerService.CreateServer`) two system roles are seeded:
`default` (send/read/edit/delete/invite) and `admin` (everything). The creator is added as
a participant with `admin`.

---

## 4. Authentication & authorization

### `Token` (model)

Opaque random token (32 bytes, base64), not a JWT. Fields: `TokenValue`, `Permissions`
(`List<TokenPermissions>`), `UserId`, `User`. Validity is checked against
`User.LastTokenReset` — see logout below.

### `TokenService`

- `GenerateToken(userId)` — mints a token snapshotting the user's current permissions,
  persists it, returns `TokenDTO` (or `SuspendedTokenDTO` if `User.SuspendedUntil` is in
  the future, carrying suspension metadata).
- `GetTokenData(tokenValue)` — **cache-first** lookup (`token:{value}`, 1h TTL). A token is
  invalid if it doesn't exist, the user is gone, or **`User.LastTokenReset > Token.CreatedAt`**
  (i.e. the user logged out / reset after this token was issued).
- `Logout(userId)` — bumps `LastTokenReset = now` (invalidating all existing tokens at once)
  and evicts each of the user's token cache entries.

### `AuthenticationMiddleware`

Custom middleware (not the built-in JWT handler). Resolves a token from, in order:

1. **WebSocket** subprotocol header `Sec-WebSocket-Protocol: access_token, <token>`
   (SignalR's transport convention),
2. `Authorization: Bearer <token>` header,
3. `?access_token=<token>` query string (fallback, also for SignalR).

On a valid token it builds a `ClaimsPrincipal` with a `NameIdentifier` claim (the user id —
this is what every controller reads as `callerId`) plus one claim per granted
`TokenPermission`. On an invalid token it short-circuits with **401**. **No token at all →
it passes through unauthenticated**; the `[Authorize]` attribute downstream is what rejects
anonymous access.

> Order matters: register this middleware before authorization, and the query-string check
> must run before the "invalid token" rejection so the SignalR `?access_token=` path works.

### `NullAuthHandler`

A no-op `AuthenticationHandler` returning `AuthenticateResult.NoResult()`. It exists so the
ASP.NET authorization stack has a registered scheme to attach policies to, while the actual
identity is supplied out-of-band by `AuthenticationMiddleware`. Policies (`CanUseServers`,
`CanAddFriends`, `CanSendDirectMessages`) are defined in `Program.cs` and check for the
claim of the same name.

### `NameIdUserIdProvider`

SignalR `IUserIdProvider` that maps a connection to its user via the `NameIdentifier` claim.
This is what makes `Clients.User(userId)` / `Context.UserIdentifier` line up with our
`callerId`, so the realtime layer can target users by their DB id.

---

## 5. Services reference

Each service is constructor-injected with `AppDbContext` (and `ICacheService` /
`IFileService` where noted). All are registered scoped in `Program.cs`.

### `UserService` (`+ IPasswordHasher<User>`, `+ IFileService`)
Registration, login, profile read/patch, user search.
- `AddUser` — trims/normalizes email+username, hashes password
  (`IPasswordHasher`), optionally saves a profile picture via `IFileService` and stores the
  URL as `"/uploads/{fileName}"`.
- `LoginUser` — matches by username **or** email (case-insensitive), verifies the hash.
- `PatchProfile` — partial update; password change requires the correct `oldPassword`;
  replacing the avatar deletes the old file first (see §8 for the delete-path caveat).
- `UniqueEmailAndUsername`, `SearchUsersAsync`, `GetPublicUserByIdAsync`, `GetMeByIdAsync`.

### `FriendService`
Relationship lifecycle. A `Relationship` has `SenderId`, `ReceiverId`, `Status`
(`Waiting | Rejected | Accepted`).
- `SendRequest` — rejects self-requests, missing users, and any pre-existing relationship
  in either direction.
- `AcceptRequest` / `RejectRequest` — raw-SQL state transition guarded by
  `ReceiverId == caller && Status == Waiting` (only the receiver can act, only on a pending
  request).
- `DeleteRequest` — either party may delete.
- `GetReceivedRequests` / `GetSentRequests` — paginated pending lists.
- `RetrieveActiveRelationship` — the accepted-friends list; projects the *other* user.

### `ServerService`
- `CreateServer` — creates the server, seeds `default` + `admin` roles, adds the creator as
  an admin participant, saves in one transaction.
- `DeleteServer` / `EditServer` — raw-SQL, gated by `CreatorId == caller` (only the owner).
- `GetServersList` — servers the caller participates in (joins through `ServerParticipants`).

### `ServerChannelService`
> Owned here; public surface is exercised by `ServerChannelController`
> (`Create / Delete / Edit / ListServerChannels`). Each method takes `(callerId, serverId, …)`
> and must call `VerifyUserAccessAsync` with the appropriate `Permissions` value
> (`CreateChannels` for create, etc.) before mutating. See Swagger for shapes.

### `ServerParticipantService`
Handles **both** server invites and participant management (the
`ServerInviteController`, `ServerParticipantController`, and the invite endpoints on
`UserController` all delegate here).
- Invites: `SendInvite` (checks `InviteUsers` perm, target exists, not already
  member/invited), `AcceptInvite` (deletes the invite, adds a participant on the `default`
  role — caller-side), `RejectInvite`, `DeleteInvite` (inviter-side), `ListServerInvites`
  (server-side), `ListReceivedInvites` (user-side).
- Participants: `ModifyParticipant` (role/custom-name, needs `EditUsers`),
  `DeleteParticipant` (leave self, or ban others with `BanUsers`; the server creator can
  never be removed), `ListParticipants` (needs `ReadMessages`).

### `RoleService` (`+ ICacheService`)
CRUD over server roles, cached under `roles:{serverId}` (1h). Every mutation invalidates
the cache. Guards: all paths require `ModifyRoles`; **system roles can't be edited or
deleted**; deleting a role reassigns its participants to the server's `default` role; a
server is capped at 50 roles.

### `MessageService`
DM and channel messaging — the most involved service.
- DM send flow: `SendPrivateMessage` → resolve/stage a `Conversation` →
  `PersistMessage`. Conversations are **canonicalized**: the user pair is sorted into
  `(UserLowId < UserHighId)`, enforced by a DB `CHECK` + unique index, so there's exactly
  one conversation per pair regardless of who starts it (`OrderPair`).
- New conversation + first message are saved in **one** `SaveChanges` (no orphan
  conversations on failure).
- **Race handling:** if two requests create the same conversation concurrently, the loser
  hits Postgres `23505` (unique violation). The `catch (DbUpdateException) when (... SqlState
  "23505")` block detaches the losing entities, re-fetches the winning conversation, and
  re-inserts the message against it.
- `OnlyFriendsMessages`: if the receiver restricts DMs to friends, non-friends are rejected
  (there's a documented, accepted TOCTOU gap in the inline comments).
- Channel send/read require server permissions (`SendMessages` / `ReadMessages`); edits and
  deletes are gated by `EditMessages` / `DeleteMessages` and ownership (`SenderId == caller`).
- `GetConversations` — conversation list with last-message preview, ordered by recency.

---

## 6. Controllers (route reference)

Controllers add no logic beyond claim parsing + param clamping; bodies/responses are in
Swagger. Base prefix `api/v1`.

| Controller | Routes |
|---|---|
| `UserController` | `POST register`, `POST login`, `POST logout`, `GET search`, `PATCH update-profile`, `GET server-invites`, `POST server-invites/{id}/accept-invite`, `POST server-invites/{id}/reject-invite` |
| `FriendsController` | `POST /`, `DELETE {id}`, `POST {id}/accept-request`, `POST {id}/reject-request`, `GET requests/received`, `GET requests/sent`, `GET friends-list` |
| `ServerController` | `POST /`, `DELETE {id}`, `PATCH {id}`, `GET /` |
| `ServerChannelController` | base `servers/{serverId}/channels`: `POST`, `DELETE {channelId}`, `PATCH {channelId}`, `GET` |
| `ServerInviteController` | base `servers/{serverId}/invites`: `POST`, `DELETE {inviteId}`, `GET` |
| `ServerParticipantController` | base `servers/{serverId}/participants`: `PATCH {participantId}`, `DELETE {participantId}`, `GET` |
| `RoleController` | base `servers/{serverId}/roles`: `POST`, `DELETE {roleId}`, `PATCH {roleId}`, `GET`, `GET {roleId}` |
| `MessageController` | base `message`: DM + channel send/get/patch/delete (see Swagger) |

REST convention: same resource, different verb — `PATCH /resource/{id}` to edit,
`DELETE /resource/{id}` to remove. Don't add separate `/edit` / `/delete` URLs.

---

## 7. Adding a new feature (end-to-end checklist)

1. **Model** in `backend.Models`, deriving `BaseEntity` (gets v7 `Id` + `CreatedAt` free).
   Register the `DbSet` and any relationships/constraints in `AppDbContext` (coordinate the
   migration with the infra owner).
2. **DTOs** in `backend.Models.DTO`. List responses extend `PaginatedListDTO`.
3. **Service** returning the tuple contract; do server-scoped auth via
   `VerifyUserAccessAsync`; invalidate any relevant cache key on writes.
4. **Controller** — thin: read `callerId`, clamp paging, call service, map to `ActionResult`.
   Pick the right `[Authorize(Policy=…)]` from `TokenPermissions`.
5. Register the service in `Program.cs` (scoped). Verify in Swagger.

---

## 8. Known issues / TODO for the next dev

6. **`MessageService` TOCTOU on `OnlyFriendsMessages`** — accepted for now, documented
   inline. Revisit only once the "friendship removed mid-send" policy is decided. But for our scale is just ignored for now.
7. **Logging** — every `// log error` placeholder needs a real logger.
