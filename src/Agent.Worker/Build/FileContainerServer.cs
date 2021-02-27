// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.VisualStudio.Services.Agent.Util;
using Microsoft.VisualStudio.Services.FileContainer.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.VisualStudio.Services.WebApi;
using System.Net.Http;
using System.Net;
using Agent.Sdk.Blob;
using BuildXL.Cache.ContentStore.Hashing;
using Microsoft.VisualStudio.Services.BlobStore.WebApi;
using Microsoft.VisualStudio.Services.Content.Common;
using Microsoft.VisualStudio.Services.BlobStore.Common;
using Microsoft.VisualStudio.Services.BlobStore.Common.Telemetry;

namespace Microsoft.VisualStudio.Services.Agent.Worker.Build
{
    public class FileContainerServer
    {
        private readonly ConcurrentQueue<string> _fileUploadQueue = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _fileUploadTraceLog = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly ConcurrentDictionary<string, ConcurrentQueue<string>> _fileUploadProgressLog = new ConcurrentDictionary<string, ConcurrentQueue<string>>();
        private readonly FileContainerHttpClient _fileContainerHttpClient;
        private readonly VssConnection _connection;
        private DedupStoreClientWithDataport _dedupClient;
        private BlobStoreClientTelemetry _blobTelemetry;

        private CancellationTokenSource _uploadCancellationTokenSource;
        private TaskCompletionSource<int> _uploadFinished;
        private Guid _projectId;
        private long _containerId;
        private string _containerPath;
        private int _filesProcessed = 0;
        private string _sourceParentDirectory;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA2000:Dispose objects before losing scope", MessageId = "fileContainerClientConnection")]
        public FileContainerServer(
            VssConnection connection,
            Guid projectId,
            long containerId,
            string containerPath)
        {
            ArgUtil.NotNull(connection, nameof(connection));
            this._connection = connection;

            _projectId = projectId;
            _containerId = containerId;
            _containerPath = containerPath;

            // default file upload request timeout to 600 seconds
            var fileContainerClientConnectionSetting = connection.Settings.Clone();
            if (fileContainerClientConnectionSetting.SendTimeout < TimeSpan.FromSeconds(600))
            {
                fileContainerClientConnectionSetting.SendTimeout = TimeSpan.FromSeconds(600);
            }

            var fileContainerClientConnection = new VssConnection(connection.Uri, connection.Credentials, fileContainerClientConnectionSetting);
            _fileContainerHttpClient = fileContainerClientConnection.GetClient<FileContainerHttpClient>();
        }

        public async Task<long> CopyToContainerAsync(
            IAsyncCommandContext context,
            String source,
            CancellationToken cancellationToken)
        {
            ArgUtil.NotNull(context, nameof(context));
            ArgUtil.NotNull(source, nameof(source));

            //set maxConcurrentUploads up to 2 until figure out how to use WinHttpHandler.MaxConnectionsPerServer modify DefaultConnectionLimit
            int maxConcurrentUploads = Math.Min(Environment.ProcessorCount, 2);
            //context.Output($"Max Concurrent Uploads {maxConcurrentUploads}");

            List<String> files;
            if (File.Exists(source))
            {
                files = new List<String>() { source };
                _sourceParentDirectory = Path.GetDirectoryName(source);
            }
            else
            {
                files = Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories).ToList();
                _sourceParentDirectory = source.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }

