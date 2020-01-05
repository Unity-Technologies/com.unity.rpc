# About the Unity RPC package

## Build

## How to build

Check [How to Build](https://raw.githubusercontent.com/Unity-Technologies/Git-for-Unity/master/BUILD.md) for all the build, packaging and versioning details.

### Release build 

`build[.sh|cmd] -r`

### Release build and package

`pack[.sh|cmd] -r -b`

### Release build and test

`test[.sh|cmd] -r -b`


### Where are the build artifacts?

Packages sources are in `build/packages`.

Nuget packages are in `build/nuget`.

Packman (npm) packages are in `upm-ci~/packages`.

Binaries for each project are in `build/bin` for the main projects and `build/tests` for the tests.

### How to bump the major or minor parts of the version

The `version.json` file in the root of the repo controls the version for all packages.
Set the major and/or minor number in it and **commit the change** so that the next build uses the new version.
The patch part of the version is the height of the commit tree since the last manual change of the `version.json`
file, so once you commit a change to the major or minor parts, the patch will reset back to 0.