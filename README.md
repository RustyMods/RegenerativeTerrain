# Regenerative Terrain

Inspired by Enshrouded - Terrain and vegetation will regenerate over time

## Features

Automatically regenerate terrain and vegetation

Crafting stations will block renegeration so players can safely terraform and build - As soon as crafting station is removed, the terrain will start regenerating

Vegetation on the other hand - If crafting station is nearby, then the vegetation will forever be removed.

## Changelog
```
1.0.0 - Initial release
```

## Configurations

1. General

- Lock Configurations

If installed on server, only admins can change configurations

- Radius

Distance to check for crafting station to block regeneration

- Stations Block

On/Off - If stations should block regeneration

2. Terrain

- Duration

Length of time for terrain to regenerate completely

- Enabled

On/Off - If should regenerate terrain

- Reset Cultivated

If should regenerate cultivated terrain back to grass

- Reset Dirt

If should regenerate dirt back to grass

- Reset Paved

If should regenerate paved back to grass

- Update Frequency

Time between terrain update

3. Vegetation

- Enabled

On/Off - If should regenerate rocks and trees

- Exclude Ores

If should affect rocks that drop ores

- Exclusion

Custom prefab exclusion, ex: Rock_3:MineRock_Copper

- Growth Duration

Length of time for rocks to renegerate (for smaller rocks, ie: MineRock_Tin, GuckSack)

- Respawn Time

Length of time for rocks to respawn (For larger rocks, ie: rock4_copper)

## How It Works

As the player loads a scene, the game begins the compile the terrain. This plugin patches behind the loading and updating of the terrain compiler to check if terrain has been modified. If so, then it will calculate based on the time modified a percentage ratio to multiply the modifications towards zero - the original values. Once the renegeration is complete, it will remove the timer and set the compiler terrain modifications to un-modified.

The timer is set as soon as something saves a modification to the terrain compiler.

For the vegetation, plugin will get behind the destructible prefab or instantiation of the mine rock 5 component. For destructibles, like MineRock_Tin, as soon as the prefab is destroyed, the plugin will spawn a new prefab that has a custom behavior that grows the prefab. Once complete, it replaces it with a new destructible prefab. For mine rock 5 components, as soon as it is created, a timer is set. Once the timer meets the condition, then it destroys the original and replaces it with the original destructible prefab that spawned the mine rock 5. Example: rock4_copper is a destructible prefab with 1 Hit point. Once hit, it is destroyed and replaced with rock4_copper_frac, which is a mine rock 5. The frac prefab is the one that you mine away into pieces. Since these are two different prefabs, the plugin will set the timer as soon as the fractured version is spawned, and will replace the fractured rock with the original rock4_copper, thus regenerating rock.


## Contact information
For Questions or Comments, find <span style="color:orange">Rusty</span> in the Odin Plus Team Discord

[![https://i.imgur.com/XXP6HCU.png](https://i.imgur.com/XXP6HCU.png)](https://discord.gg/v89DHnpvwS)

Or come find me at the [Modding Corner](https://discord.gg/fB8aHSfA8B)

##
If you enjoy this mod and want to support me:
[PayPal](https://paypal.me/mpei)

<span>
<img src="https://i.imgur.com/rbNygUc.png" alt="" width="150">
<img src="https://i.imgur.com/VZfZR0k.png" alt="https://www.buymeacoffee.com/peimalcolm2" width="150">
</span>
