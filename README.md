# Hazardous World

A comprehensive environmental hazard mod for [Techtonica](https://store.steampowered.com/app/1457320/Techtonica/) that adds dangerous zones, hostile flora, and protective equipment to enhance survival gameplay.

## Overview

Hazardous World transforms the underground factory experience by introducing environmental dangers that players must navigate and protect themselves against. From toxic gas clouds to aggressive plant life, this mod adds a layer of survival challenge while providing the tools needed to overcome these obstacles.

## Features

### Environmental Hazard Zones

#### Toxic Zones
- Poisonous gas areas that deal continuous damage
- Recognizable by green fog visual effects
- Found in cave systems and swamp-like areas
- Damage can be reduced by 80% with the Hazmat Suit

#### Radiation Zones
- High-radiation areas near ore deposits and deep underground
- Distinctive yellow glow visual effect
- Higher damage than toxic zones
- Power generators can emit localized radiation fields (configurable)
- Damage can be reduced by 90% with the Radiation Shield

### Hostile Flora System

The mod introduces four types of dangerous plant life that spawn dynamically throughout the world:

#### Spore Plants
- Bulbous plants that emit toxic spore clouds
- Creates a damage zone with configurable radius (default: 6m)
- Periodic spore pulse animations with particle effects
- Deals 8 damage per second by default
- Can be destroyed (50 HP)
- Spawns up to 10 by default across the world

#### Venomous Thorns
- Dangerous thorn bushes with dark red-tipped branches
- Deals contact damage when touched (15 damage per hit)
- Short damage cooldown (0.5 seconds)
- Lower health pool (30 HP) - easier to clear
- Maximum of 15 bushes in the world by default

#### Acid Spitters
- Pitcher-like plants that launch acid projectiles
- Ranged attacks with arcing trajectory
- 12 damage per hit with 12m attack range
- 3-second cooldown between attacks
- Tracks and aims at nearby players
- 40 HP - moderately durable
- Maximum of 8 spitters by default

#### Grasping Vines
- Tendril clusters that slow and trap players
- Reduces movement speed by 50% when grabbed
- Holds players for 2 seconds by default
- Animated tendrils that react to player presence
- 25 HP
- Maximum of 12 vine patches by default

### Protective Equipment

All protective gear is unlocked through the **Hazard Protection** research node (Tier 6 - VICTOR level, requires 100 Green Research Cores).

#### Hazmat Suit
- **Protection:** Reduces toxic damage by 80%
- **Stack Size:** 1
- **Recipe:**
  - 20x Plantmatter Fiber
  - 10x Biobrick
  - 5x Iron Frame
- **Crafting Time:** 20 seconds (Assembler)

#### Radiation Shield
- **Protection:** Reduces radiation damage by 90%
- **Stack Size:** 1
- **Recipe:**
  - 10x Steel Frame
  - 20x Copper Ingot
  - 10x Iron Components
- **Crafting Time:** 30 seconds (Assembler)

#### Antidote
- **Effect:** Instantly removes toxic status effects
- **Stack Size:** 20
- **Recipe:**
  - 5x Shiverthorn
  - 10x Plantmatter
- **Output:** 3x Antidote
- **Crafting Time:** 5 seconds (Assembler)

## Dealing with Hazards

### General Strategy
1. **Scout ahead** - Watch for visual cues like colored fog or unusual plants
2. **Craft protection** - Research and craft appropriate protective equipment before entering hazardous areas
3. **Keep antidotes handy** - Stack antidotes for emergency toxic cure
4. **Clear hostile flora** - Attack and destroy dangerous plants to make areas safe
5. **Manage reactor placement** - Consider radiation zones when placing power generators

### Hazard-Specific Tips

| Hazard Type | Visual Cue | Counter |
|-------------|------------|---------|
| Toxic Zones | Green fog | Hazmat Suit, Antidote |
| Radiation | Yellow glow | Radiation Shield |
| Spore Plants | Purple bulb with particles | Destroy or equip Hazmat Suit |
| Venomous Thorns | Dark bush with red tips | Destroy or avoid |
| Acid Spitters | Green pitcher plant | Destroy from range or take cover |
| Grasping Vines | Tendril cluster | Destroy quickly or avoid |

## Installation

### Using r2modman (Recommended)
1. Open r2modman
2. Select Techtonica
3. Search for "HazardousWorld"
4. Click Install

### Manual Installation
1. Ensure you have all required dependencies installed
2. Download the latest release
3. Extract `HazardousWorld.dll` to your `BepInEx/plugins/` folder
4. Launch the game

## Requirements

| Dependency | Minimum Version |
|------------|-----------------|
| BepInEx | 5.4.21+ |
| EquinoxsModUtils | 6.1.3+ |
| EMUAdditions | 2.0.0+ |
| TechtonicaFramework | 1.0.0+ |

## Configuration

Configuration file location: `BepInEx/config/com.certifired.HazardousWorld.cfg`

### General Settings

| Option | Default | Description |
|--------|---------|-------------|
| Debug Mode | false | Enable detailed debug logging |

### Toxic Zones

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Toxic Zones | true | - | Enable toxic gas zones |
| Toxic Damage Per Second | 5.0 | 0.1-50 | Damage dealt per second in toxic zones |

### Radiation

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Radiation Zones | true | - | Enable radiation hazard zones |
| Radiation Damage Per Second | 8.0 | 0.1-50 | Damage dealt per second in radiation zones |
| Reactors Emit Radiation | true | - | Power generators create radiation fields |
| Reactor Radiation Radius | 5.0 | 1-20 | Radius of reactor radiation zones |

### Flora (General)

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Hostile Flora | true | - | Enable hostile plant damage on contact |
| Flora Damage Per Second | 3.0 | 0.1-20 | Contact damage from hostile flora |
| Max Total Flora | 50 | 10-200 | Maximum total hostile flora entities |

### Spore Plants

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Spore Plants | true | - | Enable spore plant spawning |
| Spore Damage Per Second | 8.0 | 1-30 | Damage from spore clouds |
| Spore Cloud Radius | 6.0 | 2-15 | Radius of spore damage zone |
| Max Spore Plants | 10 | 1-50 | Maximum spore plants in world |

### Venomous Thorns

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Venomous Thorns | true | - | Enable thorn bush spawning |
| Thorn Damage Per Hit | 15.0 | 1-50 | Damage dealt on contact |
| Max Thorn Bushes | 15 | 1-50 | Maximum thorn bushes in world |

### Acid Spitters

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Acid Spitters | true | - | Enable acid spitter spawning |
| Acid Damage Per Hit | 12.0 | 1-40 | Damage from acid projectiles |
| Acid Spit Range | 12.0 | 5-25 | Attack range |
| Max Acid Spitters | 8 | 1-30 | Maximum acid spitters in world |

### Grasping Vines

| Option | Default | Range | Description |
|--------|---------|-------|-------------|
| Enable Grasping Vines | true | - | Enable vine patch spawning |
| Vine Slow Percentage | 0.5 | 0.1-0.9 | Movement speed reduction (0.5 = 50%) |
| Grab Duration | 2.0 | 0.5-5 | How long vines hold the player |
| Max Vine Patches | 12 | 1-40 | Maximum vine patches in world |

### Debug Keys

| Option | Default | Description |
|--------|---------|-------------|
| Spawn Spore Plant Key | F10 | Debug key to spawn spore plant near player |
| Spawn Random Flora Key | F11 | Debug key to spawn random hostile flora |

## Compatibility

- Compatible with other mods using TechtonicaFramework
- Uses the Modded category in the tech tree to avoid conflicts
- Equipment recipes use standard game resources

## Known Issues

- Hazard zone visual effects are placeholder implementations
- Player damage system integration pending full TechtonicaFramework support
- Reactor radiation zone tracking is a placeholder

## Changelog

### [1.5.0] - Current
- Added Venomous Thorns hostile flora
- Added Acid Spitter ranged attackers
- Added Grasping Vines slow/trap mechanic
- Enhanced hostile flora spawning system
- Comprehensive configuration options for all flora types
- Debug spawn keys for testing

### [1.0.0] - 2025-01-05
- Initial release
- Hazmat Suit equipment
- Radiation Shield equipment
- Antidote consumable
- Toxic and radiation zone framework
- Spore Plant hostile flora
- Integration with TechtonicaFramework

## Credits

- **Mod Author:** certifired
- **Development Assistance:** Claude Code (Anthropic)
- **Frameworks:**
  - [EquinoxsModUtils](https://github.com/CubeSuite/TTMod-EquinoxsModUtils) by Equinox
  - [EMUAdditions](https://github.com/CubeSuite/TTMod-EMUAdditions) by Equinox
  - TechtonicaFramework by certifired

## License

This mod is licensed under the **GNU General Public License v3.0 (GPL-3.0)**.

You are free to:
- Use the mod for any purpose
- Study how the mod works and modify it
- Distribute copies of the mod
- Distribute modified versions of the mod

Under the following conditions:
- Source code must be made available when distributing
- Modifications must be released under the same license
- Changes must be documented
- Original copyright and license notices must be preserved

For the full license text, see: https://www.gnu.org/licenses/gpl-3.0.en.html

## Links

- **Source Code:** [GitHub](https://github.com/certifired/HazardousWorld) *(if available)*
- **Bug Reports:** Please report issues on the mod's GitHub Issues page or Thunderstore comments
- **Techtonica Discord:** [Official Discord](https://discord.gg/techtonica)
- **Techtonica Modding Discord:** [Modding Community](https://discord.gg/equinox-modding)
- **Thunderstore:** [HazardousWorld on Thunderstore](https://thunderstore.io/c/techtonica/p/certifired/HazardousWorld/) *(when published)*

---

*Made with dedication for the Techtonica modding community*
