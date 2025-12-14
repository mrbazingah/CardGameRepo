# Multiplayer Scene Setup Instructions

## ⚠️ IMPORTANT: NetworkObjects Must Be Spawned!

NetworkObjects in Fusion **must be spawned at runtime**, not manually placed in the scene. Create **prefabs** with all serialized fields set, then spawn instances of them.

---

## Game Flow Overview

Understanding the flow helps with setup:

1. **Lobby Scene**: Players join, press Ready, host starts game
2. **Multiplayer Scene Loads**: NetworkRunner persists from lobby
3. **Hand Spawning**: `NetworkPlayerHandSpawner` spawns a `NetworkPlayerHand` for each player
4. **In-Game Ready**: Players press Ready button (in their hand UI)
5. **Game Starts**: When both ready, `GameStarted = true`
6. **Cards Dealt**: `NetworkCardGenerator` deals cards to each hand
7. **Start Player Assigned**: Player with lowest card (excluding 2, 10) goes first
8. **Gameplay**: Turn-based card playing until a player empties their hand

---

## Step 1: Create NetworkPlayerHand Prefab

### 1.1 Create the Template

1. **In your scene, create a test object:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkPlayerHand_Template`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkPlayerHand`

3. **Configure NetworkObject:**
   - **Object Interest**: "Input Authority Only" (only owner sees their cards)
   - **Auto Host Authority**: ✅ Checked

### 1.2 Create Transform Children

Create 3 empty GameObjects as children:

| Child Name | Purpose | Position |
|------------|---------|----------|
| `HandTransform` | Main hand cards | X: 0, Y: -4, Z: 0 |
| `UnderSideTransform` | Under-side cards | X: 0, Y: -3.5, Z: 0 |
| `OverSideTransform` | Over-side cards | X: 0, Y: -3.5, Z: 0 |

### 1.3 Configure NetworkPlayerHand Component

| Field | Value |
|-------|-------|
| **handTransform** | Drag `HandTransform` child |
| **underSideTransform** | Drag `UnderSideTransform` child |
| **overSideTransform** | Drag `OverSideTransform` child |
| **baseCardSpacing** | `0.7` |
| **maxHandWidth** | `8` |
| **sideBaseCardSpacing** | `1.5` |
| **sideMaxHandWidth** | `15` |
| **popUpHeight** | `0.5` |
| **overSideOffset** | `0.15` |
| **lerpSpeed** | `15` |
| **isTurnPos** | `(0, -4)` |
| **isNotTurnPos** | `(0, -4.7)` |
| **playChanceDelay** | `0.5` |
| **cardLayerMask** | Your card layer (or "Everything") |

### 1.4 Assign UI Elements (Optional)

| Field | Description |
|-------|-------------|
| **endTurnButton** | Button to end turn |
| **readyButton** | Button to ready up (shown before game starts) |
| **chanceNotice** | UI shown when chance is available |
| **cardAmountText** | TextMeshProUGUI showing card count |
| **cardAmountTextOffset** | `(-1, 1.2)` |

### 1.5 Create the Prefab

1. Drag `NetworkPlayerHand_Template` from Hierarchy to Project window
2. This creates your prefab
3. **Delete** the template from the scene (keep only the prefab)

### 1.6 Register Prefab with Fusion

1. Open **Tools → Fusion → Network Project Config**
2. In Inspector, find the **Prefabs** section
3. Add your `NetworkPlayerHand` prefab to the list
4. Note the **Prefab ID** assigned

---

## Step 2: Create NetworkPlayerHandSpawner (Scene Object)

This spawns player hands when players join.

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkPlayerHandSpawner`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkPlayerHandSpawner`

3. **Configure NetworkObject:**
   - **Auto Host Authority**: ✅ Checked

4. **Configure NetworkPlayerHandSpawner:**
   - **Player Hand Prefab**: Select your `NetworkPlayerHand` prefab from Network Project Config
   - **Player Hand Position**: `(0, -4)`

---

## Step 3: Create NetworkOpponentHandDisplay (Scene Object)

This displays face-down cards representing the opponent's hand at the top of the screen.

