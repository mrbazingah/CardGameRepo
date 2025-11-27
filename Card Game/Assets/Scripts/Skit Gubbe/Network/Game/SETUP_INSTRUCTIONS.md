# Multiplayer Scene Setup Instructions

## ⚠️ IMPORTANT: NetworkObjects Must Be Spawned!

NetworkObjects in Fusion **must be spawned at runtime**, not manually placed in the scene. However, you can create a **prefab** with all serialized fields set, then spawn instances of it.

---

## Method 1: Using Prefab + Spawner (RECOMMENDED)

### Step 1: Create NetworkPlayerHand Prefab

1. **In your scene, create a test NetworkPlayerHand:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkPlayerHand_Template`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkPlayerHand`

3. **Set NetworkObject:**
   - **Object Interest**: "Input Authority Only"
   - **Auto Host Authority**: ✅ Checked

4. **Create Transform Children:**
   - Create 3 empty GameObjects as children:
     - `HandTransform` (for main hand cards)
     - `UnderSideTransform` (for under-side cards)
     - `OverSideTransform` (for over-side cards)
   - Position `HandTransform` at: **X: 0, Y: -4, Z: 0** (bottom of screen)
   - Position `UnderSideTransform` and `OverSideTransform` nearby (e.g., Y: -3.5)

5. **Configure NetworkPlayerHand Component:**
   - **handTransform**: Drag `HandTransform` child
   - **underSideTransform**: Drag `UnderSideTransform` child
   - **overSideTransform**: Drag `OverSideTransform` child
   - **baseCardSpacing**: `0.7`
   - **maxHandWidth**: `8`
   - **sideBaseCardSpacing**: `1.5`
   - **sideMaxHandWidth**: `15`
   - **popUpHeight**: `0.5`
   - **overSideOffset**: `0.15`
   - **lerpSpeed**: `15`
   - **isTurnPos**: `(0, -4)`
   - **isNotTurnPos**: `(0, -4.7)`
   - **playChanceDelay**: `0.5`
   - **cardLayerMask**: Set to your card layer (or leave as "Everything")
   - **UI Elements**: Assign your buttons, text, etc.

6. **Create Prefab:**
   - Drag `NetworkPlayerHand_Template` from Hierarchy to Project window
   - This creates your prefab
   - Delete the template from the scene (keep the prefab)

