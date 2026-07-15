using System;
using System.Collections.Generic;

namespace GpibUtils.Instruments.Analyzers
{
    /// <summary>
    /// Offline decoder for a raw SRAM dump of the HP 85620A Mass Memory Module — de-scrambles the module's
    /// address and data bit permutations and extracts the stored Downloadable Programs (DLPs). Ported from
    /// the <c>DLPBits</c> app (issue #14); the de-scramble algorithm is credited to Kirril, applied to the
    /// KO4BB SRAM image. This is a pure-software file utility (no GPIB); the extracted DLP bodies can then be
    /// downloaded to a live analyzer with <see cref="Hp85620A.DefineFunction"/> (<c>FUNCDEF</c>).
    /// </summary>
    public static class Hp85620ASramImage
    {
        /// <summary>A stored DLP begins after this two-byte marker (0x10, 0x80).</summary>
        public static readonly byte[] DlpStartMarker = { 0x10, 0x80 };

        /// <summary>A stored DLP ends at this two-byte marker (0x3B, 0xFF).</summary>
        public static readonly byte[] DlpEndMarker = { 0x3B, 0xFF };

        /// <summary>
        /// Translates an output byte position to the scrambled source address it reads from — the module's
        /// address-line permutation (DLPBits <c>AddrXlat</c>).
        /// </summary>
        public static int TranslateAddress(int a) =>
            ((a << 10) & 1024) |
            ((a << 10) & 2048) |
            ((a << 7) & 512) |
            ((a << 10) & 8192) |
            ((a << 10) & 16384) |
            ((a << 2) & 128) |
            ((a >> 1) & 32) |
            ((a >> 4) & 8) |
            ((a >> 4) & 16) |
            ((a >> 3) & 64) |
            ((a >> 2) & 256) |
            ((a << 1) & 4096) |
            ((a >> 11) & 2) |
            ((a >> 11) & 4) |
            ((a >> 14) & 1) |
            (a & 0x18000);

        /// <summary>De-scrambles one data byte — the module's data-line bit permutation (DLPBits).</summary>
        public static byte DescrambleByte(byte d) => (byte)(
            (d >> 7) |
            ((d << 1) & 2) |
            ((d << 1) & 4) |
            ((d << 1) & 8) |
            ((d >> 2) & 16) |
            (d & 32) |
            ((d << 2) & 64) |
            ((d << 4) & 128));

        /// <summary>
        /// De-scrambles a full SRAM image: for each output position, reads the byte at its translated source
        /// address and applies the data de-scramble. Throws if a translated address falls outside the image
        /// (an image that is too small / not a full module dump).
        /// </summary>
        public static byte[] Descramble(byte[] image)
        {
            if (image == null) throw new ArgumentNullException(nameof(image));
            var outBytes = new byte[image.Length];
            for (int p = 0; p < image.Length; p++)
            {
                int src = TranslateAddress(p);
                if (src < 0 || src >= image.Length)
                    throw new FormatException(
                        $"SRAM address translation out of range at position {p} -> {src} (image length {image.Length}). " +
                        "This does not look like a full 85620A module dump.");
                outBytes[p] = DescrambleByte(image[src]);
            }
            return outBytes;
        }

        /// <summary>Extracts every DLP body found between the start (0x10,0x80) and end (0x3B,0xFF) markers in
        /// already-de-scrambled data. The returned arrays exclude the markers themselves.</summary>
        public static IReadOnlyList<byte[]> ExtractDlps(byte[] data)
        {
            var parts = new List<byte[]>();
            if (data == null || data.Length == 0) return parts;

            int index = 0;
            while (index < data.Length)
            {
                int start = FindSequence(data, DlpStartMarker, index);
                if (start < 0) break;
                start += DlpStartMarker.Length;

                int end = FindSequence(data, DlpEndMarker, start);
                if (end < 0) break;

                int length = end - start;
                if (length > 0)
                {
                    var part = new byte[length];
                    Array.Copy(data, start, part, 0, length);
                    parts.Add(part);
                }
                index = end + DlpEndMarker.Length;
            }
            return parts;
        }

        /// <summary>De-scrambles <paramref name="image"/> and extracts the stored DLP bodies in one step.</summary>
        public static IReadOnlyList<byte[]> DecodeDlps(byte[] image) => ExtractDlps(Descramble(image));

        /// <summary>First index of <paramref name="sequence"/> in <paramref name="data"/> at or after
        /// <paramref name="startIndex"/>, or -1.</summary>
        internal static int FindSequence(byte[] data, byte[] sequence, int startIndex)
        {
            if (data == null || sequence == null || sequence.Length == 0 || startIndex < 0) return -1;
            for (int i = startIndex; i <= data.Length - sequence.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < sequence.Length; j++)
                {
                    if (data[i + j] != sequence[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }
}
