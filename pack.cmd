@echo on
setlocal

set Configuration=Debug
if not %1.==. (
	set Configuration=%1
)

dotnet pack --no-build --no-restore -c %Configuration%

