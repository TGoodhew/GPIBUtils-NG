namespace GpibUtils.Visa
{
    /// <summary>
    /// A decoded backend status for a failed operation: a short name, a plain-English meaning, and the
    /// raw numeric code when the backend has one (e.g. a VISA status). <see cref="Empty"/> when the
    /// backend could not decode the failure (the raw exception message is then used instead).
    /// </summary>
    public readonly struct GpibStatus
    {
        public string Name { get; }
        public string Meaning { get; }
        public int? Code { get; }

        public bool HasName => !string.IsNullOrEmpty(Name);

        public GpibStatus(string name, string meaning, int? code = null)
        {
            Name = name;
            Meaning = meaning;
            Code = code;
        }

        public static readonly GpibStatus Empty = new GpibStatus(null, null);

        public override string ToString()
        {
            if (!HasName) return "(undecoded)";
            return Code.HasValue ? $"{Name} (0x{Code.Value:X8}): {Meaning}" : $"{Name}: {Meaning}";
        }
    }
}