            context.Output(StringUtil.Loc("TotalUploadFiles", files.Count()));
            using (_uploadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // hook up reporting event from file container client.
                _fileContainerHttpClient.UploadFileReportTrace += UploadFileTraceReportReceived;
                _fileContainerHttpClient.UploadFileReportProgress += UploadFileProgressReportReceived;

                try
                {
                    // try upload all files for the first time.
                    UploadResult uploadResult = await ParallelUploadAsync(context, files, maxConcurrentUploads, _uploadCancellationTokenSource.Token);

                    if (uploadResult.FailedFiles.Count == 0)
                    {
                        // all files have been upload succeed.
                        context.Output(StringUtil.Loc("FileUploadSucceed"));
                        return uploadResult.TotalFileSizeUploaded;
                    }
                    else
                    {
                        context.Output(StringUtil.Loc("FileUploadFailedRetryLater", uploadResult.FailedFiles.Count));
                    }

                    // Delay 1 min then retry failed files.
                    for (int timer = 60; timer > 0; timer -= 5)
                    {
                        context.Output(StringUtil.Loc("FileUploadRetryInSecond", timer));
                        await Task.Delay(TimeSpan.FromSeconds(5), _uploadCancellationTokenSource.Token);
                    }

                    // Retry upload all failed files.
                    context.Output(StringUtil.Loc("FileUploadRetry", uploadResult.FailedFiles.Count));
                    UploadResult retryUploadResult = await ParallelUploadAsync(context, uploadResult.FailedFiles, maxConcurrentUploads, _uploadCancellationTokenSource.Token);

                    if (retryUploadResult.FailedFiles.Count == 0)
                    {
                        // all files have been upload succeed after retry.
                        context.Output(StringUtil.Loc("FileUploadRetrySucceed"));
                        return uploadResult.TotalFileSizeUploaded + retryUploadResult.TotalFileSizeUploaded;
                    }
                    else
                    {
                        throw new Exception(StringUtil.Loc("FileUploadFailedAfterRetry"));
                    }
                }
                finally
                {
                    _fileContainerHttpClient.UploadFileReportTrace -= UploadFileTraceReportReceived;
                    _fileContainerHttpClient.UploadFileReportProgress -= UploadFileProgressReportReceived;
                }
            }
        }

        private async Task<UploadResult> ParallelUploadAsync(IAsyncCommandContext context, IReadOnlyList<string> files, int concurrentUploads, CancellationToken token)
        {
            // return files that fail to upload and total artifact size
            var uploadResult = new UploadResult();

            // nothing needs to upload
            if (files.Count == 0)
            {
                return uploadResult;
            }

            var uploadToBlob = String.Equals(context.GetVariableValueOrDefault("agent.UploadBuildArtifactsToBlob"), "true", StringComparison.InvariantCultureIgnoreCase);
            if (uploadToBlob)
            {
                var verbose = String.Equals(context.GetVariableValueOrDefault("system.debug"), "true", StringComparison.InvariantCultureIgnoreCase);
                var (dedupClient, clientTelemetry) = await DedupManifestArtifactClientFactory.Instance
                    .CreateDedupClientAsync(verbose, (str) => context.Output(str), this._connection, token);
                
                _dedupClient = dedupClient;
                _blobTelemetry = clientTelemetry;
            }

            // ensure the file upload queue is empty.
            if (!_fileUploadQueue.IsEmpty)
            {
                throw new ArgumentOutOfRangeException(nameof(_fileUploadQueue));
            }

            // enqueue file into upload queue.
            foreach (var file in files)
            {
                _fileUploadQueue.Enqueue(file);
            }

            // Start upload monitor task.
            _filesProcessed = 0;
            _uploadFinished = new TaskCompletionSource<int>();
            _fileUploadTraceLog.Clear();
            _fileUploadProgressLog.Clear();
            Task uploadMonitor = ReportingAsync(context, files.Count(), _uploadCancellationTokenSource.Token);

            // Start parallel upload tasks.
            List<Task<UploadResult>> parallelUploadingTasks = new List<Task<UploadResult>>();
            for (int uploader = 0; uploader < concurrentUploads; uploader++)
            {
                parallelUploadingTasks.Add(UploadAsync(context, uploader, uploadToBlob, _uploadCancellationTokenSource.Token));
            }

            // Wait for parallel upload finish.
            await Task.WhenAll(parallelUploadingTasks);
            foreach (var uploadTask in parallelUploadingTasks)
            {
                // record all failed files.
                uploadResult.AddUploadResult(await uploadTask);
            }

            // Stop monitor task;
            _uploadFinished.TrySetResult(0);
            await uploadMonitor;

            return uploadResult;
        }

