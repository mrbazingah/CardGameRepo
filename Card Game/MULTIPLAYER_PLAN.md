# Multiplayer Architecture Plan

## Overview

2-player card game using Unity Gaming Services (UGS). Only card actions are synced over the network. All local UI state (animations, hand sorting, pile/deck transfers) runs on each client independently.

**Stack:**
- Unity Gaming Services Lobbies — matchmaking (already built)
- Unity Relay — peer-to-peer transport without a dedicated server
- Netcode for GameObjects (NGO) — RPC and ownership framework

---

## Architecture Principle

> Only sync actions, not state.

The host is the authority for the initial deal. After that, both clients maintain their own local game state and only broadcast play events to each other. Neither client can see the other's hand — they only see what has been played.

---

## Component Breakdown

### 1. Lobby (Already Built — NetworkLobby.cs)

**What it does:**
- Host creates lobby, guest joins via room code
- Stores lobby settings (CardsPerPlayer, CanChance) in lobby data
- Host clicks Start Game → sets `GameStarted = 1` in lobby data
- Guest detects flag via poll loop → both load `Multiplayer Scene`

**What it hands off to the next system:**
- Both players are now in `Multiplayer Scene`
- The lobby settings (CardsPerPlayer, CanChance) are read from `NetworkLobby.Instance` before it is destroyed

---

### 2. Relay Setup (New — RelayManager.cs)

**What it does:**
- Bridges the lobby and the NGO session
- Runs immediately when both players enter `Multiplayer Scene`

**Flow:**

```
Host enters Multiplayer Scene
  → Creates a Relay allocation (2 players)
  → Gets a Relay join code
  → Writes join code back to the lobby data ("RelayCode")
  → Starts NGO as Host using the Relay allocation

Guest enters Multiplayer Scene
  → Polls lobby data until "RelayCode" appears
  → Joins the Relay allocation using that code
  → Starts NGO as Client
```

**Key calls:**
- `RelayService.Instance.CreateAllocationAsync(2)` — host only
- `RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId)` — host gets shareable code
- `RelayService.Instance.JoinAllocationAsync(joinCode)` — guest joins
- `NetworkManager.Singleton.StartHost()` / `StartClient()` — begins NGO session

**How it connects to lobby:**
- RelayManager reads `NetworkLobby.Instance` for lobby ID
- Writes the Relay join code into lobby data so the guest can fetch it
- Once NGO is running, the lobby is no longer needed and can be left

---

### 3. Game Manager (New — MultiplayerGameManager.cs)

**What it does:**
- Sits on a NetworkObject in the scene (host-owned)
- Orchestrates the game flow after both players are connected

**Flow:**

```
OnClientConnectedCallback fires for both players
  → Once 2 players are connected, host begins the deal

Host deals cards:
  → Generates full deck
  → Shuffles
  → Splits into 2 hands based on CardsPerPlayer setting
  → Sends each player their hand via a targeted ClientRpc

Game loop begins:
  → Host decides who goes first (coin flip / random)
  → Broadcasts turn state to both clients
```

**Why the host deals:**
Both clients must agree on who has what cards from the start. If each client dealt their own hand, the decks would differ. The host is the single source of truth for the initial deal only.

---

### 4. Card Data (CardData.cs / Shared)

**What gets sent over the network:**
A card is represented as a plain serializable struct — no MonoBehaviour, no GameObject reference.

```csharp
public struct CardNetData : INetworkSerializable
{
    public int CardId;      // index into a shared card database
    public int Value;
    public int Suit;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardId);
        serializer.SerializeValue(ref Value);
        serializer.SerializeValue(ref Suit);
    }
}
```

Both clients reference the same shared card database (a ScriptableObject list) so only the ID needs to travel over the wire — the receiver looks up the full card data locally.

---

### 5. Network Actions (MultiplayerGameManager.cs — RPCs)

These are the only things ever sent over the network during gameplay.

#### Host → Client: Deal Hand
```
[ClientRpc] DealHandClientRpc(CardNetData[] hand, ClientRpcParams target)
```
- Called once at game start
- Sent privately to each client (using `ClientRpcParams` to target one connection)
- Each client receives only their own cards — the opponent never sees this RPC

