// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Sdk;
using Agent.Sdk.Blob;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Content.Common.Tracing;
using Microsoft.VisualStudio.Services.WebApi;
using Microsoft.VisualStudio.Services.BlobStore.Common.Telemetry;
using Agent.Plugins.PipelineArtifact;

namespace Microsoft.VisualStudio.Services.Agent.Tests
{
    public class MockDedupManifestArtifactClientFactory : IDedupManifestArtifactClientFactory
    {
        private TestTelemetrySender telemetrySender;
        private readonly Uri baseAddress = new Uri("http://testBaseAddress");
        public Task<(DedupManifestArtifactClient client, BlobStoreClientTelemetry telemetry)> CreateDedupManifestClientAsync(bool verbose, Action<string> traceOutput, VssConnection connection, CancellationToken cancellationToken)
        {
            telemetrySender = new TestTelemetrySender();
            return Task.FromResult((client: (DedupManifestArtifactClient)null, telemetry: new BlobStoreClientTelemetry(
                NoopAppTraceSource.Instance,
                baseAddress,
                telemetrySender)));

        }

        public Task<(DedupStoreClientWithDataport client, BlobStoreClientTelemetry telemetry)> CreateDedupClientAsync(bool verbose, Action<string> traceOutput, VssConnection connection, CancellationToken cancellationToken)
        {
            telemetrySender = new TestTelemetrySender();
            return Task.FromResult((client: (DedupStoreClientWithDataport)null, telemetry: new BlobStoreClientTelemetry(
                NoopAppTraceSource.Instance,
                baseAddress,
                telemetrySender)));

        }
    }
}