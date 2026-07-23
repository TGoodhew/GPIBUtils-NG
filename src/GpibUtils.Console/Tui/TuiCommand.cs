using System.ComponentModel;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Tui
{
    /// <summary>
    /// Launches the interactive TUI (issue #172). Also the target of running <c>gpibutils</c> with no verb
    /// on a terminal. The one-shot CLI verbs are unchanged and remain the automation/scripting surface.
    /// </summary>
    public sealed class TuiCommand : Command<TuiCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-p|--provider <NAME>")]
            [Description("Provider to start on (e.g. Simulated, NI-VISA). Defaults to an available provider.")]
            public string Provider { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            new TuiApp(settings.Provider).Run();
    }
}
