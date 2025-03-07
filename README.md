# Solution built using Net 8.0.
Can be run as a console application locally and windows service on Prod.

# Contains 2 projects:
1. PowerPositionReportService
2. PowerPositionReportService.Tests

# Uses below Nuget packages

* PowerPositionReportService

    <PackageReference Include="Microsoft.Extensions.Hosting" Version="9.0.2" />
    <PackageReference Include="Microsoft.Extensions.Hosting.WindowsServices" Version="9.0.2" />
    <PackageReference Include="Serilog" Version="4.2.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="9.0.0" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="9.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageReference Include="Serilog.Sinks.File" Version="6.0.0" />

* PowerPositionReportService.Tests

	<PackageReference Include="Microsoft.Extensions.Configuration" Version="9.0.2" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageReference Include="Microsoft.Testing.Extensions.CodeCoverage" Version="17.12.6" />
    <PackageReference Include="Microsoft.Testing.Extensions.TrxReport" Version="1.4.3" />
    <PackageReference Include="Moq" Version="4.20.72" />
    <PackageReference Include="MSTest" Version="3.6.4" />
	
# Has below configurabale settings 

1. OutputDirectory - CSV file output location

2. IntervalMinutes - Interval between each extract run 

3. RetryCount - Number of attempts that the extract should make to retrieve trades in case of a failure

4. RetryDelaySeconds - Interval between each Retry attempt

5. Serilog - path - Path of a log file