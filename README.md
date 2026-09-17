# PLEXOS Solution Query (.NET)

This application reads a completed PLEXOS solution ZIP with the documented, faster `Solution.QueryToList()` API and writes the returned rows to CSV.

## Required files

Use the **PLEXOS API directory** described in section 2.1 of the PDF. It must contain these five files:

- `PLEXOS_NET.Core.dll`
- `EEUTILITY.dll`
- `EnergyExemplar.PLEXOS.Utility.dll`
- `master.xml`
- `transform.xsl`

You also need the .NET SDK and the **.NET Framework 4.8 Developer Pack**. PLEXOS Desktop API assemblies target .NET Framework, which is why this project is `net48`.

## Run from PowerShell

Open PowerShell in this folder and replace the API and solution paths with yours.

```powershell
$env:PLEXOS_API_DIR = 'C:\Program Files\Energy Exemplar\PLEXOS 10.0 API'
dotnet build
dotnet run --no-build -- --solution 'C:\Temp\3Node\Model Base Solution\Model Base Solution.zip' --collection SystemGenerators --output 'C:\Temp\generator-results.csv'
```

The first command tells the build where your PLEXOS API folder is. The build copies all three DLLs plus `master.xml` and `transform.xsl` into `bin\Debug\net48`, beside the executable. That placement is required by the PLEXOS documentation. After the first build, repeat only the `dotnet run --no-build ...` command for new solution files.

## Examples

```powershell
# One generator under System
dotnet run --no-build -- --solution 'C:\Temp\3Node\Model Base Solution\Model Base Solution.zip' --collection SystemGenerators --parent System --child Gen1

# Region data, arranged differently
dotnet run --no-build -- --solution 'C:\Temp\3Node\Model Base Solution\Model Base Solution.zip' --collection SystemRegions --period TradingPeriod --series Values
```

Run `dotnet run --no-build -- --help` for the full option list. The defaults are the PDF's example: `STSchedule`, `Interval`, and `Properties`. Empty parent/child filters query all matching objects. If the collection key is invalid, the tool lists the valid keys inside your particular solution.

## Why no ADODB reference?

The PDF says `Solution.Query()` returns an ADODB Recordset, which needs `adodb`. This project uses `QueryToList()` instead; the PDF specifically recommends it as faster for .NET and it does not require ADODB.

This is a query-only tool: it reads an existing solution ZIP and does not start a PLEXOS run.
