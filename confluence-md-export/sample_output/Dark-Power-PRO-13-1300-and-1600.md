<!--
source: https://bequiet.atlassian.net/wiki/spaces/ldiod/pages/435191838/Dark+Power+PRO+13+1300+and+1600
space: LDP3 (Listan / Developex - IO Center Desktop)
page-id: 435191838
version: 54 (2026-07-14)
exported: 2026-07-20 from Confluence storage format
note: images are referenced by their Confluence attachment filenames; binaries were not downloaded in this pass.
-->

# Dark Power PRO 13 (1300 and 1600)

> **Version history** — Confluence "Change History" macro (not exported).

## Global overview

![image-20250723-141353.png](assets/image-20250723-141353.png)
![image-20250723-142202.png](assets/image-20250723-142202.png)
![image-20251114-120817.png](assets/image-20251114-120817.png)

The be quiet! Dark Power Pro 13 1600W offers 80 PLUS® Titanium efficiency, world-class performance, and ATX 3.1 compliance for next-gen GPU compatibility.

## General specifications

- P1 has a unique ID and is identified by the IO Center.
- P1 is displayed in a tile on the main screen of the IO Center.
- P1 has a dedicated product page in the left side menu, grouped under the new "Power supply" tab.
- P1 is integrated in the HM.
- The list of PIDs supported models of the P1 product:

  ![image-20250918-131406.png](assets/image-20250918-131406.png)
- The 'be quiet!' VID is `x373f`

## Home screen

The device should be displayed on the Home screen like others.

On the P1 tile, the System should display a navigation link to the Configurations. ![image-20251114-120752.png](assets/image-20251114-120752.png)

In the left-side navigation menu, a new type of device should be represented by the icon. ![image-20250910-175613.png](assets/image-20250910-175613.png) On hover over, the tooltip with a text "Power Supply Unit" should appear.

## Configurations screen

- The User can change Rail Mode from MULTI to SINGLE. This affects the representation of the data on the right-side panel:

  ![image-20260120-155804.png](assets/image-20260120-155804.png)

  - MULTI means that the current will be split into 6 12V lines, and each must be displayed in the right-side panel with the values of the current. ![image-20250910-182733.png](assets/image-20250910-182733.png)
  - SINGLE means only one 12V line, and on the right-side panel, only one value for the current must be displayed. ![image-20260217-125643.png](assets/image-20260217-125643.png)
  - Current and Voltage in the rail overview should always show 2 decimals (including zeros), so it does not look that jumpy.
  - Power should be rounded to integer - no decimals
- The User can switch between SEMI-PASSIVE and PERFORMANCE mode, which will affect the curve displayed under it.
- The User can specify a cost per KWH in the input field. Allowed values are from 0.01 to 100000.00. Only digits and '.' are allowed. On focus lost, validate the value and round up to the closest valid value. (It is in local currency, and we do not want to know the real price.)
- "Update calculations" button must store a new 'COST PER KWH" and after that calculation rules should apply updated price.

### Right-side panel

#### "Device Details" block

- Device rendered image
- Model Name (from device)
- Article Number (as a value based on device SN. Article number = 'BP' + SN[0-4]; constant text 'BP' + first 5 characters from serial number of the device)
- Serial Number (from device)

#### "Power Consumption" block

