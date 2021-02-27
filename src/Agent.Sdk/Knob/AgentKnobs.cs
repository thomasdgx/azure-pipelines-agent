// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Agent.Sdk.Knob
{
    public class AgentKnobs
    {
        // Containers
        public static readonly Knob PreferPowershellHandlerOnContainers = new Knob(
            nameof(PreferPowershellHandlerOnContainers),
            "If true, prefer using the PowerShell handler on Windows containers for tasks that provide both a Node and PowerShell handler version.",
            new RuntimeKnobSource("agent.preferPowerShellOnContainers"),
            new EnvironmentKnobSource("AGENT_PREFER_POWERSHELL_ON_CONTAINERS"),
            new BuiltInDefaultKnobSource("true"));

        public static readonly Knob SetupDockerGroup = new Knob(
            nameof(SetupDockerGroup),
            "If true, allows the user to run docker commands without sudo",
            new RuntimeKnobSource("VSTS_SETUP_DOCKERGROUP"),
            new EnvironmentKnobSource("VSTS_SETUP_DOCKERGROUP"),
            new BuiltInDefaultKnobSource("true"));
        
        public static readonly Knob AllowMountTasksReadonlyOnWindows = new Knob(
            nameof(AllowMountTasksReadonlyOnWindows),
            "If true, allows the user to mount 'tasks' volume read-only on Windows OS",
            new RuntimeKnobSource("VSTS_SETUP_ALLOW_MOUNT_TASKS_READONLY"),
            new EnvironmentKnobSource("VSTS_SETUP_ALLOW_MOUNT_TASKS_READONLY"),
            new BuiltInDefaultKnobSource("true"));

        public static readonly Knob SkipPostExeceutionIfTargetContainerStopped = new Knob(
            nameof(SkipPostExeceutionIfTargetContainerStopped),
            "If true, skips post-execution step for tasks in case the target container has been stopped",
            new RuntimeKnobSource("AGENT_SKIP_POST_EXECUTION_IF_CONTAINER_STOPPED"),
            new EnvironmentKnobSource("AGENT_SKIP_POST_EXECUTION_IF_CONTAINER_STOPPED"),
            new BuiltInDefaultKnobSource("false"));

        // Directory structure
        public static readonly Knob AgentToolsDirectory = new Knob(
            nameof(AgentToolsDirectory),
            "The location to look for/create the agents tool cache",
            new EnvironmentKnobSource("AGENT_TOOLSDIRECTORY"),
            new EnvironmentKnobSource("agent.ToolsDirectory"),
            new BuiltInDefaultKnobSource(string.Empty));

        public static readonly Knob OverwriteTemp = new Knob(
            nameof(OverwriteTemp),
            "If true, the system temp variable will be overriden to point to the agent's temp directory.",
            new RuntimeKnobSource("VSTS_OVERWRITE_TEMP"),
            new EnvironmentKnobSource("VSTS_OVERWRITE_TEMP"),
            new BuiltInDefaultKnobSource("false"));

        // Tool configuration
        public static readonly Knob DisableFetchByCommit = new Knob(
            nameof(DisableFetchByCommit),
            "If true and server supports it, fetch the target branch by commit. Otherwise, fetch all branches and pull request ref to get the target branch.",
            new RuntimeKnobSource("VSTS.DisableFetchByCommit"),
            new EnvironmentKnobSource("VSTS_DISABLEFETCHBYCOMMIT"),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob PreferGitFromPath = new Knob(
            nameof(PreferGitFromPath),
            "Determines which Git we will use on Windows. By default, we prefer the built-in portable git in the agent's externals folder, setting this to true makes the agent find git.exe from %PATH% if possible.",
            new RuntimeKnobSource("system.prefergitfrompath"),
            new EnvironmentKnobSource("system.prefergitfrompath"),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob DisableGitPrompt = new Knob(
            nameof(DisableGitPrompt),
            "If true, git will not prompt on the terminal (e.g., when asking for HTTP authentication).",
            new RuntimeKnobSource("VSTS_DISABLE_GIT_PROMPT"),
            new EnvironmentKnobSource("VSTS_DISABLE_GIT_PROMPT"),
            new BuiltInDefaultKnobSource("true"));

        public const string QuietCheckoutRuntimeVarName = "agent.source.checkout.quiet";
        public const string QuietCheckoutEnvVarName = "AGENT_SOURCE_CHECKOUT_QUIET";

        public static readonly Knob QuietCheckout = new Knob(
            nameof(QuietCheckout),
            "Aggressively reduce what gets logged to the console when checking out source.",
            new RuntimeKnobSource(QuietCheckoutRuntimeVarName),
            new EnvironmentKnobSource(QuietCheckoutEnvVarName),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob UseNode10 = new Knob(
            nameof(UseNode10),
            "Forces the agent to use Node 10 handler for all Node-based tasks",
            new RuntimeKnobSource("AGENT_USE_NODE10"),
            new EnvironmentKnobSource("AGENT_USE_NODE10"),
            new BuiltInDefaultKnobSource("false"));

        // Agent logging
        public static readonly Knob AgentPerflog = new Knob(
            nameof(AgentPerflog),
            "If set, writes a perf counter trace for the agent. Writes to the location set in this variable.",
            new EnvironmentKnobSource("VSTS_AGENT_PERFLOG"),
            new BuiltInDefaultKnobSource(string.Empty));

        public static readonly Knob TraceVerbose = new Knob(
            nameof(TraceVerbose),
            "If set to anything, trace level will be verbose",
            new EnvironmentKnobSource("VSTSAGENT_TRACE"),
            new BuiltInDefaultKnobSource(string.Empty));

        // Timeouts
        public static readonly Knob AgentChannelTimeout = new Knob(
            nameof(AgentChannelTimeout),
            "Timeout for channel communication between agent listener and worker processes.",
            new EnvironmentKnobSource("VSTS_AGENT_CHANNEL_TIMEOUT"),
            new BuiltInDefaultKnobSource("30"));

        public static readonly Knob AgentDownloadTimeout = new Knob(
            nameof(AgentDownloadTimeout),
            "Amount of time in seconds to wait for the agent to download a new version when updating",
            new EnvironmentKnobSource("AZP_AGENT_DOWNLOAD_TIMEOUT"),
            new BuiltInDefaultKnobSource("1500")); // 25*60

        public static readonly Knob TaskDownloadTimeout = new Knob(
            nameof(TaskDownloadTimeout),
            "Amount of time in seconds to wait for the agent to download a task when starting a job",
            new EnvironmentKnobSource("VSTS_TASK_DOWNLOAD_TIMEOUT"),
            new BuiltInDefaultKnobSource("1200")); // 20*60

        // HTTP
        public const string LegacyHttpVariableName = "AZP_AGENT_USE_LEGACY_HTTP";
        public static readonly Knob UseLegacyHttpHandler = new DeprecatedKnob(
            nameof(UseLegacyHttpHandler),
            "Use the libcurl-based HTTP handler rather than .NET's native HTTP handler, as we did on .NET Core 2.1",
            new EnvironmentKnobSource(LegacyHttpVariableName),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob HttpRetryCount = new Knob(
            nameof(HttpRetryCount),
            "Number of times to retry Http requests",
            new EnvironmentKnobSource("VSTS_HTTP_RETRY"),
            new BuiltInDefaultKnobSource("3"));

        public static readonly Knob HttpTimeout = new Knob(
            nameof(HttpTimeout),
            "Timeout for Http requests",
            new EnvironmentKnobSource("VSTS_HTTP_TIMEOUT"),
            new BuiltInDefaultKnobSource("100"));

        public static readonly Knob HttpTrace = new Knob(
            nameof(HttpTrace),
            "Enable http trace if true",
            new EnvironmentKnobSource("VSTS_AGENT_HTTPTRACE"),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob NoProxy = new Knob(
            nameof(NoProxy),
            "Proxy bypass list if one exists. Should be comma seperated",
            new EnvironmentKnobSource("no_proxy"),
            new BuiltInDefaultKnobSource(string.Empty));

        public static readonly Knob ProxyAddress = new Knob(
            nameof(ProxyAddress),
            "Proxy server address if one exists",
            new EnvironmentKnobSource("VSTS_HTTP_PROXY"),
            new EnvironmentKnobSource("http_proxy"),
            new BuiltInDefaultKnobSource(string.Empty));

        public static readonly Knob ProxyPassword = new Knob(
            nameof(ProxyPassword),
            "Proxy password if one exists",
            new EnvironmentKnobSource("VSTS_HTTP_PROXY_PASSWORD"),
            new BuiltInDefaultKnobSource(string.Empty));

        public static readonly Knob ProxyUsername = new Knob(
            nameof(ProxyUsername),
            "Proxy username if one exists",
            new EnvironmentKnobSource("VSTS_HTTP_PROXY_USERNAME"),
            new BuiltInDefaultKnobSource(string.Empty));

        // Secrets masking
        public static readonly Knob AllowUnsafeMultilineSecret = new Knob(
            nameof(AllowUnsafeMultilineSecret),
            "WARNING: enabling this may allow secrets to leak. Allows multi-line secrets to be set. Unsafe because it is possible for log lines to get dropped in agent failure cases, causing the secret to not get correctly masked. We recommend leaving this option off.",
            new RuntimeKnobSource("SYSTEM_UNSAFEALLOWMULTILINESECRET"),
            new EnvironmentKnobSource("SYSTEM_UNSAFEALLOWMULTILINESECRET"),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob MaskUsingCredScanRegexes = new Knob(
            nameof(MaskUsingCredScanRegexes),
            "Use the CredScan regexes for masking secrets. CredScan is an internal tool developed at Microsoft to keep passwords and authentication keys from being checked in. This defaults to disabled, as there are performance problems with some task outputs.",
            new EnvironmentKnobSource("AZP_USE_CREDSCAN_REGEXES"),
            new BuiltInDefaultKnobSource("false"));

        // Misc
        public static readonly Knob DisableAgentDowngrade = new Knob(
            nameof(DisableAgentDowngrade),
            "Disable agent downgrades. Upgrades will still be allowed.",
            new EnvironmentKnobSource("AZP_AGENT_DOWNGRADE_DISABLED"),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob PermissionsCheckFailsafe = new Knob(
            nameof(PermissionsCheckFailsafe),
            "Maximum depth of file permitted in directory hierarchy when checking permissions. Check to avoid accidentally entering infinite loops.",
            new EnvironmentKnobSource("AGENT_TEST_VALIDATE_EXECUTE_PERMISSIONS_FAILSAFE"),
            new BuiltInDefaultKnobSource("100"));

        public static readonly Knob DisableInputTrimming = new Knob(
            nameof(DisableInputTrimming),
            "By default, the agent trims whitespace and new line characters from all task inputs. Setting this to true disables this behavior.",
            new EnvironmentKnobSource("DISABLE_INPUT_TRIMMING"),
            new BuiltInDefaultKnobSource("false"));

        public static readonly Knob DecodePercents = new Knob(
            nameof(DecodePercents),
            "By default, the agent does not decodes %AZP25 as % which may be needed to allow users to work around reserved values. Setting this to true enables this behavior.",
            new RuntimeKnobSource("DECODE_PERCENTS"),
            new EnvironmentKnobSource("DECODE_PERCENTS"),
            new BuiltInDefaultKnobSource(""));
    }

}
