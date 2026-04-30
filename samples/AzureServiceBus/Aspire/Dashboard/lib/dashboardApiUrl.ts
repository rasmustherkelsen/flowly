// Aspire injects service discovery URLs as environment variables when WithReference is used.
// The format is: services__{resourceName}__{endpointName}__{index}
// The Api resource is named "api" in the AppHost.
export function getDashboardApiUrl(): string {
  return (
    process.env['services__api__http__0'] ??
    process.env['services__api__https__0'] ??
    process.env['Services__api__http__0'] ??
    process.env['Services__api__https__0'] ??
    'http://localhost:5200'
  );
}
