## 1.0.0

- Initial release

## 1.0.1

- Changed README to say it spawns inside. There was a small mistake
- Fixed an error in the console where it came to finding nodes
- Changed Rarity to 25
- Broke the infinite loop iteration for teleport
- Made multiplayer more stable
- Improved Collider check for a better experience
- We now have a big vision check which make sure you don't look at 173 in any way. Not just the middle of his position
- Added The snap neck sound upon kill
- Added more configs:
  - SpawnWeight;
  -  TicksAfterKilling;
  -  AmmountOfDamage;
  -  LeapDistance;
  -  NumberOfLeapPerFrame;
  -  AmmountOfTimeWaiting;

## 1.0.2
- Removed NavMesh util from DLL build
- Modified Nav Mesh agent for it to be smaller
- Recentered some assets such as the map dot and collision detection
- Renamed object ID to 173 ( For lethal,doesn't affect gameplay )
- Changed Vision Collider to cover more of the feet