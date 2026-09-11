# CLAUDE.md

## Testing
Tests use Microsoft.Testing.Platform through TUnit.

You should run tests whenever you make changes.
It is recommended to run all tests, including NativeAOT tests.

### Normal Tests

```bash
# export TFMS=net8.0;net10.0
dotnet test -- --retry-failed-tests 3 --report-trx --no-progress
```

Make sure to specify --retry-failed-tests 3. This will retry failed tests up to 3 times, reducing test failures due to temporary environmental issues.

### NativeAOT Tests

```bash
dotnet publish tests\Configuration.Writable.Tests\Configuration.Writable.Tests.csproj -f net10.0 -r win-x64 -o ./publish
./publish/Configuration.Writable.Tests.exe --retry-failed-tests 3 --report-trx --no-progress
```

The TFM and runtime need to be adjusted according to the environment in which the tests are being run. 
Each test project needs to be executed separately.