        private async Task<UploadResult> UploadAsync(IAsyncCommandContext context, int uploaderId, bool uploadToBlob, CancellationToken token)
        {
            List<string> failedFiles = new List<string>();
            long uploadedSize = 0;
            string fileToUpload;
            Stopwatch uploadTimer = new Stopwatch();
            while (_fileUploadQueue.TryDequeue(out fileToUpload))
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    string itemPath = (_containerPath.TrimEnd('/') + "/" + fileToUpload.Remove(0, _sourceParentDirectory.Length + 1)).Replace('\\', '/');
                    uploadTimer.Restart();
                    bool catchExceptionDuringUpload = false;
                    HttpResponseMessage response = null;
                    long uploadLength = 0;
                    try
                    {
                        if (uploadToBlob)
                        {
                            var result = await UploadToBlobStore(context, fileToUpload, token);
                            var retryHelper = new RetryHelper(context);

                            response = await retryHelper.Retry(async () => await _fileContainerHttpClient.CreateItemForArtifactUpload(_containerId, itemPath, _projectId,
                                result.dedupId.ValueString, (long) result.length, token),
                                                           (retryCounter) => (int) Math.Pow(retryCounter, 2) * 5,
                                                           (exception) => true);
                            uploadLength = (long) result.length;
                        }
                        else
                        {
                            using (FileStream fs = File.Open(fileToUpload, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                response = await _fileContainerHttpClient.UploadFileAsync(_containerId, itemPath, fs, _projectId, cancellationToken: token, chunkSize: 4 * 1024 * 1024);
                                uploadLength = fs.Length;
                            }
                        }
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        context.Output(StringUtil.Loc("FileUploadCancelled", fileToUpload));
                        if (response != null)
                        {
                            response.Dispose();
                            response = null;
                        }

                        throw;
                    }
                    catch (Exception ex)
                    {
                        catchExceptionDuringUpload = true;
                        context.Output(StringUtil.Loc("FileUploadFailed", fileToUpload, ex.Message));
                        context.Output(ex.ToString());
                    }

                    uploadTimer.Stop();
                    if (catchExceptionDuringUpload || (response != null && response.StatusCode != HttpStatusCode.Created))
                    {
                        if (response != null)
                        {
                            context.Output(StringUtil.Loc("FileContainerUploadFailed", response.StatusCode, response.ReasonPhrase, fileToUpload, itemPath));
                        }

                        // output detail upload trace for the file.
                        ConcurrentQueue<string> logQueue;
                        if (_fileUploadTraceLog.TryGetValue(itemPath, out logQueue))
                        {
                            context.Output(StringUtil.Loc("FileUploadDetailTrace", itemPath));
                            string message;
                            while (logQueue.TryDequeue(out message))
                            {
                                context.Output(message);
                            }
                        }

                        // tracking file that failed to upload.
                        failedFiles.Add(fileToUpload);
                    }
                    else
                    {
                        context.Debug(StringUtil.Loc("FileUploadFinish", fileToUpload, uploadTimer.ElapsedMilliseconds));
                        uploadedSize += uploadLength;
                        // debug detail upload trace for the file.
                        ConcurrentQueue<string> logQueue;
                        if (_fileUploadTraceLog.TryGetValue(itemPath, out logQueue))
                        {
                            context.Debug($"Detail upload trace for file: {itemPath}");
                            string message;
                            while (logQueue.TryDequeue(out message))
                            {
                                context.Debug(message);
                            }
                        }
                    }

                    if (response != null)
                    {
                        response.Dispose();
                        response = null;
                    }

                    Interlocked.Increment(ref _filesProcessed);
                }
                catch (Exception ex)
                {
                    context.Output(StringUtil.Loc("FileUploadFileOpenFailed", ex.Message, fileToUpload));
                    throw;
                }
            }