**Note:** This is a regular MonoBehaviour (not NetworkBehaviour). Each client locally tracks and displays the opponent's cards.

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkOpponentHandDisplay`

2. **Add Component:**
   - Add Component → `NetworkOpponentHandDisplay`
   - ❌ Do NOT add NetworkObject (this is local only)

3. **Create Transform Child:**
   - Create empty GameObject as child: `OpponentHandTransform`
   - Position at: **X: 0, Y: 4, Z: 0** (top of screen)

4. **Create Face-Down Card Prefab:**
   - Create a simple prefab with:
     - SpriteRenderer (card back sprite)
     - Collider2D (optional)
   - Save as `FaceDownCard` prefab in your project (NOT in Network Prefabs)

5. **Configure NetworkOpponentHandDisplay:**

| Field | Value |
|-------|-------|
| **opponentHandTransform** | Drag `OpponentHandTransform` child |
| **faceDownCardPrefab** | Drag your local `FaceDownCard` prefab |
| **baseCardSpacing** | `0.7` |
| **maxHandWidth** | `8` |
| **opponentCardCountText** | (Optional) TextMeshProUGUI |
| **cardCountTextOffset** | `(1, 1.2)` |

---

## Step 4: Create Other Network Objects

### NetworkCardGenerator

1. Create GameObject: `NetworkCardGenerator`
2. Add Components: `NetworkObject`, `NetworkCardGenerator`
3. Configure:
   - **cardPrefab**: Your `NetworkedCard` prefab (must be in Network Project Config)
   - **cardSprites**: Array of card face sprites
   - **cardsPerPlayer**: `5`

### NetworkPile

1. Create GameObject: `NetworkPile`
2. Add Components: `NetworkObject`, `NetworkPile`
3. Create child `PileTransform` at center of screen
4. Configure:
   - **pileTransform**: Drag `PileTransform` child
   - **cardPrefab**: Your `NetworkedCard` prefab

### GameManagerNetwork

1. Create GameObject: `GameManagerNetwork`
2. Add Components: `NetworkObject`, `GameManagerNetwork`
3. Configure UI elements (start button, pause menu, win menu, etc.)

### NetworkRunnerHandler (Optional)

Only needed if you want to handle runner setup in the game scene.

1. Create GameObject: `NetworkRunnerHandler`
2. Add Component: `NetworkRunnerHandler` (no NetworkObject needed)

---

## Step 5: Final Scene Checklist

Your Multiplayer Scene should have:

| Object | Components | Notes |
|--------|------------|-------|
| ✅ NetworkPlayerHandSpawner | NetworkObject, NetworkPlayerHandSpawner | Spawns hands for players |
| ✅ NetworkOpponentHandDisplay | NetworkOpponentHandDisplay only | Local display (no NetworkObject!) |
| ✅ NetworkCardGenerator | NetworkObject, NetworkCardGenerator | Deals cards |
| ✅ NetworkPile | NetworkObject, NetworkPile | Central pile |
| ✅ GameManagerNetwork | NetworkObject, GameManagerNetwork | Game state |
| ✅ NetworkRunnerHandler | NetworkRunnerHandler | (Optional) Runner handling |

---

## How It Works

### Player Hand Spawning
1. When players join, `NetworkPlayerHandSpawner` detects them
2. Spawns a `NetworkPlayerHand` with that player's InputAuthority
3. Only the owner can see/interact with their hand

### Card Dealing
1. `NetworkCardGenerator` waits for `GameStarted = true`
2. Finds `NetworkPlayerHand` objects by matching `InputAuthority`
3. Spawns cards and assigns them via RPC

### Opponent Display
1. Each client's `NetworkOpponentHandDisplay` finds the opponent's `NetworkPlayerHand`
2. Reads the `NetworkedHandCount` property (synced via Fusion)
3. Locally creates/destroys face-down card GameObjects to match

### Turn Assignment
1. After cards are dealt (1.5 second delay)
2. Checks each player's hand for lowest card (excluding 2 and 10)
3. Player with lowest card goes first

---

## Troubleshooting

### Cards not being dealt?
- ✅ Check `NetworkPlayerHandSpawner` has the prefab assigned
- ✅ Verify prefab is in Network Project Config → Prefabs
- ✅ Ensure `GameStarted` becomes true (players must ready up)
- ✅ Check Console for "Could not find NetworkPlayerHand" warnings

### Player can't see their cards?
- ✅ Check prefab's NetworkObject "Object Interest" is "Input Authority Only"
- ✅ Verify the spawned NetworkPlayerHand has correct InputAuthority
- ✅ Check that cards are spawned with the correct player's InputAuthority

### Opponent cards not showing?
- ✅ Verify `NetworkOpponentHandDisplay` does NOT have a NetworkObject
- ✅ Check `faceDownCardPrefab` is assigned (local prefab, not network prefab)
- ✅ Ensure opponent's `NetworkPlayerHand` has spawned
- ✅ Check Console for "Found opponent hand" log message

### Game not starting?
- ✅ Both players must press the Ready button
- ✅ Check `PlayersReady` count in GameManagerNetwork
- ✅ Verify UI buttons are properly assigned

### Wrong player starts?
- ✅ Start player is assigned 1.5 seconds after game starts
- ✅ Cards must be dealt before start player is determined
- ✅ Check Console for "Start player assigned" log

---

## Quick Setup Checklist

- [ ] Created `NetworkPlayerHand` prefab with all fields configured
- [ ] Added prefab to **Tools → Fusion → Network Project Config → Prefabs**
- [ ] Created `NetworkPlayerHandSpawner` in scene with prefab assigned
- [ ] Created `NetworkOpponentHandDisplay` in scene (NO NetworkObject!)
- [ ] Created local `FaceDownCard` prefab for opponent display
- [ ] Created `NetworkCardGenerator` with card prefab assigned
- [ ] Created `NetworkPile` with pile transform and card prefab
- [ ] Created `GameManagerNetwork` with UI elements assigned
- [ ] Tested with 2 players - both ready up, cards dealt, game plays
