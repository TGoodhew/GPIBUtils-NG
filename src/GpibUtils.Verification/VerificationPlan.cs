using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace GpibUtils.Verification
{
    /// <summary>Loads a verification plan from an inline point list or a CSV file, and writes results to CSV.</summary>
    public static class VerificationPlan
    {
        /// <summary>Parses a comma/space/semicolon-separated list of nominal voltages into points, applying
        /// the run's global range + default tolerance to each.</summary>
        public static List<VerificationPoint> ParseInlinePoints(string raw, string globalRange = null, double? defaultTolerancePpm = null)
        {
            var points = new List<VerificationPoint>();
            if (string.IsNullOrWhiteSpace(raw)) return points;
            foreach (var token in raw.Split(new[] { ',', ' ', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!double.TryParse(token.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    throw new FormatException($"Could not parse point '{token}'.");
                points.Add(new VerificationPoint { NominalVolts = v, Range = globalRange, TolerancePpm = defaultTolerancePpm });
            }
            return points;
        }

        /// <summary>
        /// Parses a CSV plan file. The header must contain <c>nominal_V</c>; optional columns are
        /// <c>range</c>, <c>tolerance_ppm</c>, <c>notes</c>. Blank lines and lines starting with <c>#</c>
        /// are ignored.
        /// </summary>
        public static List<VerificationPoint> ParsePlanFile(string path, string globalRange = null, double? defaultTolerancePpm = null)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("Plan file not found: " + path);
            return ParsePlanLines(File.ReadAllLines(path), globalRange, defaultTolerancePpm);
        }

        internal static List<VerificationPoint> ParsePlanLines(IReadOnlyList<string> lines, string globalRange, double? defaultTolerancePpm)
        {
            var points = new List<VerificationPoint>();
            string[] header = null;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                var cols = line.Split(',').Select(c => c.Trim()).ToArray();
                if (header == null)
                {
                    header = cols.Select(c => c.ToLowerInvariant()).ToArray();
                    if (Array.IndexOf(header, "nominal_v") < 0)
                        throw new FormatException("Plan file header must contain 'nominal_V'.");
                    continue;
                }

                var p = new VerificationPoint { Range = globalRange, TolerancePpm = defaultTolerancePpm };
                bool haveNominal = false;
                for (int c = 0; c < cols.Length && c < header.Length; c++)
                {
                    var val = cols[c].Trim();
                    if (val.Length == 0) continue;
                    switch (header[c])
                    {
                        case "nominal_v":
                            if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var nv))
                                throw new FormatException($"Line {i + 1}: bad nominal_V '{val}'.");
                            p.NominalVolts = nv; haveNominal = true; break;
                        case "range": p.Range = val; break;
                        case "tolerance_ppm":
                            if (!double.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var tol))
                                throw new FormatException($"Line {i + 1}: bad tolerance_ppm '{val}'.");
                            p.TolerancePpm = tol; break;
                        case "notes": p.Notes = val; break;
                    }
                }
                if (haveNominal) points.Add(p);
            }
            return points;
        }

        /// <summary>Serializes results to CSV text (the same schema the legacy runner wrote).</summary>
        public static string ToCsv(IEnumerable<VerificationResult> results, string timestampIso = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("idx,nominal_V,range,measured_V,abs_err_V,ppm_of_reading,stddev_V,samples,tolerance_ppm,verdict,timestamp_iso,notes");
            string ts = timestampIso ?? string.Empty;
            foreach (var r in results)
            {
                sb.Append(r.Index).Append(',')
                  .Append(G(r.NominalVolts, 7)).Append(',')
                  .Append(r.Range ?? "").Append(',')
                  .Append(double.IsNaN(r.MeasuredVolts) ? "" : G(r.MeasuredVolts, 9)).Append(',')
                  .Append(double.IsNaN(r.AbsErrorVolts) ? "" : G(r.AbsErrorVolts, 6)).Append(',')
                  .Append(double.IsNaN(r.PpmOfReading) ? "" : G(r.PpmOfReading, 6)).Append(',')
                  .Append(double.IsNaN(r.StdDevVolts) ? "" : G(r.StdDevVolts, 6)).Append(',')
                  .Append(r.Samples).Append(',')
                  .Append(r.TolerancePpm.HasValue ? r.TolerancePpm.Value.ToString("G6", CultureInfo.InvariantCulture) : "").Append(',')
                  .Append(r.Verdict ?? "").Append(',')
                  .Append(ts).Append(',')
                  .Append(((r.Errored ? "ERROR: " + r.Error + " " : "") + (r.Notes ?? "")).Replace(",", " "));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static string G(double v, int sig) => v.ToString("G" + sig, CultureInfo.InvariantCulture);
    }
}
