using System;
using System.ComponentModel;
using GpibUtils.Instruments.Analyzers;
using Spectre.Console;
using Spectre.Console.Cli;

namespace GpibUtils.Console.Instruments
{
    /// <summary>Shared options for every <c>hp85620a</c> subcommand (the standard instrument options).</summary>
    public class Hp85620ASettings : InstrumentSettings
    {
        /// <summary>Opens a session (default GPIB0::18::INSTR, the host analyzer) and builds the driver.</summary>
        internal Hp85620A OpenDriver(out Visa.IInstrumentSession session)
        {
            session = OpenSession("hp85620a", Hp85620A.DefaultResource);
            return new Hp85620A(session);
        }

        /// <summary>Parses a device token (mem/card) to the enum.</summary>
        internal static MassStorageDevice ParseDevice(string token)
        {
            switch ((token ?? "mem").Trim().ToLowerInvariant())
            {
                case "mem": case "module": case "memory": return MassStorageDevice.Module;
                case "card": case "fram": case "sram": return MassStorageDevice.Card;
                default: throw new ArgumentException($"Unknown device '{token}'. Use mem or card.");
            }
        }
    }

    /// <summary>Shared execution shell: open, run, echo the commands sent, and (optionally) print a result.</summary>
    internal static class Hp85620ARunner
    {
        public static int Run(Hp85620ASettings settings, Func<Hp85620A, string> action) => Runner.Guard(() =>
        {
            var driver = settings.OpenDriver(out var session);
            using (session)
            {
                string result = action(driver);
                foreach (var sent in driver.History)
                    AnsiConsole.MarkupLineInterpolated($"[grey]sent[/]: [green]{sent}[/]");
                if (!string.IsNullOrEmpty(result))
                    AnsiConsole.MarkupLineInterpolated($"[green]{result}[/]");
            }
            return 0;
        });
    }

    /// <summary>Show the instrument identity (ID?).</summary>
    public sealed class Hp85620AIdnCommand : Command<Hp85620AIdnCommand.Settings>
    {
        public sealed class Settings : Hp85620ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp85620ARunner.Run(settings, d => d.Identify());
    }

    /// <summary>Catalog a storage device (mem or card): list entries + free bytes.</summary>
    public sealed class Hp85620ACatalogCommand : Command<Hp85620ACatalogCommand.Settings>
    {
        public sealed class Settings : Hp85620ASettings
        {
            [CommandArgument(0, "[device]")]
            [Description("Storage device: mem (module RAM, default) or card.")]
            public string Device { get; set; } = "mem";
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp85620ARunner.Run(settings, d =>
            {
                var cat = d.Catalog(Hp85620ASettings.ParseDevice(settings.Device));
                var table = new Table().AddColumn("Entry");
                foreach (var e in cat.Entries) table.AddRow(Markup.Escape(e));
                AnsiConsole.Write(table);
                return $"{cat.Entries.Length} entr{(cat.Entries.Length == 1 ? "y" : "ies")}, {cat.BytesFree} bytes free";
            });
    }

    /// <summary>Store a named module entry onto the card (CARDSTORE).</summary>
    public sealed class Hp85620AStoreCommand : Command<Hp85620AStoreCommand.Settings>
    {
        public sealed class Settings : Hp85620ASettings
        {
            [CommandArgument(0, "<name>")]
            [Description("Entry name to store from module memory to the card.")]
            public string Name { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp85620ARunner.Run(settings, d => { d.StoreToCard(settings.Name); return $"stored '{settings.Name}' to card"; });
    }

    /// <summary>Load a named entry from the card into module memory (CARDLOAD).</summary>
    public sealed class Hp85620ALoadCommand : Command<Hp85620ALoadCommand.Settings>
    {
        public sealed class Settings : Hp85620ASettings
        {
            [CommandArgument(0, "<name>")]
            [Description("Entry name to load from the card into module memory.")]
            public string Name { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp85620ARunner.Run(settings, d => { d.LoadFromCard(settings.Name); return $"loaded '{settings.Name}' into module memory"; });
    }

    /// <summary>Dispose all entries in module memory (DISPOSE ALL).</summary>
    public sealed class Hp85620AClearCommand : Command<Hp85620AClearCommand.Settings>
    {
        public sealed class Settings : Hp85620ASettings { }

        public override int Execute(CommandContext context, Settings settings) =>
            Hp85620ARunner.Run(settings, d => { d.ClearModule(); return "module memory cleared (DISPOSE ALL)"; });
    }

    /// <summary>Offline: decode a raw 85620A SRAM dump and list/export the stored DLP programs (no GPIB).</summary>
    public sealed class Hp85620ADecodeCommand : Command<Hp85620ADecodeCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<image>")]
            [Description("Path to a raw 85620A SRAM dump (e.g. SRAM_85620A.bin).")]
            public string Image { get; set; }

            [CommandOption("--out <DIR>")]
            [Description("Write each extracted DLP to this directory as dlp_NN.bin.")]
            public string OutDir { get; set; }
        }

        public override int Execute(CommandContext context, Settings settings) => Runner.Guard(() =>
        {
            if (!System.IO.File.Exists(settings.Image))
            {
                AnsiConsole.MarkupLineInterpolated($"[red]SRAM image not found:[/] {settings.Image}");
                return 1;
            }

            var dlps = Hp85620ASramImage.DecodeDlps(System.IO.File.ReadAllBytes(settings.Image));
            var table = new Table().AddColumn("DLP").AddColumn("Bytes").AddColumn("Preview");
            for (int i = 0; i < dlps.Count; i++)
            {
                var preview = System.Text.Encoding.ASCII.GetString(dlps[i], 0, System.Math.Min(40, dlps[i].Length))
                    .Replace('\r', ' ').Replace('\n', ' ');
                table.AddRow((i + 1).ToString(), dlps[i].Length.ToString(), Markup.Escape(preview));
            }
            AnsiConsole.Write(table);

            if (!string.IsNullOrWhiteSpace(settings.OutDir))
            {
                System.IO.Directory.CreateDirectory(settings.OutDir);
                for (int i = 0; i < dlps.Count; i++)
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(settings.OutDir, $"dlp_{i + 1:00}.bin"), dlps[i]);
                AnsiConsole.MarkupLineInterpolated($"[green]wrote {dlps.Count} DLP file(s) to {settings.OutDir}[/]");
            }
            AnsiConsole.MarkupLineInterpolated($"[grey]{dlps.Count} DLP(s) extracted[/]");
            return 0;
        });
    }
}
