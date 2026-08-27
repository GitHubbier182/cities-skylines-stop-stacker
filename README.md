# Stop Stacker

Stop Stacker is a stable Cities: Skylines mod for busy shared bus stops. It gives supported stops clear berth markers, passenger waiting positions, live service information, and concurrent loading for buses that are fully inside the usable stop area.

Version 2.2.5 is released on Steam Workshop item `3751418194` and in this clean public source repository. It keeps building-owned bus-station platforms under their native lane ownership instead of projecting Stop Stacker berths and passenger waiting positions onto a nearby road. Supported roadside and in-lane bus stops retain their existing behavior.

## Features

- Clear `B#` berth labels for supported pit-backed and in-lane bus stops.
- Live status bubbles showing berth, line, route stop number, and waiting passengers.
- Passenger wait-position assignment beside the service's displayed berth.
- Concurrent unloading and loading for eligible buses inside the usable stop area.
- Guarded dwell reduction after passenger exchange completes.
- Camera-visible stops are prioritised when the overlay opens during an unfinished bounded city rebuild.
- Player-selectable Modern, Futuristic, Old world, or disabled bus-stop signs and dispatch boards.
- Per-stop visual disable and re-enable controls, with an option to disable Stop Stacker service at those stops as well.
- Runtime rebuilding when lines, stops, or roads change, without moving live vanilla stop nodes.
- Shared scan arbitration for load-time and changed-stop berth topology, processed one visible stop per atomic manager step.
- Bounded simulation-thread vehicle discovery for multi-bus service, including expanded 65,536-slot vehicle managers.
- Native launcher registration with optional external UnifiedUI for showing or hiding berth labels and status bubbles, with the shared ScratchyBald toolbar retained as the standalone fallback.
- Saved per-stop disable choices.

## Compatibility

- Requires Harmony 2.2.2-0, Steam Workshop item `2040656402`.
- Observed working alongside Improved Public Transport 3, Improved Public Transport Essentials, Transport Lines Manager 14.6, and basic Express Bus Services settings.
- Stands down from bus departure control when another mod patches `BusAI.CanLeave`, preserving external unbunching or service-spacing decisions.
- Stands down from stop-position adjustment when a supported external stop-position owner is already active.
- Leaves building-owned and other native non-road station platforms under vanilla stop-position and passenger-wait ownership.
- Uses live road geometry for left-hand and right-hand custom-road pavement-side placement.
- Advanced Stop Selection Revisited can affect related stop/platform behavior; report any incorrect interaction with the active mod list and logs.

## Support

For player bug reports, use:

https://github.com/GitHubbier182/scratchys-cities-skylines-mod-support/issues/new?template=bug_report.yml

Include the city context, relevant transport mods, and any `[StopStacker]` lines from `Player.log` or `output_log.txt`.

## Source boundaries

The current code keeps vanilla ownership of transport lines, stop nodes, routing, passenger records, departure safety, line spacing, and ordinary vehicle lifecycle. Stop Stacker adds only its stop-position, presentation, passenger-waiting, multi-bus service, and guarded dwell boundaries.

## Copyright and intellectual property

Copyright © 2026 ScratchyBald. All rights reserved.

This repository is published for source transparency and reference only. No
licence is granted to copy, modify, compile, distribute, repackage, republish,
or incorporate its code or documentation into another project without prior
written permission, except as permitted by applicable law and GitHub's Terms of
Service.

**Stop Stacker** and its associated original branding identify a ScratchyBald
release. They may not be used in a way that falsely suggests authorship,
endorsement, or affiliation. Original concepts and functionality are claimed
only to the extent protected by applicable law.

Cities: Skylines and related marks are the property of their respective owners.
This independent community modification is not affiliated with or endorsed by
Colossal Order or Paradox Interactive.
