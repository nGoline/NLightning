using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace NLightning.Daemon.Plugins;

using Contracts;

public interface IDaemonContext
{
    IServiceProvider Services { get; }
    IControlClient Client { get; }
    ILoggerFactory LoggerFactory { get; }
    IConfiguration Configuration { get; }
}