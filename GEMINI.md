# Biking Fitness app for connecting to Kickr to control resistance

## Important! Before finishing work ensure build works "dotnet build", add new unit tests for any logic, and make sure unit tests pass ("dotnet test")

### Changes should always be added to git and committed after every valid build / test phase.

App should have robust logging to ensure we can figure out why it crashes.

### TODOs
 - It turns out the resistance levels are very aggressive and biking at over 10% is tough. Might be worth figuring out how to use Simulation (Sim) mode you can modify the nominal grade in .5% increments and the app will provide  resistance accounting for your weight as entered into your profile to simulate riding that grade outdoors. THis would allow for more incremental resistance instead of 1-100. 
 - Would be nice to show cadence or distance also.