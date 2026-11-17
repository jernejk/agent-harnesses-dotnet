#:package OpenTelemetry@1.20.1
#:package OpenTelemetry.Exporter.OpenTelemetryProtocol@1.20.1
#:package OpenTelemetry.Exporter.Console@1.20.1
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

public static class Telemetry
{
    public static TracerProvider Start(string service, string source, string[] args)
    {
        var builder = Sdk.CreateTracerProviderBuilder()
            .ConfigureResource(resource => resource.AddService(service))
            .AddSource(source);

        var otlp = Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");
        if (!string.IsNullOrEmpty(otlp))
        {
            builder.AddOtlpExporter();
            Ui.Line(Tone.Muted, $"telemetry → {otlp}, dashboard: http://localhost:18888");
        }
        else if (args.Contains("--console-traces"))
        {
            builder.AddConsoleExporter();
        }
        return builder.Build();
    }
}