            return new UploadResult(failedFiles, uploadedSize);
        }

        private async Task<(DedupIdentifier dedupId, ulong length)> UploadToBlobStore(IAsyncCommandContext context, string itemPath, CancellationToken cancellationToken)
        {
            // Create chunks and identifier
            var chunk = await ChunkerHelper.CreateFromFileAsync(FileSystem.Instance, itemPath, cancellationToken, false);
            var rootNode = new DedupNode(new []{ chunk});
            var dedupId = rootNode.GetDedupIdentifier(HashType.Dedup64K);

            // Setup upload session to keep file for at mimimum one day
            var verbose = String.Equals(context.GetVariableValueOrDefault("system.debug"), "true", StringComparison.InvariantCultureIgnoreCase);
            var tracer = DedupManifestArtifactClientFactory.CreateArtifactsTracer(verbose, (str) => context.Output(str));
            var keepUntulRef = new KeepUntilBlobReference(DateTime.UtcNow.AddDays(1));
            var uploadSession = _dedupClient.CreateUploadSession(keepUntulRef, tracer, FileSystem.Instance);

            // Upload the chunks
            var uploadRecord = _blobTelemetry.CreateRecord<BuildArtifactActionRecord>((level, uri, type) =>
                new BuildArtifactActionRecord(level, uri, type, nameof(UploadAsync), context));
            await _blobTelemetry.MeasureActionAsync(
                record: uploadRecord,
                actionAsync: async () => await AsyncHttpRetryHelper.InvokeAsync(
                        async () =>
                        {
                            return await uploadSession.UploadAsync(rootNode, new Dictionary<DedupIdentifier, string>(){ [dedupId] = itemPath }, cancellationToken);
                        },
                        maxRetries: 3,
                        tracer: tracer,
                        canRetryDelegate: e => true, // this isn't great, but failing on upload stinks, so just try a couple of times
                        cancellationToken: cancellationToken,
                        continueOnCapturedContext: false)
            );
            return (dedupId, rootNode.TransitiveContentBytes);
        }

        private async Task ReportingAsync(IAsyncCommandContext context, int totalFiles, CancellationToken token)
        {
            int traceInterval = 0;
            while (!_uploadFinished.Task.IsCompleted && !token.IsCancellationRequested)
            {
                bool hasDetailProgress = false;
                foreach (var file in _fileUploadProgressLog)
                {
                    string message;
                    while (file.Value.TryDequeue(out message))
                    {
                        hasDetailProgress = true;
                        context.Output(message);
                    }
                }

                // trace total file progress every 25 seconds when there is no file level detail progress
                if (++traceInterval % 2 == 0 && !hasDetailProgress)
                {
                    context.Output(StringUtil.Loc("FileUploadProgress", totalFiles, _filesProcessed, (_filesProcessed * 100) / totalFiles));
                }

                await Task.WhenAny(_uploadFinished.Task, Task.Delay(5000, token));
            }
        }

        private void UploadFileTraceReportReceived(object sender, ReportTraceEventArgs e)
        {
            ConcurrentQueue<string> logQueue = _fileUploadTraceLog.GetOrAdd(e.File, new ConcurrentQueue<string>());
            logQueue.Enqueue(e.Message);
        }

        private void UploadFileProgressReportReceived(object sender, ReportProgressEventArgs e)
        {
            ConcurrentQueue<string> progressQueue = _fileUploadProgressLog.GetOrAdd(e.File, new ConcurrentQueue<string>());
            progressQueue.Enqueue(StringUtil.Loc("FileUploadProgressDetail", e.File, (e.CurrentChunk * 100) / e.TotalChunks));
        }
    }
}