- Efficiency, % (`87.5 %`)
  - Based on the [List of sensors](https://q-link.devx.kiev.ua/products/psu_p1.html).
  - Formula: `((sum(3.3V DC output current (cA) * 3.3V DC output voltage (cV) + 5V DC output current (cA) * 5V DC output voltage (cV) + 12V{N} DC output current (cA) * 12V{N} DC output voltage (cV)))*10'000 / (AC input power (dW))*10) * 100%`
    - `{N}` depends on rail mode and can be a single 12V values pair or a set of values for 6 sensors
  - The result should be in % from 0 to 100 as `NN.N %` (live, not stored).
- Uptime, the time from the last software launch (`1y 3m 5d 1h 5m`)
  - FW provides `PSU_uptime_s` (seconds since power-on).
  - SW converts seconds to a duration.
  - SW shows it in the UI as `years / months / days / hours / min`.
- Total Uptime, the time a device is on, is taken from the device data (`1y 3m 5d 1h 5m`)
  - FW provides `PSU_total_runtime_s` (device lifetime counter, never reset).
  - SW polls it from the device. *(Update rate `Disabled` - poll-only, not pushed by the device.)*
  - SW converts seconds to a duration.
  - SW shows it in the UI as `years / months / days / hours / min`
- Total power draw, bar with number of W (from the device, rounded to even number)
  - FW provides `AC_input_power_dW` (deciwatts, 0.1 W).
  - SW reads it every 500 ms.
  - SW converts to watts (`AC_input_power_dW * 10`) and rounds to even.
  - SW shows it in the UI as `NNNN W` (progress bar against the rated maximum, e.g. 1600 W; live, not stored).

#### "Fan details" block

- Mode, text
  - Displays current cooling mode of the device
- Speed, RPM
  - Displays current RPM values from the device's fan

#### "Rail overview" block

- Rail Mode, text
- A table with Power (rounded to even), Current (rounded to 2 digits after coma), Voltage (rounded to 2 digits after coma) for each rail

#### "Power cost calculator" block

**Since the software launch**, (num, 2 signs after dot). This is calculated per current session as a (total KWH consumed) * (price).

- FW pushes `AC_input_power_dW` every 500 ms; SW aggregates each sample while a session is active.
- SW calculates per-tick energy: `e_KWh = (AC_input_power_dW * 10 / 1000 / 7200` = power(KW) per tick(h).
- SW accumulates it in memory: `session_energy_kWh += e_kWh` (not persisted; reset when the session ends).
- SW calculates `session_energy_kWh x price` and shows it in the UI.

**Cost Lifetime**, (num, 2 signs after dot). This is calculated since the software was installed and detected P1. It must be displayed only after 1 week of records. Before 1 week, display `n/a`.

- FW pushes `AC_input_power_dW` every 500 ms; SW calculates `e_kWh` (as above) while a session is active.
- SW accumulates it into `lifetime_energy_kWh` and stores it in appdata per SN - a separate persisted counter, kept beyond the 30-day window.
- SW calculates `lifetime_energy_kWh x price` and shows it in the UI.
- Monthly cost estimate, (num, 2 signs after dot). This is calculated since the software was installed and detected P1. It must be displayed only after 1 week of records. Before 1 week, display `n/a`.
  - A Tooltip text: "Estimated costs are based on recorded usage while IO Center is running in the background and the entered energy price and may differ from actual utility bills. `<br>` The monthly cost estimate becomes available after at least 7 calendar days of recorded data." ![image-20260624-102727.png](assets/image-20260624-102727.png)

Session length

- FW provides `PSU_uptime_s` (seconds since power-on).
- SW polls it from the device. *(This sensor's update rate is `Disabled` - the device does not push it, so SW must poll it; Session Length refreshes at the SW's polling cadence.)*
- SW converts seconds to a duration.
- SW shows it in the UI as `years / months / days / hours / min`.
- Additionally, the system must store the total number of minutes of all sessions since the software installation.
- Values should be displayed in the proper format based on the selected locale (e.g., for GE separator is a comma, for EN, it is a dot).

### Rounding rules

These must be applied only to the final UI elements because some of them can be reused for a differen elements.

- Efficiency, % (the value format is `NN.N` %)
- Input power, W (the value format is `NNNN` W)
- Total output power, W (the value format is `NNNN` W)
- Cooling mode, `SEMI-PASSIVE/PERFORMANCE`
- Silent Wings 4 135mm, `NNNN` RPM
- Rail Mode, `MULTI/SINGLE`
- Rail line details, table
  - 12V lines:   Power (`NNN` W), Current (`NN.NN` A), Voltage (`NN.NN` V)
  - 5V lines:    Power (`NNN` W), Current (`NN.NN` A), Voltage (`NN.NN` V)
  - 3.3V lines: Power (`NNN` W), Current (`NN.NN` A), Voltage (`NN.NN` V)

### Tooltips on the Configurations screen

[BeQuiet – Figma](https://www.figma.com/design/L40eio9whwhHaSAuIdscvV/BeQuiet?node-id=16492-5226&t=RxygjrFYz99i2P3L-0)

```
Displays whether the PSU is operating in single-rail or multi-rail mode, affecting how power is distributed across components.
```

```
Shows the status of key PSU protections: Over-Current (OCP), Over-Temperature (OTP), and Over-Power (OPP) to prevent hardware damage. Recently triggered protection circuits appear here.
```

```
Indicates the current power usage of the system, helping monitor energy draw and efficiency.
```

```
Estimated costs are based on recorded usage while IO Center is running in the background and the entered energy price and may differ from actual utility bills. <br> The monthly cost estimate becomes available after at least 7 calendar days of recorded data.
```

## Default values

When the 'Reset' button is pressed, the system must prompt confirmation.

When confirmed, the system must set flags in the device according to the list:

- Trigger reset on device
- NOT reset `Total Runtime`
- Rail Mode == SINGLE
- Fan Mode == SEMI-PASSIVE
- COST PER KWH == 0.10
- POWER CONSUMPTION → Session Length == 0
- POWER COST CALCULATOR → Since Software Launch == 0
- Close and open a new session in the Q-link protocol

## HM integration

- P1 is shown by default in the HM, if the IO Center can detect it. ![image-20250910-181635.png](assets/image-20250910-181635.png)
- By default, all parameters are enabled, except the 'rail line details'.
- When edited, the tile allows for managing such device parameters ![image-20251008-133338.png](assets/image-20251008-133338.png)
  - Efficiency, % (the value format is `NN.N` %)
  - Input power, W (the value format is `NNNN` W)
  - Total output power, W (the value format is `NNNN` W)
  - Cooling mode, `SEMI-PASSIVE/PERFORMANCE`
  - Silent Wings 4 135mm, RPM
  - Rail Mode, `MULTI/SINGLE`
  - Rail line details, table
    - 12V lines:   Power (`NNN` W), Current (`NN.NN` A), Voltage (`NN.NN` V)
    - 5V lines:    Power (`NNN` W), Current (`NN.NN` A), Voltage (`NN.NN` V)
    - 3.3V lines: Power (`NNN` W), Current (`NN.NN` A), Voltage (`NN.NN` V)

## Reset to factory settings

For this device a "Product settings" screen does not exist and a reset to factory settings should be displayed on the "Configuration" screen.

A button should allow the user to resetsome of the PSU settings.

The reset should restore:

- all [Default values](#default-values)
- `Session Length`
- `Since Software Launch`
- `Cost Lifetime` (remove historical data of power draw)
- `Monthly Cost Estimate` (remove historical data of power draw)
- **do not reset** the `Total Runtime`

Before performing the reset, the software must display a **confirmation pop-up**.

Header: *"Reset device %device_name% to factory settings"*

Text: "You are about to reset %device_name% device to factory settings. \n Device will reconnect after reset operation."

Buttons: [Reset] [Cancel]

After a successful reset, a toast **notification** should confirm the action.

*"All settings have been restored to default."*

Reset settings to default tooltip: *"Reset all settings to their factory defaults. This includes Session data, Costs, Cooling and Power options"*

## Error states

- The FW update for the P1 device is not supported, and there should be no ability to check the FW version or update it.
- In the case when the device is stuck in the 'bootloader' state, the system must:
  - On the device detection or software launch, display a pop-up ![image-20260115-140623.png](assets/image-20260115-140623.png)
  - On the 'Home screen', the device should display an error ![image-20260115-141117.png](assets/image-20260115-141117.png)
