## Build

`dotnet build -c Release`

## Where are the built artifacts?

Nuget packages are in `build/nuget`. Npm packages are in `build/npm`.
Binaries for each project are in `build/bin` for the main projects, `build/Samples/bin` for the samples, and `build/bin/tests` for the tests.

## How to bump the major or minor parts of the version

The `version.json` file in the root of the repo controls the version for all packages.
Set the major and/or minor number in it and **commit the change** so that the next build uses the new version.
The patch part of the version is the height of the commit tree since the last manual change of the `version.json`
file, so once you commit a change to the major or minor parts, the patch will reset back to 0.