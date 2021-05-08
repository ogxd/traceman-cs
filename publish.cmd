rm Release
dotnet publish Traceman.Analyzer/Traceman.Analyzer.csproj -o Release -r win-x64 -p:PublishTrimmed=true --self-contained true
dotnet publish Traceman.Collector/Traceman.Collector.csproj -o Release -r win-x64 -p:PublishTrimmed=true --self-contained true