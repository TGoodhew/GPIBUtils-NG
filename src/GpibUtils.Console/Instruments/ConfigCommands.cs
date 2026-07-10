using System.ComponentModel;
using GpibUtils.Common;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for the <c>config address</c> commands.</summary>
    public class ConfigSettings : CommandSettings
    {
        [CommandOption("--file <PATH>")]
        [Description("Config file path (default: %APPDATA%\\GpibUtils\\addresses.json, or $GPIBUTILS_CONFIG).")]
        public string File { get; set; }

        internal InstrumentAddressStore LoadStore() =>
            InstrumentAddressStore.Load(string.IsNullOrWhiteSpace(File) ? null : File.Trim());
    }

    /// <summary>Lists every known instrument with its effective GPIB address and where it comes from.</summary>
    public sealed class ConfigAddressListCommand : Command<ConfigAddressListCommand.Settings>
    {
        public sealed class Settings : ConfigSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var store = settings.LoadStore();
            var table = new Table().RoundedBorder();
            table.AddColumn("Device");
            table.AddColumn("Address");
            table.AddColumn("Source");
            table.AddColumn("Instrument");

            foreach (var inst in KnownInstruments.All)
            {
                bool configured = store.TryGet(inst.Key, out var addr);
                table.AddRow(
                    Markup.Escape(inst.Key),
                    Markup.Escape(configured ? addr : inst.DefaultResource),
                    configured ? "[green]configured[/]" : "[grey]default[/]",
                    Markup.Escape(inst.Description));
            }

            AnsiConsole.Write(table);
            AnsiConsole.MarkupLineInterpolated($"[grey]config file:[/] {store.Path}");
            return 0;
        });
    }

    /// <summary>Shows the effective address for one device.</summary>
    public sealed class ConfigAddressGetCommand : Command<ConfigAddressGetCommand.Settings>
    {
        public sealed class Settings : ConfigSettings
        {
            [CommandArgument(0, "<device>")]
            [Description("Instrument key (e.g. hp8340b).")]
            public string Device { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            if (!KnownInstruments.TryGet(settings.Device, out var inst))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Unknown device[/] '{settings.Device}'. Known: {KnownInstruments.KeyList}");
                return 1;
            }
            var store = settings.LoadStore();
            bool configured = store.TryGet(inst.Key, out var addr);
            var effective = configured ? addr : inst.DefaultResource;
            var note = $"{(configured ? "configured" : "default")}; manual default {inst.DefaultResource}";
            AnsiConsole.MarkupLineInterpolated($"[green]{inst.Key}[/]: {effective} [grey]({note})[/]");
            return 0;
        });
    }

    /// <summary>Stores (overrides) the GPIB address for one device.</summary>
    public sealed class ConfigAddressSetCommand : Command<ConfigAddressSetCommand.Settings>
    {
        public sealed class Settings : ConfigSettings
        {
            [CommandArgument(0, "<device>")]
            [Description("Instrument key (e.g. hp8340b).")]
            public string Device { get; set; }

            [CommandArgument(1, "<resource>")]
            [Description("VISA resource string, e.g. GPIB0::20::INSTR.")]
            public string Resource { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            if (!KnownInstruments.TryGet(settings.Device, out var inst))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Unknown device[/] '{settings.Device}'. Known: {KnownInstruments.KeyList}");
                return 1;
            }
            var store = settings.LoadStore();
            store.Set(inst.Key, settings.Resource);
            store.Save();
            AnsiConsole.MarkupLineInterpolated($"[green]set[/] {inst.Key} = {settings.Resource.Trim()}  [grey]({store.Path})[/]");
            return 0;
        });
    }

    /// <summary>Removes a device's stored override (reverting it to the manual default).</summary>
    public sealed class ConfigAddressClearCommand : Command<ConfigAddressClearCommand.Settings>
    {
        public sealed class Settings : ConfigSettings
        {
            [CommandArgument(0, "<device>")]
            [Description("Instrument key (e.g. hp8340b).")]
            public string Device { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            if (!KnownInstruments.TryGet(settings.Device, out var inst))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]Unknown device[/] '{settings.Device}'. Known: {KnownInstruments.KeyList}");
                return 1;
            }
            var store = settings.LoadStore();
            if (store.Remove(inst.Key))
            {
                store.Save();
                AnsiConsole.MarkupLineInterpolated($"[green]cleared[/] {inst.Key} — reverts to default {inst.DefaultResource}");
            }
            else
            {
                AnsiConsole.MarkupLineInterpolated($"[grey]{inst.Key} had no override; default {inst.DefaultResource}[/]");
            }
            return 0;
        });
    }

    /// <summary>Prints the config-file path (and whether it exists).</summary>
    public sealed class ConfigPathCommand : Command<ConfigPathCommand.Settings>
    {
        public sealed class Settings : ConfigSettings { }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            var store = settings.LoadStore();
            bool exists = System.IO.File.Exists(store.Path);
            AnsiConsole.MarkupLineInterpolated($"{store.Path} [grey]({(exists ? "exists" : "not created yet")})[/]");
            return 0;
        });
    }
}
