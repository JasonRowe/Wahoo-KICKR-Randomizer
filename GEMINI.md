# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure unit tests pass ("dotnet test")

### Changes should always be added to git and committed after every valid build / test phase.

App should have robust logging to ensure we can figure out why it crashes.

### TODOs
 - Implement Reconnection Logic: Add robust reconnection logic to app to handle unexpected disconnects (GattCommunicationStatus.Unreachable).
 - [COMPLETED] Figure out correct resistance values. Updated logic to use 0-1 range, with UI limited to 0-10% to prevent excessive resistance.
 - intermittently I get the following when connecting Connection error: System.Runtime.InteropServices.COMException (0x80650081)
 - Also COM Error 0x80650081 happens when trying to set some resistance values


## Most Recent Log:
[2026-01-24 18:24:26.202] Connection error: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.SendCommand(Byte opCode, Nullable`1 parameter) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 202
   at BikeFitnessApp.MainWindow.BtnConnect_Click(Object sender, RoutedEventArgs e) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 118
[2026-01-24 18:24:56.074] Connection error: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.SendCommand(Byte opCode, Nullable`1 parameter) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 202
   at BikeFitnessApp.MainWindow.BtnConnect_Click(Object sender, RoutedEventArgs e) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 118
[2026-01-24 18:25:50.303] Connection error: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.SendCommand(Byte opCode, Nullable`1 parameter) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 202
   at BikeFitnessApp.MainWindow.BtnConnect_Click(Object sender, RoutedEventArgs e) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 118
[2026-01-24 18:37:52.188] Connection error: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.SendCommand(Byte opCode, Nullable`1 parameter) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 202
   at BikeFitnessApp.MainWindow.BtnConnect_Click(Object sender, RoutedEventArgs e) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 118
[2026-01-24 18:39:13.744] Start button clicked.
[2026-01-24 18:39:13.831] Calculated resistance: 0.011323837642011158
[2026-01-24 18:39:13.832] Set to Standard Power Mode.
[2026-01-24 18:39:14.429] Successfully sent resistance command.
[2026-01-24 18:39:43.761] Calculated resistance: 0.0016750124519151477
[2026-01-24 18:39:45.037] Successfully sent resistance command.
[2026-01-24 18:40:13.758] Calculated resistance: 0.024374768140073888
[2026-01-24 18:40:14.451] Successfully sent resistance command.
[2026-01-24 18:40:43.765] Calculated resistance: 0.193759534928387
[2026-01-24 18:40:44.707] Exception in WorkoutTimer_Tick: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.SendCommand(Byte opCode, Nullable`1 parameter) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 202
   at BikeFitnessApp.MainWindow.WorkoutTimer_Tick(Object sender, EventArgs e) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 173
[2026-01-24 18:41:13.756] Calculated resistance: 0.020801590197184375
[2026-01-24 18:41:14.870] Exception in WorkoutTimer_Tick: System.Runtime.InteropServices.COMException (0x80650081)
   at BikeFitnessApp.MainWindow.SendCommand(Byte opCode, Nullable`1 parameter) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 202
   at BikeFitnessApp.MainWindow.WorkoutTimer_Tick(Object sender, EventArgs e) in C:\Users\Jason\Documents\BikeFitnessApp\MainWindow.xaml.cs:line 173
[2026-01-24 18:41:43.755] Calculated resistance: 0.06194936623469963
[2026-01-24 18:41:44.286] Successfully sent resistance command.
[2026-01-24 18:42:13.757] Calculated resistance: 0.11947251924350877
[2026-01-24 18:42:14.298] Successfully sent resistance command.
[2026-01-24 18:42:38.268] Stop button clicked.