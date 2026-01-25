# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure unit tests pass ("dotnet test")

### Changes should always be added to git and committed after every valid build / test phase.

App should have robust logging to ensure we can figure out why it crashes.

### TODOs
 - [COMPLETED] Make scan happen auto on startup of app
 - [COMPLETED] Move scan and connect to initial startup page that on continue brings you to the app page with resistance controls and display
 - [COMPLETED] Integrate Material Design to get a better default look. Added MaterialDesignThemes, modern cards, vibrant colors, and a visual resistance gauge.
 - [COMPLETED] Implement Reconnection Logic: Added retry mechanism for COMException 0x80650081.
 - [IN PROGRESS] Debug Connection Stability. Added ConnectionStatusChanged monitoring, Uncached service discovery, and better watcher cleanup to diagnose 0x80650081 errors.
 - [COMPLETED] Protocol Mismatch: Confirmed Wahoo Custom Characteristic requires Wahoo OpCodes. OpCode 0x42 (Resistance) with values 0-99 (integer) works successfully.
 - [COMPLETED] Re-enable Workout Timer: Restored sliders, removed debug UI, and connected timer to Wahoo resistance logic.
 - [COMPLETED] UI Improvements: Enlarged window height, increased resistance text size, and added dynamic color coding (Green -> Red).
 - [COMPLETED] UI Best Practices: Applied responsive grid layout, ViewBox for scaling, multiples of 4 for spacing, and centered startup location.
 - intermittently I get the following when connecting Connection error: System.Runtime.InteropServices.COMException (0x80650081)
 - Also COM Error 0x80650081 happens when trying to set some resistance values


## Most Recent Log:
[2026-01-24 23:50:23.562] Calculated resistance: 0.22138182331962517
[2026-01-24 23:50:23.850] Write exception: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.WriteCharacteristicWithRetry(IBuffer buffer) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 310. Retry 1/5
[2026-01-24 23:50:24.658] Successfully sent resistance command.
[2026-01-24 23:50:53.565] Calculated resistance: 0.24108357505870473
[2026-01-24 23:50:54.057] Successfully sent resistance command.
[2026-01-24 23:51:23.571] Calculated resistance: 0.30407828418130456
[2026-01-24 23:51:23.872] Successfully sent resistance command.
[2026-01-24 23:51:53.602] Calculated resistance: 0.1428494302068813
[2026-01-24 23:51:53.884] Successfully sent resistance command.
[2026-01-24 23:52:23.611] Calculated resistance: 0.5259094338910529
[2026-01-24 23:52:24.492] Write exception: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.WriteCharacteristicWithRetry(IBuffer buffer) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 310. Retry 1/5
[2026-01-24 23:52:25.088] Successfully sent resistance command.
[2026-01-24 23:52:53.622] Calculated resistance: 0.6267592070432091
[2026-01-24 23:52:54.102] Write exception: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.WriteCharacteristicWithRetry(IBuffer buffer) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 310. Retry 1/5
[2026-01-24 23:52:54.899] Successfully sent resistance command.
[2026-01-24 23:53:23.630] Calculated resistance: 0.5000270356750461
[2026-01-24 23:53:24.315] Successfully sent resistance command.
[2026-01-24 23:59:18.605] Device Connection Status Changed: Connected
[2026-01-24 23:59:19.680] Sending command (Byte): OpCode=00, Param=, Bytes=[00]
[2026-01-24 23:59:21.200] Start button clicked.
[2026-01-24 23:59:21.202] Calculated resistance: 0.23179797769208396
[2026-01-24 23:59:21.573] Successfully sent resistance command.
[2026-01-24 23:59:55.444] Device Connection Status Changed: Connected
[2026-01-24 23:59:56.160] Sending command (Byte): OpCode=00, Param=, Bytes=[00]
[2026-01-24 23:59:57.765] Start button clicked.
[2026-01-24 23:59:57.767] Calculated resistance: 0.2195048407693167
[2026-01-24 23:59:58.234] Successfully sent resistance command.
