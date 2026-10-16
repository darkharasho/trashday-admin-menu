# Manual verification, 0.1.0

Host a lobby (ideally with a second client) with the mod deployed. Tick each line.

## Open and close
- [ ] F1 in the main menu does nothing.
- [ ] F1 in a level you host opens the menu; the cursor is free; moving the mouse doesn't turn the camera; WASD doesn't move you.
- [ ] F1 again closes it; the cursor locks and movement and look work again.
- [ ] Esc closes it and the pause menu does NOT open. Esc again (menu closed) opens the pause menu as normal.
- [ ] With the pause menu open, F1 does nothing.
- [ ] The Close button closes it.
- [ ] As a client (second instance), F1 shows "Admin menu: host only" and nothing opens.
- [ ] Open the menu, then leave to the main menu: the menu is gone and the main menu cursor works.

## Players
- [ ] Both players are listed; the host is first, marked "(you)".
- [ ] Kill the client: they die, the row shows Dead within a second.
- [ ] Revive them: they're alive and at full health.
- [ ] Heal after taking damage restores health.
- [ ] Bring to me puts them just in front of you, facing you; Go to puts you behind them.
- [ ] Invincible (everyone) on: hazards and falls do no damage. Kill still works. Off: damage again.
- [ ] Invincible stays on after a game restart (config persisted).

## Items
- [ ] The list shows readable names (not raw keys) and rarities.
- [ ] Typing filters it; clearing the search shows everything.
- [ ] Spawn 1 / 5 / 10 drops that many in front of you; the client sees them and can pick them up.
- [ ] +$10 / +$100 / +$1000 add to team money; the label updates.
- [ ] Custom amount "250" adds 250; "abc", "-5" and "0" show an error and add nothing.

## Cart
- [ ] Summary shows panels intact and damage.
- [ ] Bring cart to me puts it about 3 m ahead, upright.
- [ ] Tip the cart over; Flip upright rights it.
- [ ] Break panels; Repair restores them and the summary shows all intact.
- [ ] Invincible cart on: crashes break no panels and loosen no wheels.

## Level
- [ ] Next checkpoint moves everyone and the cart; after the last one it reports that all are reached.
- [ ] Complete level needs two clicks and then ends the level.
- [ ] Teleport point buttons move everyone and the cart.

## Robustness
- [ ] `BepInEx/LogOutput.log` shows "AdminMenu 0.1.0 loaded" and "Item catalog: N items", and no errors.
