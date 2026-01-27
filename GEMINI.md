# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure unit tests pass ("dotnet test")

### Changes should always be added to git and committed after every valid build / test phase.

App should have robust logging to ensure we can figure out why it crashes.

### TODOs
 - [COMPLETED] App should have a way to disable or enable logging from an admin menu. Admin menu should be subtle at top of workout app. Logging should be off by default.
 - allow for changing interval of intensity changes 30sec by clicking up or down arrow and increasing by 10sec + or -.
 - Allow for users to select option for smooth hilly ride where ride slowing moves from min to max and back to min until ride is stopped.
 - Sometimes resistance changes are obvious but sometimes I'm not sure the change went through. It would be nice if we could find any API to verify the setting from the device after we send the command.
 - [COMPLETED] When the resistance change is requested and failed we should not show that value in the UI.
 - intermittently I get the following when connecting Connection error: System.Runtime.InteropServices.COMException (0x80650081)
 - Also COM Error 0x80650081 happens when trying to set some resistance values