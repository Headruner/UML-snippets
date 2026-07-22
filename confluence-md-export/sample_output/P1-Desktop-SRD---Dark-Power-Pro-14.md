<!--
source: https://bequiet.atlassian.net/wiki/spaces/ldiod/pages/518324294/P1+Desktop+SRD+-+Dark+Power+Pro+14
space: LDP3 (Listan / Developex - IO Center Desktop)
page-id: 518324294
version: 4 (2025-10-16)
exported: 2026-07-20 (Confluence storage format)
note: images referenced by filename; binaries not downloaded (see images.csv).
-->

# P1 Desktop SRD - Dark Power Pro 14

# Changelog

| Date | Editor | Note |
| --- | --- | --- |
| 2025-10-16 | @Martin Pajenkamp | Added schedule |

# Glossary

- P1 – work name of power supply unit 1 aka Dark Power Pro 14 IO
- A1 – work name of air cooler 1 aka Dark Rock Pro 6 IO LCD
- W1 – work name of water cooler 1 aka Light Loop IO LCD
- SC – work name of small controller aka IO Controller
- BC – work name of big controller aka IO Controller Pro
- CS – Cooling Studio (formerly GCFM - Global Fan Controller Module), a feature that allows configuration of all fans in a computer
- HRM – Hardware Resource Monitor, a popup screen on the main page of IO Center that display system information and is fully configurable

# Product overview

P1 is a premium power supply unit with an integrated 135mm fan that can run in zero rpm (semi-passive) mode. The fan is PWM controlled. The 12V rails can be combined to a single rail, or split into 6 rails, named 12V1 to 12V6. The efficiency is rated at 80 PLUS and Cybenetics Titanium.  
P1 is connected directly to the motherboard via internal USB header.  
No parts of this PSU feature any ARGB LED illumination.

P1 comes in two wattages:

- Dark Power Pro 14 IO 1600W
- Dark Power Pro 14 IO 1300W

Each wattage is available in different local SKUs (EU, US, U2), however this has no relevance for the implementation of the software.

# System operation

![system-operation.png](assets/system-operation.png)

# General specifications

- P1 has a unique ID that is identified by IO Center
- P1 is displayed in a tile on the main screen of IO Center
- P1 has a dedicated product page in the left side menu, grouped under the new “Power supply” tab
- P1 is integrated in the HRM

# Page 1: CONFIGURATION

- Left side
  - RAIL MODE
    - MULTI / SINGLE (Default: MULTI)
  - FAN MODE
    - SEMI-PASSIVE / PERFORMANCE (Default: SEMI-PASSIVE)
    - Show the currently active fan curve in graph (Y axis: RPM; X axis: Either Load % or Temperature (tbd))
    - These fan curves are predefined and can NOT be edited by the user
    - SEMI-PASSIVE fan curve:

![semi-passive-curve.png](assets/semi-passive-curve.png)

    - PERFORMANCE fan curve:

![performance-curve.png](assets/performance-curve.png)

  - POWER COST SETTINGS
    - Field to enter cost per KWh
    - Instead of a currency, we display an icon of a coin with the number 1 on it, to avoid this mess altogether.
- Right side has tiles with information
  - Device details
    - Render image of the detected P1, view at the cable connectors
    - Model name
    - Serial number
    - Manufacturing date
  - Power Consumption
    - Efficiency
    - Session length
    - Total runtime
    - Total power draw
  - Power Cost Monitor
    - Use coin symbol instead of currency
    - Daily Cost Estimation
    - Monthly Cost Estimation
    - Annual Cost Estimation
  - Rail Overview
    - Rail Mode (shows current 12V rail mode)
    - Table with Rail, Power, Current, Voltage shows values for all 12V rails (1-6), 5V rail and 3.3V rail
  - Protection Status
    - This section starts with a short sentence, explaining that if a protection circuit was triggered recently, it will show up here.
    - If one of the following protections was triggered in the last 7 days, the name of the circuit shows up here, the time it was triggered, and
      - Over Temperature Protection (OTP)
      - Over Current Protection (OCP)
      - Over Power Protection (OPP)
      - Fan
      - (UVP, OVP, SCP – will be checked by Doris)
  - Fan details
    - Mode (shows current cooling mode)
    - Speed (shows current fan RPM)
  - Reset to default button

# HRM integration

- P1 is shown by default in the HRM, if IO Center can detect it
- The tile in the HRM shows
  - Name and wattage of the PSU, e.g. “Dark Power Pro 14 IO 1600W”
  - Efficiency
  - Input power
  - Total output power
  - Fan mode switcher (SEMI PASSIVE or PERFORMANCE)
  - Silent Wings 4 135mm [RPM]
  - Rail mode (Single Rail / Multi Rail)
  - All of the above are shown in the HRM by default, but can be deactivated. The following settings are optional and need to be enabled by the user:
    - Power on 12V rail (12V1…12V6, if multi rail)
    - Power on 5V rail
    - Power on 3.3V rail
    - Current on 12V rail (12V1…12V6, if multi rail)
    - Current on 5V rail
    - Current on 3.3V rail
    - Voltage on 12V rail (12V1…12V6, if multi rail)
    - Voltage on 5V rail
    - Voltage on 3.3V rail