7. **Add Prefab to Network Project:**
   - Open Fusion → Network Project Settings
   - Go to "Prefabs" tab
   - Add your `NetworkPlayerHand` prefab to the list
   - Note the **Prefab ID** (you'll need it)

### Step 2: Setup NetworkPlayerHandSpawner

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkPlayerHandSpawner`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkPlayerHandSpawner`

3. **Set NetworkObject:**
   - **Auto Host Authority**: ✅ Checked

4. **Assign in NetworkPlayerHandSpawner:**
   - **Player Hand Prefab**: Select your `NetworkPlayerHand` prefab from the Network Project prefabs
   - **Player Hand Position**: `(0, -4)`

---

---

## Step 3: Create NetworkOpponentHandDisplay (1 total)

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkOpponentHandDisplay`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkOpponentHandDisplay`

3. **Set NetworkObject:**
   - **Object Interest**: "All" (everyone sees opponent cards)
   - **Auto Host Authority**: ✅ Checked

4. **Create Transform Child:**
   - Create empty GameObject as child: `OpponentHandTransform`
   - Position at: **X: 0, Y: 4, Z: 0** (top of screen)

5. **Assign in NetworkOpponentHandDisplay Component:**
   - **opponentHandTransform**: Drag `OpponentHandTransform` child
   - **faceDownCardPrefab**: Drag your NetworkedCard prefab (same one used for cards)
   - **baseCardSpacing**: `0.7` (or `150` if using UI units)
   - **maxHandWidth**: `8` (or `1000` if using UI units)
   - **opponentCardCountText**: (Optional) Drag TextMeshProUGUI for card count
   - **cardCountTextOffset**: `(1, 1.2)`

---

## Step 4: Verify Scene Setup

Your scene should have:

1. ✅ **1x NetworkPlayerHandSpawner** (spawns hands for players)
2. ✅ **1x NetworkOpponentHandDisplay** (shows opponent cards at top)
3. ✅ **1x NetworkCardGenerator** (with card prefab assigned)
4. ✅ **1x NetworkPile** (for the central pile)
5. ✅ **1x GameManagerNetwork** (game manager)
6. ✅ **1x NetworkRunnerHandler** (or NetworkRunner from lobby)

---

## How It Works

1. **When players join:**
   - `NetworkPlayerHandSpawner` spawns a `NetworkPlayerHand` for each player
   - Each hand is spawned with that player's InputAuthority
   - Only the owner can see/interact with their hand

2. **When cards are dealt:**
   - `NetworkCardGenerator` finds `NetworkPlayerHand` objects by matching `InputAuthority`
   - Cards are spawned and added to the correct player's hand

3. **Opponent display:**
   - `NetworkOpponentHandDisplay` finds the opponent's `NetworkPlayerHand`
   - It reads the `NetworkedHandCount` and displays face-down cards at the top

---

## Troubleshooting

**Cards not being dealt?**
- ✅ Make sure `NetworkPlayerHandSpawner` is in the scene and has the prefab assigned
- ✅ Check that the prefab is added to Network Project Settings → Prefabs
- ✅ Verify `GameStarted` is true before dealing begins
- ✅ Check Console for "Could not find NetworkPlayerHand for player" warnings

**Player can't see their cards?**
- ✅ Check prefab's NetworkObject "Object Interest" is "Input Authority Only"
- ✅ Verify the spawned NetworkPlayerHand has the correct InputAuthority

**Opponent cards not showing?**
- ✅ Check NetworkOpponentHandDisplay has "Object Interest" set to "All"
- ✅ Verify it can find the opponent's NetworkPlayerHand (check Console logs)

---

## Quick Checklist

- [ ] Created NetworkPlayerHand prefab with all serialized fields set
- [ ] Added prefab to Network Project Settings → Prefabs
- [ ] Created NetworkPlayerHandSpawner in scene and assigned prefab
- [ ] Created NetworkOpponentHandDisplay in scene
- [ ] All other network objects (CardGenerator, Pile, GameManager) are in scene
- [ ] Tested with 2 players - cards should be dealt automatically

### For EACH player (you need 2):

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkPlayerHand_Player1` (and `NetworkPlayerHand_Player2` for the second)

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkPlayerHand`

3. **Set NetworkObject:**
   - In NetworkObject component:
     - **Object Interest**: Set to "Input Authority Only" (so only the owner sees it)
     - **Auto Host Authority**: ✅ Checked

4. **Create Transform Children:**
   - Create 3 empty GameObjects as children:
     - `HandTransform` (for main hand cards)
     - `UnderSideTransform` (for under-side cards)
     - `OverSideTransform` (for over-side cards)
   - Position `HandTransform` at: **X: 0, Y: -4, Z: 0** (bottom of screen)
   - Position `UnderSideTransform` and `OverSideTransform` nearby (e.g., Y: -3.5)

5. **Assign in NetworkPlayerHand Component:**
   - **handTransform**: Drag `HandTransform` child
   - **underSideTransform**: Drag `UnderSideTransform` child
   - **overSideTransform**: Drag `OverSideTransform` child
   - **baseCardSpacing**: `0.7`
   - **maxHandWidth**: `8`
   - **sideBaseCardSpacing**: `1.5`
   - **sideMaxHandWidth**: `15`
   - **popUpHeight**: `0.5`
   - **overSideOffset**: `0.15`
   - **lerpSpeed**: `15`
   - **isTurnPos**: `(0, -4)`
   - **isNotTurnPos**: `(0, -4.7)`
   - **playChanceDelay**: `0.5`
   - **cardLayerMask**: Set to your card layer (or leave as "Everything")

6. **UI Elements (if you have them):**
   - **endTurnButton**: Drag your End Turn button GameObject
   - **readyButton**: Drag your Ready button GameObject
   - **chanceNotice**: Drag your Chance notice GameObject
   - **cardAmountText**: Drag your card count TextMeshProUGUI
   - **cardAmountTextOffset**: `(-1, 1.2)`

---

## Step 2: Create NetworkOpponentHandDisplay (1 total)

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkOpponentHandDisplay`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkOpponentHandDisplay`

3. **Set NetworkObject:**
   - **Object Interest**: Set to "All" (everyone sees opponent cards)
   - **Auto Host Authority**: ✅ Checked

4. **Create Transform Child:**
   - Create empty GameObject as child: `OpponentHandTransform`
   - Position at: **X: 0, Y: 4, Z: 0** (top of screen)

5. **Assign in NetworkOpponentHandDisplay Component:**
   - **opponentHandTransform**: Drag `OpponentHandTransform` child
   - **faceDownCardPrefab**: Drag your NetworkedCard prefab (same one used for cards)
   - **baseCardSpacing**: `150` (or `0.7` if using world units)
   - **maxHandWidth**: `1000` (or `8` if using world units)
   - **opponentCardCountText**: (Optional) Drag TextMeshProUGUI for card count
   - **cardCountTextOffset**: `(1, 1.2)`

---

## Step 3: Create NetworkPlayerHandAssigner (1 total)

1. **Create GameObject:**
   - Right-click in Hierarchy → Create Empty
   - Name it: `NetworkPlayerHandAssigner`

2. **Add Components:**
   - Add Component → `NetworkObject`
   - Add Component → `NetworkPlayerHandAssigner`

3. **Set NetworkObject:**
   - **Auto Host Authority**: ✅ Checked

4. **Assign in NetworkPlayerHandAssigner Component:**
   - **Available Hands**: 
     - Set Size to `2`
     - Drag `NetworkPlayerHand_Player1` to Element 0
     - Drag `NetworkPlayerHand_Player2` to Element 1

**This script will automatically assign the manually placed NetworkPlayerHand objects to players when they join.**

---

## Step 4: Verify Scene Setup

Your scene should have:

1. ✅ **2x NetworkPlayerHand** objects (at bottom, Y: -4)
2. ✅ **1x NetworkOpponentHandDisplay** object (at top, Y: 4)
3. ✅ **1x NetworkCardGenerator** (with card prefab assigned)
4. ✅ **1x NetworkPile** (for the central pile)
5. ✅ **1x GameManagerNetwork** (game manager)
6. ✅ **1x NetworkRunnerHandler** (or NetworkRunner from lobby)

---

## Step 5: How It Works

1. When players join, the NetworkPlayerHand objects will be assigned input authority automatically
2. NetworkCardGenerator deals cards by finding NetworkPlayerHand objects matching each player's InputAuthority
3. Each player only sees their own NetworkPlayerHand (because of Input Authority Only)
4. NetworkOpponentHandDisplay shows face-down cards at the top for the opponent

---

## Troubleshooting

**Cards not being dealt?**
- Make sure NetworkPlayerHand objects exist in the scene BEFORE cards are dealt
- Check that NetworkCardGenerator can find them (Debug.Log in `DealInitialHands`)
- Verify GameStarted is true before dealing begins

**Player can't see their cards?**
- Check NetworkObject "Object Interest" is set to "Input Authority Only"
- Verify the NetworkPlayerHand has the correct InputAuthority assigned

**Opponent cards not showing?**
- Check NetworkOpponentHandDisplay has "Object Interest" set to "All"
- Verify it can find the opponent's NetworkPlayerHand