#### Client → Host → All: Play Card
```
[ServerRpc] PlayCardServerRpc(CardNetData card)         // client sends to host
[ClientRpc] CardPlayedClientRpc(ulong playerId, CardNetData card)  // host broadcasts to all
```
- When a player plays a card, they call `PlayCardServerRpc`
- Host validates it is that player's turn and the card is legal
- Host calls `CardPlayedClientRpc` so both clients update their display

#### Host → All: Turn Change
```
[ClientRpc] SetTurnClientRpc(ulong activePlayerId)
```
- Sent by host whenever the turn changes
- Each client enables/disables their interaction based on whether it is their turn

#### Host → All: Game Over
```
[ClientRpc] GameOverClientRpc(ulong winnerId)
```
- Host determines win condition and broadcasts result

---

### 6. Local Game State (LocalGameState.cs)

**What it does:**
- Manages the local player's hand, deck, and pile entirely on-device
- Never synced — each client owns their own copy

**Responsibilities:**
- Stores the local hand as a `List<CardNetData>` (populated by `DealHandClientRpc`)
- Handles moving cards between hand → play area → discard pile locally
- Fires UI events so the card display updates

**What it does NOT do:**
- It does not know anything about the opponent's hand
- It does not send its state over the network
- It only calls `PlayCardServerRpc` when the player commits a play

---

### 7. UI Layout (LobbyUIManager.cs or MultiplayerUIManager.cs)

**Layout rule:**
- Bottom half = local player (always, regardless of host/guest)
- Top half = opponent (mirrored display)

**Local player area (bottom):**
- Shows actual card faces from `LocalGameState`
- Cards are interactive only when it is the local player's turn
- Pile and deck rendered locally

**Opponent area (top):**
- Card backs shown for cards in opponent's hand (count only — no values)
- When opponent plays a card (`CardPlayedClientRpc` fires), the played card is revealed in the opponent's play area
- Discard pile rendered from received play events

**How it knows who is local:**
```csharp
bool isLocalPlayer = NetworkManager.Singleton.LocalClientId == playerId;
```

---

## Full Flow Diagram

```
[Start Scene]
    ↓ host creates lobby / guest joins via code
[Multiplayer Lobby Scene]
    ↓ host clicks Start Game → GameStarted flag set in lobby
[Multiplayer Scene loads for both]
    ↓
RelayManager runs
    Host: creates Relay allocation → writes RelayCode to lobby
    Guest: polls lobby → reads RelayCode → joins Relay
    Both: NGO session starts (Host/Client)
    ↓
MultiplayerGameManager detects 2 players connected
    Host: generates deck → deals hands → sends via targeted ClientRpc
    Both: receive their hand → LocalGameState populated → UI renders
    ↓
Host sends SetTurnClientRpc → game loop begins
    ↓
Player plays card:
    Local: card removed from hand in LocalGameState
    Network: PlayCardServerRpc → validated by host → CardPlayedClientRpc
    Both: played card appears in play area, opponent's card count decreases
    ↓
Host checks win condition after each play
    → GameOverClientRpc when game ends
```

---

## Files To Create

| File | Purpose |
|---|---|
| `RelayManager.cs` | Creates/joins Relay allocation, starts NGO Host or Client |
| `MultiplayerGameManager.cs` | NetworkObject, deals cards, owns all RPCs, enforces turn order |
| `LocalGameState.cs` | Local hand/deck/pile logic, no networking |
| `MultiplayerUIManager.cs` | Renders bottom (local) and top (opponent) areas from local state + received events |
| `CardNetData.cs` | Serializable card struct for network transmission |
| `CardDatabase.cs` | ScriptableObject — shared card definitions looked up by ID |

---

## What Is and Is Not Synced

| Data | Synced? | How |
|---|---|---|
| Initial hand | Yes (private) | Targeted ClientRpc from host |
| Card played | Yes (public) | ServerRpc → ClientRpc |
| Turn state | Yes | ClientRpc from host |
| Win/loss | Yes | ClientRpc from host |
| Hand order / sorting | No | Local only |
| Pile / deck animations | No | Local only |
| Opponent's unplayed cards | No | Only card back count shown |
| Lobby settings (CardsPerPlayer etc.) | Read once | From NetworkLobby at scene load |
