using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace GpibUtils.Common
{
    /// <summary>
    /// A small, persistent map of instrument key → VISA resource string, so the bench's <b>actual</b> GPIB
    /// addresses can be configured once and reused, instead of being hardcoded constants or passed on every
    /// command line. Each driver's <c>DefaultResource</c> remains the documented manual factory default; a
    /// stored entry overrides it, and an explicit address (e.g. CLI <c>--address</c>) overrides that — see
    /// <see cref="Resolve"/>. Shared by the Console and WPF front-ends (issue #54).
    ///
    /// <para>Persisted as JSON at <see cref="DefaultConfigPath"/> (override with the
    /// <c>GPIBUTILS_CONFIG</c> environment variable). Device keys are matched case-insensitively (stored
    /// lower-case), matching the CLI branch names (<c>hp8340b</c>, <c>hp8902a</c>, …).</para>
    /// </summary>
    public sealed class InstrumentAddressStore
    {
        private readonly Dictionary<string, string> _map =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>The file this store was loaded from / will save to.</summary>
        public string Path { get; private set; }

        private InstrumentAddressStore(string path) { Path = path; }

        /// <summary>Default config-file path: <c>%APPDATA%\GpibUtils\addresses.json</c>, or the
        /// <c>GPIBUTILS_CONFIG</c> environment variable when set.</summary>
        public static string DefaultConfigPath
        {
            get
            {
                var env = Environment.GetEnvironmentVariable("GPIBUTILS_CONFIG");
                if (!string.IsNullOrWhiteSpace(env)) return env;
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GpibUtils");
                return System.IO.Path.Combine(dir, "addresses.json");
            }
        }

        /// <summary>The configured device → resource entries (a copy; case-insensitive keys, sorted).</summary>
        public IReadOnlyDictionary<string, string> Entries =>
            _map.OrderBy(kv => kv.Key, StringComparer.Ordinal).ToDictionary(kv => kv.Key, kv => kv.Value);

        /// <summary>Loads the store from <paramref name="path"/> (default <see cref="DefaultConfigPath"/>).
        /// A missing file yields an empty store — it is not an error to have no config yet.</summary>
        public static InstrumentAddressStore Load(string path = null)
        {
            path = path ?? DefaultConfigPath;
            var store = new InstrumentAddressStore(path);
            if (!File.Exists(path)) return store;

            var doc = ReadFile(File.ReadAllBytes(path));
            if (doc?.addresses != null)
                foreach (var e in doc.addresses)
                    if (!string.IsNullOrWhiteSpace(e?.device) && !string.IsNullOrWhiteSpace(e.resource))
                        store._map[e.device.Trim()] = e.resource.Trim();
            return store;
        }

        /// <summary>Writes the store to <paramref name="path"/> (default: where it was loaded from),
        /// creating the directory if needed.</summary>
        public void Save(string path = null)
        {
            path = path ?? Path;
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var doc = new AddressFile
            {
                addresses = _map.OrderBy(kv => kv.Key, StringComparer.Ordinal)
                                .Select(kv => new AddressEntry { device = kv.Key, resource = kv.Value })
                                .ToList()
            };
            File.WriteAllBytes(path, WriteDoc(doc));
            Path = path;
        }

        /// <summary>Gets the configured resource for <paramref name="device"/>, or false if none is stored.</summary>
        public bool TryGet(string device, out string resource)
        {
            resource = null;
            return !string.IsNullOrWhiteSpace(device) && _map.TryGetValue(device.Trim(), out resource);
        }

        /// <summary>Stores (or replaces) the resource for <paramref name="device"/>.</summary>
        public void Set(string device, string resource)
        {
            if (string.IsNullOrWhiteSpace(device)) throw new ArgumentException("Device key is required.", nameof(device));
            if (string.IsNullOrWhiteSpace(resource)) throw new ArgumentException("Resource string is required.", nameof(resource));
            _map[device.Trim()] = resource.Trim();
        }

        /// <summary>Removes the override for <paramref name="device"/>; returns whether one existed.</summary>
        public bool Remove(string device) =>
            !string.IsNullOrWhiteSpace(device) && _map.Remove(device.Trim());

        /// <summary>
        /// Resolves the address to open for a device, applying the precedence
        /// <b>explicit &gt; configured &gt; default</b>: an explicit <paramref name="explicitAddress"/>
        /// (e.g. a CLI <c>--address</c>) wins; otherwise a stored override for <paramref name="device"/>;
        /// otherwise the driver's <paramref name="defaultAddress"/> (its manual factory default).
        /// </summary>
        public string Resolve(string explicitAddress, string device, string defaultAddress)
        {
            if (!string.IsNullOrWhiteSpace(explicitAddress)) return explicitAddress.Trim();
            return TryGet(device, out var configured) ? configured : defaultAddress;
        }

        // ---- JSON (framework-native DataContractJsonSerializer; no NuGet dependency) ------------------

        [DataContract]
        internal sealed class AddressFile
        {
            [DataMember(Name = "addresses", Order = 0)]
            public List<AddressEntry> addresses { get; set; }
        }

        [DataContract]
        internal sealed class AddressEntry
        {
            [DataMember(Name = "device", Order = 0)]
            public string device { get; set; }

            [DataMember(Name = "resource", Order = 1)]
            public string resource { get; set; }
        }

        private static AddressFile ReadFile(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return null;
            using (var ms = new MemoryStream(bytes))
            {
                var ser = new DataContractJsonSerializer(typeof(AddressFile));
                return (AddressFile)ser.ReadObject(ms);
            }
        }

        private static byte[] WriteDoc(AddressFile doc)
        {
            using (var ms = new MemoryStream())
            {
                var settings = new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
                var ser = new DataContractJsonSerializer(typeof(AddressFile), settings);
                using (var w = JsonReaderWriterFactory.CreateJsonWriter(ms, Encoding.UTF8, ownsStream: false, indent: true))
                    ser.WriteObject(w, doc);
                return ms.ToArray();
            }
        }
    }
}
