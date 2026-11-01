
# DCS BIOS MATRIC Middleware
<img src="https://github.com/tgudelj/DCS-BIOS-MATRIC-Middleware/assets/1483482/d1162be3-0e6f-4650-a567-0def8886123d" />


DCS BIOS MATRIC Middleware is a program which provides integration between [DCS World flight simulator](**https://www.digitalcombatsimulator.com/) and [MATRIC](https://matricapp.com) utilizing DCS-BIOS to read data from DCS in real time and push it to MATRIC as variables.
This enables MATRIC users to bind control states to actual flightsim state (e.g. indicators, displays etc.). 
DCS BIOS Middleware is based on excellent work done by [charliefoxtwo](https://github.com/charliefoxtwo) DCS-BIOS-Communicator library https://github.com/charliefoxtwo/DCS-BIOS-Communicator.



![image](https://github.com/tgudelj/DCS-BIOS-MATRIC-Middleware/assets/1483482/ef91ba43-6092-412e-9998-aac2a86e349d)

<img src="https://github.com/tgudelj/DCS-BIOS-MATRIC-Middleware/assets/1483482/34fd3a8c-b5c5-4b87-9751-2420596d3665" width="480" />


## Features

- Parses data from DCS-BIOS and exports it to MATRIC variables
- Reads a "well known" variable called DCS_INPUT_COMMAND from MATRIC and sends it to DCS-BIOS input interface
- Easy setup, just unzip to folder

## Development and diagnostics

Build and run the regression suite on Windows x64 with the .NET 8 SDK or later (the bundled MATRIC assembly requires x64):

```powershell
dotnet build DCSMatricIntegrator.sln -c Release
dotnet test DCSMatricIntegrator.sln -c Release
```

The main window shows DCS-BIOS stream health, MATRIC API availability, and the detected aircraft.
MATRIC availability is checked with `GETCONNECTEDCLIENTS` once every **10 seconds**, with a **5-second reply deadline** after each request.
An empty client list still confirms that the integration API is responding. UDP sends alone do not establish availability or acknowledge that variables were applied.
After MATRIC becomes available, the middleware resends cached values in batches of at most 100 variables.
A fallback refresh once per minute recovers from brief server restarts between probes. Normal updates contain only changed values; no empty updates are sent.

Use **Help → Open logs folder** for configuration warnings and connection errors.
Logs are stored in `%LOCALAPPDATA%\DCS-BIOS-MATRIC\logs`, with a 5 MiB file limit and at most 14 files.
Individual packets and variable updates are not logged at the default level.

Variable filters and the update-frequency slider apply immediately while running. Network settings require stopping and starting the middleware.
For an aircraft with no saved variable selection, all its variables are forwarded. An explicit empty selection exports none, except separately selected common/metadata variables.
Searches operate on the complete lists, and adding or removing filtered entries preserves hidden selections. Cancel discards edits.

The tests exercise synthetic DCS-BIOS streams, real loopback UDP MATRIC messages, reconnects, timeouts, partial startup failures, repeated start/stop, and the WinForms configuration dialogs.
Validate a release against a live DCS/MATRIC session before distributing it.

## Acknowledgements

- [DCS-BIOS-Communicator library by charliefoxtwo](https://github.com/charliefoxtwo/DCS-BIOS-Communicator)
